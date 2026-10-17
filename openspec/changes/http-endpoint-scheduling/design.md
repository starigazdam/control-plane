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

Parse five numeric cron fields: minute `0-59`, hour `0-23`, day-of-month `1-31`, month `1-12`, and day-of-week `0-6` where Sunday is `0`. Support `*`, comma-separated values, ascending ranges, and positive `/step` values on `*` or ranges. Reject names, aliases, seconds, years, macros, descending ranges, empty list items, and single-value steps. Treat leading zeros as decimal and duplicate list values as one value. Accept only ASCII decimal digits `0-9`; reject signs such as `+1` or `-1` and non-ASCII digits. Require each step to be no greater than its field's value count; for a range, use the range's value count. Expand a `*/step` selection from the field's minimum, adding the step until the field's maximum is passed; expand an `a-b/step` selection from `a`, adding the step until `b` is passed. Thus day-of-month `*/2` selects 1, 3, 5, ..., 31, and weekday `*/2` selects 0, 2, 4, 6. Treat `*` as valid only as the whole field or as the base of `*/step`; reject forms such as `*,5` or `1,*`. Trim outer ASCII spaces and separate fields with one or more ASCII spaces; reject tabs and other whitespace. Treat a missing, null, or blank endpoint `cron` value as absent so it can inherit the global default. Treat a present, non-null endpoint `cron` value that is not a JSON string, including an object or array, as invalid rather than absent, so it never inherits the global default. An empty JSON object or array is therefore invalid and does not inherit the global default. Because the normal configuration provider may flatten empty objects or arrays into a blank-like value, the configuration-reading implementation must preserve or inspect the raw JSON value kind. Missing, null, and blank string values remain absent and eligible for inheritance.

Each day field always contributes its selected value set, and a plain `*` selects every value in its field, so a stepped wildcard does not remove its field's value set; each day field's selected values always filter candidate dates. If day-of-month or day-of-week begins with `*` (as `*` or `*/step`), require both fields to match (`0 0 */2 * *` matches only odd day-of-month values; `0 0 * * */2` matches Sunday, Tuesday, Thursday, and Saturday; `0 9 */2 * 1` matches Mondays only when the day-of-month is odd). If neither begins with `*`, require either field to match (`0 9 1 * 1` matches Mondays and the first of the month). Reject expressions with no matching instant in a complete 400-year Gregorian cycle. Require the next occurrence to be strictly after the evaluation instant.

Validate schedules only after the rest of an endpoint entry is valid and its ID is unique. An invalid endpoint schedule disables scheduling for that endpoint but does not remove it from manual checks. Report one safe `Warning` signal for that entry with code `invalid_cron` and ID `http-endpoint-config-cron-{index}`. Do not add a second schedule warning for an entry already excluded for another validation error.

An invalid global default disables scheduling only for entries that inherit it. Report one safe `Warning` signal for the global setting with code `invalid_cron` and ID `http-endpoint-config-default-cron` only when at least one valid endpoint inherits it; do not warn when no endpoint uses it. Both warning cases use code `invalid_cron` and are distinguished by their IDs. A valid explicit endpoint schedule remains usable when the global default is invalid. Invalid schedules do not fail host startup. Do not emit the current schedule-ignored warning for valid schedules.

For an invalid endpoint schedule, use title `Invalid HTTP endpoint schedule` and description `This endpoint remains available for manual checks; scheduled checks are disabled.` For an invalid inherited default, use ID `http-endpoint-config-default-cron`, title `Invalid default HTTP endpoint schedule`, and description `Endpoints that inherit this default remain manual-only.` Do not include schedule text, endpoint URLs, names, or exception details.

### Use UTC and process-local scheduling

Interpret all expressions in UTC. This removes daylight-saving gaps and repeated local times; no local time-zone setting exists. Start a hosted scheduler with the API process. The scheduler registers the next matching UTC instant after startup; it does not run an immediate startup check. Stop its timer and cancel its in-flight scheduled requests when application shutdown begins.

Load configuration once at startup, matching the existing configuration snapshot. Configuration changes take effect after an API restart. Do not add file watching or hot reload.

### Do not replay missed occurrences

Track the next due UTC instant for each endpoint. An instant equal to `now` counts as due. When exactly one due instant is at or before `now` and the endpoint is not busy and a shared request slot is available, run exactly one check. When two or more due instants are at or before `now`, skip all elapsed instants, send no request for them, and calculate the next future one. Restart never sends catch-up requests: startup registers the next future instant and sends no immediate request. If the endpoint is busy or no shared request slot is available at a due instant, consume and skip that occurrence without queuing it or sending a request.

Use the API process's UTC wall clock. A forward clock jump skips elapsed occurrences under the same rule. A backward jump SHALL NOT run an already-consumed due instant twice; the scheduler waits until the clock reaches the next unconsumed instant.

### Bound overlap and coordinate checks

Allow at most one check for a configured endpoint to hold that endpoint's gate across scheduled checks, overview scans, and manual operations. Acquire the per-endpoint gate first, then the shared request gate. An endpoint is `endpoint busy` while a check holds or awaits its gate; scheduled checks skip an occurrence when the endpoint is busy. An `active request` is a check that holds a shared request slot and counts toward the process-wide cap of 20. Use one process-wide gate for at most 20 active requests. Scheduled checks skip an occurrence when the endpoint is busy or no shared request slot is available immediately; do not queue scheduled work.

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
