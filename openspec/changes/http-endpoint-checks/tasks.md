# Tasks

## 1. Add configuration and endpoint validation

- [ ] 1.1 Add the `ControlPlane.HttpEndpoint` project, settings type, configuration binding, and solution/host references; verify `dotnet build src/ControlPlane.slnx` succeeds.
- [ ] 1.2 Validate endpoint IDs, unique names, absolute HTTP/HTTPS URLs, URL user information, GET/HEAD methods, expected status codes, and finite timeouts; verify unit tests reject invalid and duplicate definitions.
- [ ] 1.3 Add safe placeholder configuration documentation and verify the repository's configuration and public-artifact checks pass.

## 2. Implement manual endpoint checks

- [ ] 2.1 Add one injectable endpoint-check service that uses `IHttpClientFactory`, disables redirects, honors cancellation, and applies finite timeouts; verify handler tests cover timeout, cancellation, redirects, and response-body non-disclosure.
- [ ] 2.2 Add a named operation that accepts only a configured endpoint ID and returns safe status, duration, timestamp, and stable error codes; verify unknown IDs cause no request and outputs omit URLs, headers, bodies, and raw exceptions.
- [ ] 2.3 Add a runnable `.http` example with placeholder values and verify `python3 scripts/check_plugin_http_examples.py` passes.

## 3. Expose endpoint status

- [ ] 3.1 Add an `IStatusProvider` that uses the shared check service and maps expected responses to Healthy and failures or invalid configuration to Warning; verify healthy, failed, unavailable, and invalid-configuration tests.
- [ ] 3.2 Verify overview requests expose one safe status signal per configured endpoint and do not expose URLs or response data.

## 4. Verify the complete feature slice

- [ ] 4.1 Add a fabricated-endpoint end-to-end test that reads status, invokes the named operation, and verifies the safe execution-history result; verify it makes no live external requests.
- [ ] 4.2 Run `dotnet build src/ControlPlane.slnx`, focused tests, the full available test suite, `npm run lint --prefix ui`, `npm run build --prefix ui`, and the documented OpenSpec and plugin-example checks; record any environment limitation separately.

## Workflow follow-up

- Obtain independent high-capability review of the exact proposal revision. Resolve blocking findings and re-review material changes.
- Obtain Michal's explicit design approval before implementation. This proposal does not authorize implementation, merge, deployment, or archive.
- Keep issue #6 open. Confirm the proposed removal of scheduled checks and POST against its acceptance criteria; record any deferred behavior in a separately approved follow-up.
- After the #6 implementation slice is complete, continue with issue #9 as requested, subject to its own design, security, implementation, review, and approval gates.
- Coordinate future agent projection with #48. Do not add plugin-specific agent tools or claim agent parity before the shared contract is approved and implemented.
