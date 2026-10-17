# Design

## Context

The merged manual slice is in `src/ControlPlane.HttpEndpoint`. It loads `HttpEndpoint__DefaultCron` and per-entry `cron` fields only to warn that schedules are ignored. `HttpEndpointChecker` enforces one request, a per-endpoint timeout, disabled redirects, and safe outcomes. `HttpEndpointScanCoordinator` shares and caches overview scans. The status provider reads that scan; the named operation performs a fresh check and persists its safe result through the workbench.

The repository uses optional plugin service registration under ADR-0002. ADR-0004 requires end-to-end coverage through Aspire for implementation. This proposal adds no code and does not change baseline specs.

## Dependency on the manual-check OpenSpec change

`openspec/specs/http-endpoint-checks/spec.md` does not exist because the manual-check change remains unarchived. This proposal therefore uses only ADDED requirements and does not claim that pending manual requirements are baseline behavior. Before any implementation, first reconcile and archive the manual-check change into the baseline. Then update its overview/status requirements to apply to manual-only endpoints, remove its temporary schedule-ignored requirement, and make this delta's scheduled-endpoint requirements explicit modifications. Do not implement or archive this proposal while those requirements conflict. This sequence does not authorize runtime work.

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

Parse five numeric cron fields: minute `0-59`, hour `0-23`, day-of-month `1-31`, month `1-12`, and day-of-week `0-6` where Sunday is `0`. Support `*`, comma-separated values, ascending ranges, and positive `/step` values on `*` or ranges. Reject names, aliases, seconds, years, macros, descending ranges, empty list items, and single-value steps. Treat leading zeros as decimal and duplicate list values as one value. Require each step to be no greater than its field's value count; for a range, use the range's value count. Trim outer ASCII spaces and separate fields with one or more ASCII spaces; reject tabs and other whitespace. A present non-string `cron` value is invalid.

If day-of-month and day-of-week are both restricted, a match in either field satisfies that day portion. Treat `*` and `*/step` as unrestricted for this rule. Otherwise, the restricted field controls the day match. Reject expressions with no matching instant in a complete 400-year Gregorian cycle. Require the next occurrence to be strictly after the evaluation instant.

Validate schedules only after the rest of an endpoint entry is valid and its ID is unique. An invalid endpoint schedule disables scheduling for that endpoint but does not remove it from manual checks. Report one safe `Warning` signal for that entry with code `invalid_cron` and ID `http-endpoint-config-cron-{index}`. Do not add a second schedule warning for an entry already excluded for another validation error.

An invalid global default disables scheduling only for entries that inherit it. Report one safe `Warning` signal for the global setting only when at least one valid endpoint inherits it; do not warn when no endpoint uses it. A valid explicit endpoint schedule remains usable when the global default is invalid. Invalid schedules do not fail host startup. Do not emit the current schedule-ignored warning for valid schedules.

For an invalid endpoint schedule, use title `Invalid HTTP endpoint schedule` and description `This endpoint remains available for manual checks; scheduled checks are disabled.` For an invalid inherited default, use ID `http-endpoint-config-default-cron`, title `Invalid default HTTP endpoint schedule`, and description `Endpoints that inherit this default remain manual-only.` Do not include schedule text, endpoint URLs, names, or exception details.

### Use UTC and process-local scheduling

Interpret all expressions in UTC. This removes daylight-saving gaps and repeated local times; no local time-zone setting exists. Start a hosted scheduler with the API process. The scheduler registers the next matching UTC instant after startup; it does not run an immediate startup check. Stop its timer and cancel its in-flight scheduled requests when application shutdown begins.

Load configuration once at startup, matching the existing configuration snapshot. Configuration changes take effect after an API restart. Do not add file watching or hot reload.

### Do not replay missed occurrences

Track the next due UTC instant for each endpoint. If the scheduler wakes after one due instant but before a later occurrence is due, it may run once for that due instant. If two or more occurrences have elapsed, skip all elapsed occurrences and calculate the next future one. If the endpoint or shared capacity is busy at a due instant, consume and skip that occurrence without queuing it.

Use the API process's UTC wall clock. A forward clock jump skips elapsed occurrences under the same rule. A backward jump SHALL NOT run an already-consumed due instant twice; the scheduler waits until the clock reaches the next unconsumed instant.

### Bound overlap and coordinate checks

Allow at most one active check for a configured endpoint across scheduled checks, overview scans, and manual operations. Use one process-wide gate for at most 20 active endpoint requests. Scheduled checks skip an occurrence if its endpoint is active or the shared gate has no slot immediately; do not queue scheduled work.

Overview and manual checks may wait for endpoint capacity. Start each endpoint timeout when that check is requested; include both capacity wait and HTTP time in the timeout. If a manual operation times out before it gets a slot, send no request and return the existing safe `endpoint_timeout` result. The workbench persists the manual result through its existing operation-history path. Overview waits remain bounded by the existing 12-second scan budget; unfinished checks map to safe `endpoint_timeout` outcomes. The endpoint timeout and scan budget use the earlier deadline.

Keep one shared in-flight overview scan, its 15-second result cache, and caller cancellation that stops only that caller's wait. Scheduled checks do not join or populate the overview cache. Application shutdown cancels scheduled work and waiting overview scans.


### Publish status, not operation history

For an endpoint with a valid effective schedule, overview status uses its latest completed scheduled outcome and does not probe that endpoint. Endpoints with no effective schedule remain manual-only and continue to use the existing overview scan behavior. A manual check is fresh but does not replace the scheduled endpoint's overview result.

Before the first completed scheduled check, expose an `Unknown` signal with the current status-read time. After a check, map expected status to `Healthy`, unexpected status to `Warning`, and timeout or transport failure to `Critical`. Preserve the check's observation timestamp. Keep the last completed status unchanged until another scheduled check completes; do not mark it stale by age. The old schedule-ignored warning is removed for valid schedules; invalid schedules use the fixed warnings defined above.

A shutdown cancellation does not replace the last scheduled result with a timeout. Scheduled checks do not invoke the manual operation or create operation-history entries. Keep only the latest scheduled outcome in process memory; restart clears it. Do not promise durable scheduled history or add a database schema.

### Preserve privacy and observability limits

Do not add URL, query, response-body, response-header, or raw-exception data to logs, traces, metrics, status, or persisted operation results. Keep the current named-client log and URL-trace suppression. Do not add endpoint-specific metric labels. Status may use the configured endpoint ID as it does for manual checks; it must not expose the URL or display name.

## Risks / Trade-offs

- A process-local scheduler runs once per API process. Multiple API replicas therefore check the same endpoint independently; distributed coordination is out of scope and must be stated in operator guidance.
- UTC-only expressions avoid daylight-saving ambiguity but may not match an operator's local wall clock.
- In-memory status is lost on restart, and no scheduled run history is retained. Consumers must use the observation timestamp to assess freshness.
- Skipped occurrences do not catch up and are not exposed as status, logs, traces, or metrics. This avoids a restart or overload causing a burst of endpoint calls.
- A shared concurrency gate can delay manual and overview checks while scheduled work runs. The 20-request cap and request timeouts bound that effect.

## Migration Plan

No migration or configuration key is required. Existing `cron` and `HttpEndpoint__DefaultCron` values become active only after a separately approved implementation. Empty values continue to mean manual-only. This proposal makes no deployment or live endpoint calls.
