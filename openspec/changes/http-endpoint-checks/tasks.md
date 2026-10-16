# Tasks

## 1. Add configuration and endpoint validation

- [x] 1.1 Add the `ControlPlane.HttpEndpoint` project, settings type, configuration binding, solution and host references. The full solution build (`dotnet build src/ControlPlane.slnx --configuration Release --no-restore`) now passes with two pre-existing NU1902 warnings (MessagePack) and zero errors after the AppHost test import fix. AppHost tests still need to run in CI because the local Docker runtime is unhealthy (not `ASPIRE009`).
- [x] 1.2 Add the empty `HttpEndpoint__ConfigPath` placeholder to `.env`; resolve relative paths from the API content root; verify unset, missing, unreadable, and malformed configuration behavior.
- [x] 1.3 Validate endpoint IDs, duplicate IDs, URLs, methods, expected status codes, timeout values, and the 20-endpoint limit; verify invalid entries are excluded and reported without URLs.

## 2. Implement manual endpoint checks

- [x] 2.1 Add one injectable checker using a named `IHttpClientFactory` client; disable retries, circuit breaking, redirects, URL-bearing logs, and URL trace attributes; use `ResponseHeadersRead`; verify these behaviors with handler and captured-log/activity tests.
- [x] 2.2 Add operation ID `http-endpoint-check`; accept only a configured endpoint ID and return safe summaries with stable error codes; verify unknown IDs are rejected and cause no request.
- [x] 2.3 Add a runnable `.http` example with placeholder values; verify `python3 scripts/check_plugin_http_examples.py` passes.
- [x] 2.4 Use a real local HTTP server to verify redirects are not followed, timeout/cancellation works, a 503 response causes no retry, and response bodies remain unread.

## 3. Expose endpoint status

- [x] 3.1 Add an `IStatusProvider` that maps healthy, degraded, unavailable, invalid, and disabled states to the defined `StatusLevel` values; verify status mapping and safe signals.
- [x] 3.2 Add a shared in-flight overview scan, a 15-second result cache, and a 12-second scan budget; verify concurrent callers share a scan and stalled endpoints do not exceed the budget.

## 4. Verify the complete feature slice

- [ ] 4.1 Add an Aspire-backed E2E test that starts a local fabricated HTTP endpoint, reads overview status, invokes the operation, and verifies safe execution history; verify no live external requests occur. The test now compiles, but it has not executed; CI must run it to verify it passes, so keep unchecked.
- [ ] 4.2 Run `dotnet build src/ControlPlane.slnx`, focused tests, the full available test suite, `npm run lint --prefix ui`, `npm run build --prefix ui`, and the documented OpenSpec and plugin-example checks; record environment limits separately. The full solution build passes; API (69 passed, including configuration validation) and API E2E (3 passed) tests pass; UI lint, build, and tests all pass with 14 UI tests passed; and the plugin-example, strict change, all-spec OpenSpec, and `git diff --check` checks pass. The AppHost/full test suite remains unverified because all 3 AppHost tests stopped at the unhealthy Docker runtime (`Container runtime 'docker' was found but appears to be unhealthy`), so keep unchecked.

## Workflow follow-up

- [x] Obtain independent high-capability review of the exact proposal revision. Resolve blocking findings and re-review material changes. (Proposal PR #54 was independently reviewed and merged; no blocking findings remain.)
- [x] Obtain Michal's explicit design approval before implementation. This proposal does not authorize implementation, merge, deployment, or archive. (Michal authorized implementation with `piv 6`; implementation remains open.)
- This slice is manual GET/HEAD only. Scheduled checks and POST remain deferred and are not complete under this change.
- Keep issue #6 open; it remains open.
- Do not add plugin-specific agent tools or claim agent parity before the shared contract is approved and implemented.
- [ ] Coordinate future agent projection with #48; this remains an open follow-up.
