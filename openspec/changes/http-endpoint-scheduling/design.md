# Design

## Context

The merged manual slice is in `src/ControlPlane.HttpEndpoint`. It loads `HttpEndpoint__DefaultCron` and per-entry `cron` fields only to warn that schedules are ignored. `HttpEndpointChecker` enforces one request, a per-endpoint timeout, disabled redirects, and safe outcomes. `HttpEndpointScanCoordinator` shares and caches overview scans. The status provider reads that scan; the named operation performs a fresh check and persists its safe result through the workbench.

The repository uses optional plugin service registration under ADR-0002. ADR-0004 requires end-to-end coverage through Aspire for implementation. This proposal adds no code and does not change baseline specs.

## Goals / Non-Goals

**Goals:**

- Schedule configured endpoints without accepting caller-provided URLs or methods.
- Make each schedule decision deterministic and testable.
- Bound scheduler work and preserve the manual check's privacy and request limits.
- Expose the latest scheduled outcome in status without pretending it is durable history.

**Non-Goals:**

- POST, request bodies, custom headers, response-body assertions, or caller-selected destinations.
- Distributed scheduling, leader election, durable queues, catch-up execution, or durable scheduled history.
- Hot reload, configurable time zones, or per-endpoint time-zone rules.
- Changes to authentication, authorization, confirmation, or general operation auditing.

## Decisions

### Resolve each endpoint's effective schedule

A non-blank endpoint `cron` value overrides `HttpEndpoint__DefaultCron`. An absent, null, or blank endpoint value inherits the global default. If the resulting value is absent or blank, that endpoint remains manual-only. Schedule data never changes the configured HTTP method or target URL.

Parse five numeric cron fields: minute `0-59`, hour `0-23`, day-of-month `1-31`, month `1-12`, and day-of-week `0-6` where Sunday is `0`. Support `*`, comma-separated values, ascending ranges, and positive `/step` values on a wildcard or range. Reject names, aliases, seconds, years, macros, descending ranges, and out-of-range values. If both day-of-month and day-of-week are restricted, a match in either field satisfies that day portion; otherwise the unrestricted field does not limit the restricted field. Require the next occurrence to be strictly after the evaluation instant.

An invalid per-endpoint schedule disables scheduling for that endpoint but does not remove it from manual checks. Report one safe configuration warning for that entry. An invalid global default disables scheduling only for entries that inherit it and reports one safe warning for the global setting. A valid explicit endpoint schedule remains usable when the global default is invalid. Invalid schedules do not fail host startup.

### Use UTC and process-local scheduling

Interpret all expressions in UTC. This removes daylight-saving gaps and repeated local times; no local time-zone setting exists. Start a hosted scheduler with the API process. The scheduler registers the next matching UTC instant after startup; it does not run an immediate startup check. Stop its timer and cancel its in-flight scheduled requests when application shutdown begins.

Load configuration once at startup, matching the existing configuration snapshot. Configuration changes take effect after an API restart. Do not add file watching or hot reload.

### Do not replay missed occurrences

If the process is stopped, blocked, or overloaded at a scheduled instant, do not queue or replay that occurrence. Calculate the next future occurrence. Preserve the last completed scheduled outcome and its observation time until a later scheduled check completes.

### Bound overlap and coordinate checks

Allow at most one scheduled check per endpoint at a time. If that endpoint is still busy at its next occurrence, skip the new occurrence; do not queue it. Cap concurrent scheduled checks at 20 per API process. Apply the existing per-endpoint timeout and cancellation behavior to each scheduled request.

Use one process-wide concurrency gate for scheduled checks, overview scans, and manual operations, capped at 20 active endpoint requests. Overview scans keep their existing in-flight sharing, 15-second cache, and 12-second overall budget. Scheduled checks do not join or populate the overview cache. Manual operations still request a fresh check. When a scheduled occurrence cannot acquire capacity immediately, skip it. A caller cancellation cancels only that caller's wait; application shutdown cancels scheduled work.

### Publish status, not operation history

For an endpoint with an effective schedule, overview status uses its latest completed scheduled outcome and does not trigger an extra overview request for that endpoint. Before its first completed scheduled check, expose an `Unknown` status signal. After a check, map expected status to `Healthy`, unexpected status to `Warning`, and timeout or transport failure to `Critical`. Keep the outcome's observation timestamp so status does not appear newer than the check.

Manual operation calls remain fresh checks and continue to create the existing safe operation-history entry. Scheduled checks do not invoke the operation or create operation-history entries. Keep only the latest scheduled outcome in process memory; restart clears it. Do not promise a durable run history or add a database schema.

### Preserve privacy and observability limits

Do not add URL, query, response-body, response-header, or raw-exception data to logs, traces, metrics, status, or persisted operation results. Keep the current named-client log and URL-trace suppression. Do not add endpoint-specific metric labels. Status may use the configured endpoint ID as it does for manual checks; it must not expose the URL or display name.

## Risks / Trade-offs

- A process-local scheduler runs once per API process. Multiple API replicas therefore check the same endpoint independently; distributed coordination is out of scope and must be stated in operator guidance.
- UTC-only expressions avoid daylight-saving ambiguity but may not match an operator's local wall clock.
- In-memory status is lost on restart, and no scheduled run history is retained. Consumers must use the observation timestamp to assess freshness.
- Skipped occurrences do not catch up. This prevents a restart or overload from causing a burst of endpoint calls.
- A shared concurrency gate can delay manual and overview checks while scheduled work runs. The 20-request cap and request timeouts bound that effect.

## Migration Plan

No migration or configuration key is required. Existing `cron` and `HttpEndpoint__DefaultCron` values become active only after a separately approved implementation. Empty values continue to mean manual-only. This proposal makes no deployment or live endpoint calls.
