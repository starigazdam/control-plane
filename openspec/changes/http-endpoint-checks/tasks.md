# Tasks

## 1. Add configuration and endpoint validation

- [ ] 1.1 Add the `ControlPlane.HttpEndpoint` project, settings type, configuration binding, solution and host references; verify `dotnet build src/ControlPlane.slnx` succeeds.
- [ ] 1.2 Add the empty `HttpEndpoint__ConfigPath` placeholder to `.env`; resolve relative paths from the API content root; verify unset, missing, unreadable, and malformed configuration behavior.
- [ ] 1.3 Validate endpoint IDs, duplicate IDs, URLs, methods, expected status codes, timeout values, and the 20-endpoint limit; verify invalid entries are excluded and reported without URLs.

## 2. Implement manual endpoint checks

- [ ] 2.1 Add one injectable checker using a named `IHttpClientFactory` client; disable retries, circuit breaking, redirects, URL-bearing logs, and URL trace attributes; use `ResponseHeadersRead`; verify these behaviors with handler and captured-log/activity tests.
- [ ] 2.2 Add operation ID `http-endpoint-check`; accept only a configured endpoint ID and return safe summaries with stable error codes; verify unknown IDs are rejected and cause no request.
- [ ] 2.3 Add a runnable `.http` example with placeholder values; verify `python3 scripts/check_plugin_http_examples.py` passes.
- [ ] 2.4 Use a real local HTTP server to verify redirects are not followed, timeout/cancellation works, a 503 response causes no retry, and response bodies remain unread.

## 3. Expose endpoint status

- [ ] 3.1 Add an `IStatusProvider` that maps healthy, degraded, unavailable, invalid, and disabled states to the defined `StatusLevel` values; verify status mapping and safe signals.
- [ ] 3.2 Add a shared in-flight overview scan, a 15-second result cache, and a 12-second scan budget; verify concurrent callers share a scan and stalled endpoints do not exceed the budget.

## 4. Verify the complete feature slice

- [ ] 4.1 Add an Aspire-backed E2E test that starts a local fabricated HTTP endpoint, reads overview status, invokes the operation, and verifies safe execution history; verify no live external requests occur.
- [ ] 4.2 Run `dotnet build src/ControlPlane.slnx`, focused tests, the full available test suite, `npm run lint --prefix ui`, `npm run build --prefix ui`, and the documented OpenSpec and plugin-example checks; record environment limits separately.

## Workflow follow-up

- Obtain independent high-capability review of the exact proposal revision. Resolve blocking findings and re-review material changes.
- Obtain Michal's explicit design approval before implementation. This proposal does not authorize implementation, merge, deployment, or archive.
- Keep issue #6 open. Scheduled checks and POST are deferred to separate follow-up issues and are not complete under this change.
- Coordinate future agent projection with #48. Do not add plugin-specific agent tools or claim agent parity before the shared contract is approved and implemented.
