# Tasks

## 1. Define and validate the proposal

- [ ] 1.1 Review schedule precedence, cron syntax, invalid-input behavior, UTC and daylight-saving rules, and first-run timing.
- [ ] 1.2 Review startup, shutdown, configuration reload, missed-run, overlap, concurrency, timeout, cancellation, and overview-scan interactions.
- [ ] 1.3 Review status freshness, scheduled operation-history exclusion, in-memory lifetime, privacy limits, and multi-replica behavior.
- [ ] 1.4 Validate all OpenSpec artifacts with the pinned OpenSpec CLI in strict mode.
- [ ] 1.5 Obtain independent high-capability review of the exact proposal revision and resolve blocking findings.
- [ ] 1.6 Obtain explicit human design approval before any implementation request.

## 2. Explicit exclusions

- [ ] No runtime or test-code changes in this proposal-only change.
- [ ] No CI changes, dependency changes, deployment, or live endpoint calls.
- [ ] Do not begin implementation without separate explicit authorization.
- [ ] Keep POST and response-body assertions out of scope; POST remains tracked by issue #62.
