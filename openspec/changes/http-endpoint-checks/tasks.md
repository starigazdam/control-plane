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

- [x] 4.1 Add an Aspire-backed E2E test that starts a local fabricated HTTP endpoint, reads overview status, invokes the operation, and verifies safe execution history; verify no live external requests occur. The test compiled and all 3 AppHost tests passed in CI run `39013693234` on exact head `0c30ae7de9b40c1f701c6015368609ff23266284`.
- [x] 4.2 Run `dotnet build src/ControlPlane.slnx`, focused tests, the full available test suite, `npm run lint --prefix ui`, `npm run build --prefix ui`, and the documented OpenSpec and plugin-example checks; record environment limits separately. Exact-head CI run `39013693234` passed the build, API unit tests (67), API E2E tests (3), AppHost tests (3), and UI checks (14 tests); run `39013693233` passed container checks. OpenSpec and ADR checks passed. The local AppHost run remains blocked because Docker is unhealthy; CI provided the AppHost execution result.

## Workflow follow-up

- [x] Obtain independent high-capability review of the exact proposal revision. Resolve blocking findings and re-review material changes. (Proposal PR #54 was independently reviewed and merged; no blocking findings remain.)
- [x] Obtain Michal's explicit design approval before implementation. This proposal does not authorize implementation, merge, deployment, or archive. (Michal authorized implementation with `piv 6`; implementation remains open.)
- This slice is manual GET/HEAD only. Scheduled checks and POST remain deferred and are not complete under this change.
- Keep issue #6 open; it remains open.
- Do not add plugin-specific agent tools or claim agent parity before the shared contract is approved and implemented.
- [ ] Coordinate future agent projection with #48; this remains an open follow-up.
