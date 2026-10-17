# Proposal

## Why

The manual HTTP endpoint slice now supports configured `GET` and `HEAD` checks. Operators also need periodic checks without caller action. Issue #61 defines this planning unit; it does not authorize runtime work.

## What Changes

- Define per-endpoint cron schedules and the global `HttpEndpoint__DefaultCron` fallback.
- Define schedule parsing, invalid-schedule behavior, UTC timing, lifecycle, reload, missed-run, overlap, and concurrency rules.
- Define how scheduled outcomes appear in status and why they do not create operation-history entries.
- Preserve configured-ID-only execution, existing request bounds, and URL privacy.
- Keep POST and response-body assertions out of scope.

## Capabilities

### Modified Capabilities

- `http-endpoint-checks`: Add optional scheduled checks for configured endpoints.

## Impact

- Modify the `http-endpoint-checks` proposal contract only.
- No runtime, test, CI, dependency, baseline-spec, deployment, or live-call changes are included in this planning unit.
- The proposal defines a process-local scheduler and in-memory latest scheduled outcomes. It does not promise durable scheduled history.
- Issue #62 owns POST behavior. This change does not authorize implementation; implementation needs separate explicit approval.

## Tracking

Issue #61 requests and tracks this proposal-only OpenSpec delta.
