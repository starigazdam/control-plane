#!/usr/bin/env python3
"""Fabricated-data container acceptance checks for Control Plane.

These checks run against a real built image over its published HTTP port. They
use only fabricated records and one rejected unknown operation. The API's status
providers return local fabricated snapshots and the selected operation is
unknown to the host, so the request is rejected and recorded without any live
cloud, broker, or agent call.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import time
import urllib.error
import urllib.request

FABRICATED_OPERATION_ID = "fabricated-unknown-operation-37"
FABRICATED_CORRELATION_ID = "fabricated-correlation-37"

_OPENER = urllib.request.build_opener(urllib.request.ProxyHandler({}))


def request(url: str, method: str = "GET", body: object | None = None, timeout: int = 30):
    data = None
    headers: dict[str, str] = {}
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with _OPENER.open(req, timeout=timeout) as response:
            return (
                response.status,
                response.headers.get("Content-Type", ""),
                response.read().decode("utf-8", "replace"),
            )
    except urllib.error.HTTPError as error:
        content_type = error.headers.get("Content-Type", "") if error.headers else ""
        return error.code, content_type, error.read().decode("utf-8", "replace")


def read_history(url: str) -> list:
    status, _, body = request(f"{url}/api/operations/history?take=200")
    assert status == 200, f"history returned {status}: {body}"
    payload = json.loads(body)
    assert isinstance(payload, list), "history response must be a JSON list"
    return payload


def command_wait(args) -> None:
    deadline = time.monotonic() + args.timeout
    last: object = None
    while time.monotonic() < deadline:
        try:
            status, _, _ = request(f"{args.url}/api/version", timeout=5)
            if status == 200:
                print("ready")
                return
            last = f"HTTP {status}"
        except OSError as error:  # connection refused/reset while the host boots
            last = error
        time.sleep(2)
    raise AssertionError(f"service did not become ready within {args.timeout}s (last: {last})")


def command_version(args) -> None:
    status, _, body = request(f"{args.url}/api/version")
    assert status == 200, f"/api/version returned {status}: {body}"
    payload = json.loads(body)
    assert isinstance(payload, dict), "version payload must be a JSON object"
    assert list(payload.keys()) == ["commitSha"], (
        f"version payload must contain only commitSha, got {sorted(payload.keys())}"
    )
    sha = payload["commitSha"]
    assert isinstance(sha, str), "commitSha must be a string"
    assert re.fullmatch(r"[0-9a-f]{40}", sha), f"commitSha must be a full lowercase SHA, got {sha!r}"
    if args.expect_sha:
        assert sha == args.expect_sha, f"served SHA {sha} does not match expected {args.expect_sha}"
    print(sha)


def command_static(args) -> None:
    status, content_type, body = request(f"{args.url}/")
    assert status == 200, f"/ returned {status}"
    assert "text/html" in content_type.lower(), f"/ content type was {content_type!r}"
    asset = re.search(r'(?:src|href)="(/[^"]+)"', body)
    assert asset, "compiled UI document references no static asset"
    asset_path = asset.group(1)
    asset_status, _, asset_body = request(f"{args.url}{asset_path}")
    assert asset_status == 200, f"static asset {asset_path} returned {asset_status}"
    assert asset_body, f"static asset {asset_path} was empty"
    print(f"served / and {asset_path}")


def command_unknown(args) -> None:
    status, content_type, body = request(f"{args.url}/api/unknown")
    assert status == 404, f"/api/unknown returned {status}"
    assert "text/html" not in content_type.lower(), "/api/unknown returned the UI document"
    assert "<html" not in body.lower(), "/api/unknown returned HTML"
    print("/api/unknown returned 404 without the UI document")


def command_omissions(args) -> None:
    for path in ("/health", "/alive", "/swagger/index.html", "/swagger/v1/swagger.json"):
        status, _, _ = request(f"{args.url}{path}")
        assert status == 404, f"{path} returned {status} in the production container"
    print("production container omits /health, /alive and Swagger/OpenAPI")


def command_seed(args) -> None:
    status, _, body = request(f"{args.url}/api/overview")
    assert status == 200, f"overview returned {status}: {body}"
    overview = json.loads(body)
    projects = overview.get("projects") or []
    assert projects, "overview returned no projects"
    project_id = projects[0].get("projectId")
    assert project_id, "placeholder project id was empty"

    execute_status, _, execute_body = request(
        f"{args.url}/api/operations/execute",
        method="POST",
        body={
            "projectId": project_id,
            "operationId": FABRICATED_OPERATION_ID,
            "input": {"fabricated": "true"},
            "requestedBy": "container-acceptance",
            "correlationId": FABRICATED_CORRELATION_ID,
        },
    )
    assert execute_status == 200, f"execute returned {execute_status}: {execute_body}"
    execution = json.loads(execute_body)
    result = execution.get("result") or {}
    assert result.get("status") == "Rejected", f"unknown operation was not rejected: {result.get('status')!r}"
    assert result.get("errorCode") == "operation_not_found", (
        f"unexpected error code: {result.get('errorCode')!r}"
    )

    history = read_history(args.url)
    matching = [entry for entry in history if entry.get("correlationId") == FABRICATED_CORRELATION_ID]
    assert len(matching) == 1, (
        f"expected exactly one persisted entry for the fabricated correlation id, got {len(matching)}"
    )

    state = {
        "projectId": project_id,
        "operationId": FABRICATED_OPERATION_ID,
        "correlationId": FABRICATED_CORRELATION_ID,
        "matchingCount": len(matching),
        "totalCount": len(history),
    }
    with open(args.out, "w", encoding="utf-8") as handle:
        json.dump(state, handle, indent=2, sort_keys=True)
    print(json.dumps(state, sort_keys=True))


def command_verify(args) -> None:
    with open(args.state, encoding="utf-8") as handle:
        state = json.load(handle)

    history = read_history(args.url)
    matching = [
        entry
        for entry in history
        if entry.get("correlationId") == state["correlationId"]
        and entry.get("projectId") == state["projectId"]
        and entry.get("operationId") == state["operationId"]
    ]
    assert len(matching) == state["matchingCount"], (
        f"expected {state['matchingCount']} preserved entries, got {len(matching)}"
    )
    assert len(history) == state["totalCount"], (
        f"expected {state['totalCount']} total entries, got {len(history)}"
    )

    result = matching[0].get("result") or {}
    assert result.get("status") == "Rejected", "stored history no longer records a rejection"
    assert result.get("errorCode") == "operation_not_found", "stored history lost the rejection error code"
    print(json.dumps(state, sort_keys=True))


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    wait = subparsers.add_parser("wait", help="poll /api/version until the service is ready")
    wait.add_argument("--url", required=True)
    wait.add_argument("--timeout", type=int, default=120)

    version = subparsers.add_parser("version", help="assert the served build identity")
    version.add_argument("--url", required=True)
    version.add_argument("--expect-sha")

    static = subparsers.add_parser("static", help="assert the compiled UI is served")
    static.add_argument("--url", required=True)

    unknown = subparsers.add_parser("unknown", help="assert an unmapped API path returns 404")
    unknown.add_argument("--url", required=True)

    omissions = subparsers.add_parser("omissions", help="assert development-only endpoints are absent")
    omissions.add_argument("--url", required=True)

    seed = subparsers.add_parser("seed", help="write one rejected fabricated operation and record the tuple")
    seed.add_argument("--url", required=True)
    seed.add_argument("--out", required=True)

    verify = subparsers.add_parser("verify", help="verify a previously recorded tuple and count")
    verify.add_argument("--url", required=True)
    verify.add_argument("--state", required=True)

    return parser


def main(argv: list[str]) -> int:
    args = build_parser().parse_args(argv)
    handlers = {
        "wait": command_wait,
        "version": command_version,
        "static": command_static,
        "unknown": command_unknown,
        "omissions": command_omissions,
        "seed": command_seed,
        "verify": command_verify,
    }
    handlers[args.command](args)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv[1:]))
    except AssertionError as failure:
        print(f"FAILED: {failure}", file=sys.stderr)
        sys.exit(1)
