# Spec Delta

## Purpose

Adds scheduled checks for configured HTTP endpoints while preserving manual checks and safe results.

This delta follows the manual-check slice. Its OpenSpec change is still active, so this delta uses only ADDED requirements. Before implementation, reconcile these requirements with the manual change's archived baseline. Apply scheduling only to endpoints with valid effective schedules; keep all other endpoints manual-only.

## ADDED Requirements

### Requirement: Resolve endpoint schedule precedence

A non-blank endpoint `cron` value SHALL override `HttpEndpoint__DefaultCron`. An absent, null, or blank endpoint value SHALL inherit the global default. No effective schedule SHALL mean manual-only.

#### Scenario: Endpoint schedule overrides the default
- **WHEN** an endpoint and the global setting both have valid schedules
- **THEN** the endpoint uses its own schedule

#### Scenario: Endpoint inherits the global schedule
- **WHEN** an endpoint schedule is absent or blank and the global schedule is valid
- **THEN** the endpoint uses the global schedule

#### Scenario: Endpoint has no effective schedule
- **WHEN** both schedule values are absent or blank
- **THEN** the endpoint remains manual-only

### Requirement: Define cron fields and operators

Schedules SHALL use five numeric fields: minute `0-59`, hour `0-23`, day `1-31`, month `1-12`, and weekday `0-6` where Sunday is `0`. Each field value SHALL use only ASCII decimal digits `0-9`. Support `*`, comma-separated lists, and ascending ranges. `*` SHALL be valid only as the whole field or as the base of `*/step`. Reject empty list items, names, aliases, seconds, years, macros, descending ranges, and mixed forms such as `*,5` or `1,*`.

#### Scenario: Parse supported fields
- **WHEN** the expression is `*/15 9-17 * * 1-5`
- **THEN** the system accepts it as a valid five-field schedule

#### Scenario: Reject names and aliases
- **WHEN** an expression uses names, aliases, seconds, years, macros, or descending ranges
- **THEN** the system rejects that schedule

#### Scenario: Reject signs and non-ASCII digits
- **WHEN** a value carries a leading sign such as `+1` or `-1`, or uses a non-ASCII digit
- **THEN** the system rejects that schedule

#### Scenario: Reject misplaced wildcard
- **WHEN** a list item combines `*` with another value, such as `*,5` or `1,*`
- **THEN** the system rejects that schedule

### Requirement: Parse cron steps and values

Steps SHALL be positive integers applied only to `*` or ascending ranges. A step on `*` SHALL select the field's minimum, the minimum plus the step, and each following value through the field's maximum. A step on `a-b` SHALL select `a`, `a` plus the step, and each following value through `b`. A step SHALL not exceed the field's or range's value count. Single-value steps SHALL be rejected. Leading zeros SHALL mean decimal values. Duplicate list values SHALL count once.

#### Scenario: Parse a valid range step
- **WHEN** a range has a positive step no greater than its value count
- **THEN** the system accepts the schedule

#### Scenario: Step expansion starts at the field minimum
- **WHEN** the day-of-month field is `*/2` or the weekday field is `*/2`
- **THEN** day-of-month selects 1, 3, 5, ..., 31 and weekday selects 0, 2, 4, 6

#### Scenario: Reject invalid steps
- **WHEN** a step is zero, exceeds its allowed count, or follows a single value
- **THEN** the system rejects the schedule

### Requirement: Parse cron whitespace

The parser SHALL trim outer ASCII spaces and separate fields with one or more ASCII spaces. It SHALL reject tabs and other whitespace.

#### Scenario: Parse ASCII spaces
- **WHEN** an expression has outer ASCII spaces or multiple ASCII spaces between fields
- **THEN** the system accepts the schedule if its fields are valid

#### Scenario: Reject unsupported whitespace
- **WHEN** an expression contains a tab or other unsupported whitespace
- **THEN** the system rejects that schedule

### Requirement: Resolve cron value types

A missing, null, or blank endpoint `cron` value SHALL be absent and eligible to inherit the global default. A present, non-null non-string value, including an empty object or array, SHALL be invalid and SHALL NOT inherit the global default. Where the provider would flatten an empty object or array to a blank-like value, the implementation SHALL inspect the raw JSON value kind.

#### Scenario: Missing, null, or blank schedule
- **WHEN** an endpoint `cron` value is missing, null, or blank and the global default is valid
- **THEN** the endpoint inherits the global default

#### Scenario: Reject non-string value
- **WHEN** an endpoint `cron` value is a JSON object, array, number, or boolean
- **THEN** the system rejects that endpoint's schedule and does not inherit the global default

#### Scenario: Empty object or array is invalid
- **WHEN** an endpoint `cron` value is an empty JSON object or array and the global default is valid
- **THEN** the system rejects that endpoint's schedule and does not inherit the global default

### Requirement: Match cron day fields

Each day field SHALL always contribute its selected value set; a plain `*` selects every value in its field, and each day field's selected values always filter candidate dates. If day-of-month or weekday begins with `*` (as `*` or `*/step`), both fields SHALL match. If neither field begins with `*`, either field SHALL match.

#### Scenario: Both day fields are restricted
- **WHEN** the expression is `0 9 1 * 1`
- **THEN** it matches Mondays and the first day of each month

#### Scenario: Day-of-month step with unrestricted weekday
- **WHEN** the expression is `0 0 */2 * *`
- **THEN** it matches only odd day-of-month values

#### Scenario: Weekday step with unrestricted day-of-month
- **WHEN** the expression is `0 0 * * */2`
- **THEN** it selects Sunday, Tuesday, Thursday, and Saturday

#### Scenario: Day-of-month step with a restricted weekday
- **WHEN** the expression is `0 9 */2 * 1`
- **THEN** it matches Mondays only when the day-of-month is odd

### Requirement: Reject schedules with no occurrence

A valid schedule SHALL have at least one matching instant within a complete 400-year Gregorian calendar cycle. The next occurrence SHALL be strictly later than the evaluation instant.

#### Scenario: Schedule has no valid date
- **WHEN** an expression can never match a calendar date, such as `0 0 30 2 *`
- **THEN** the system rejects that schedule

#### Scenario: Calculate the next occurrence
- **WHEN** the system calculates a future occurrence from an evaluation instant
- **THEN** the result is strictly later than that instant

### Requirement: Keep invalid schedules manual-only

An invalid endpoint schedule SHALL disable scheduling only for that endpoint and SHALL preserve its valid manual checks. An invalid global default SHALL disable scheduling only for valid endpoints that inherit it. Invalid schedules SHALL NOT fail startup. A valid endpoint override SHALL remain active when the global default is invalid.

#### Scenario: Endpoint schedule is invalid
- **WHEN** an otherwise valid endpoint has an invalid schedule
- **THEN** it remains available for manual checks and has no scheduled checks

#### Scenario: Global default is invalid
- **WHEN** the global default is invalid and a valid endpoint inherits it
- **THEN** that endpoint remains manual-only

#### Scenario: Valid override and invalid default
- **WHEN** an endpoint has a valid override and the global default is invalid
- **THEN** the endpoint uses its own schedule

### Requirement: Report invalid schedule warnings

An invalid endpoint schedule SHALL produce one safe `Warning` signal with code `invalid_cron` and ID `http-endpoint-config-cron-{index}`. An invalid global default SHALL produce one safe `Warning` signal with code `invalid_cron` and ID `http-endpoint-config-default-cron` only when a valid endpoint inherits it. The two warning cases SHALL share the code `invalid_cron` and SHALL be distinguished by their IDs. Valid schedules SHALL NOT produce the current schedule-ignored warning.

#### Scenario: Endpoint schedule warning is emitted
- **WHEN** a valid endpoint entry has invalid `cron` syntax
- **THEN** the system emits one fixed warning for its array index without its URL, name, or schedule text

#### Scenario: Unused invalid default does not warn
- **WHEN** the global default is invalid but no valid endpoint inherits it
- **THEN** the system emits no warning for that unused setting

#### Scenario: Entry has another validation error
- **WHEN** an endpoint entry is already excluded for a non-schedule validation error
- **THEN** the system emits only its existing configuration warning

### Requirement: Start scheduled checks in UTC

The scheduler SHALL run in the API process and interpret schedules in UTC. It SHALL register the first occurrence strictly after startup, SHALL NOT run an immediate startup check, and SHALL never send catch-up requests on restart.

#### Scenario: Scheduler starts
- **WHEN** the API starts with one or more valid effective schedules
- **THEN** each endpoint's next future UTC occurrence is registered
- **AND** no immediate request is sent

### Requirement: Apply configuration and shutdown rules

The scheduler SHALL load configuration once at startup. Configuration changes SHALL require an API restart. Application shutdown SHALL stop future triggers and cancel in-flight scheduled checks.

#### Scenario: Configuration changes while active
- **WHEN** endpoint configuration changes while the API runs
- **THEN** active schedules remain unchanged until restart

#### Scenario: Application shuts down
- **WHEN** application shutdown begins
- **THEN** the scheduler starts no further checks and cancels its in-flight requests

### Requirement: Handle late and missed occurrences

An instant equal to `now` SHALL count as due. When exactly one due instant is at or before `now` and the endpoint is not busy and a shared request slot is available, the scheduler SHALL run exactly one check. When two or more due instants are at or before `now`, the scheduler SHALL skip all elapsed instants without sending a request. A due occurrence consumed while the endpoint is busy or no shared request slot is available SHALL be skipped and SHALL NOT be queued.

#### Scenario: Scheduler wakes late for exactly one occurrence
- **WHEN** exactly one due instant is at or before `now` and the endpoint is not busy and a shared request slot is available
- **THEN** the scheduler sends exactly one request for that instant

#### Scenario: Due instant equals the current time
- **WHEN** a due instant equals `now`
- **THEN** the scheduler treats it as due
- **AND** it sends no request for that instant unless the endpoint is not busy and a shared request slot is available

#### Scenario: Scheduler wakes after several occurrences
- **WHEN** two or more due instants are at or before `now`
- **THEN** the scheduler skips all elapsed instants, sends zero requests for them, and selects the next future instant

#### Scenario: Restart after several elapsed occurrences
- **WHEN** the API restarts with one or more due instants at or before `now`
- **THEN** the scheduler sends zero catch-up requests and selects the next future instant

#### Scenario: Endpoint or capacity is busy
- **WHEN** exactly one due instant is at or before `now` and the endpoint is busy or no shared request slot is available
- **THEN** the scheduler consumes and skips that occurrence, sends zero requests, and does not queue it

### Requirement: Handle system clock changes

The scheduler SHALL use the API process's UTC wall clock. A forward clock jump SHALL apply the late-occurrence rule. A backward jump SHALL NOT run a consumed due instant twice.

#### Scenario: Clock moves backward after a scheduled run
- **WHEN** the UTC clock moves backward after an occurrence was consumed
- **THEN** the scheduler does not run that occurrence again

### Requirement: Bound endpoint overlap

The system SHALL allow at most one check for each endpoint to hold that endpoint's gate across scheduled checks, overview scans, and manual operations. It SHALL acquire the per-endpoint gate before the shared request gate. An endpoint is `endpoint busy` when a check holds or awaits its gate; scheduled checks SHALL skip when the endpoint is busy.

#### Scenario: Scheduled endpoint is busy
- **WHEN** a check holds or awaits an endpoint's gate at that endpoint's scheduled instant
- **THEN** the scheduler skips that occurrence

### Requirement: Bound shared request concurrency

An `active request` is a check that holds a shared request slot and counts toward the cap. The system SHALL cap active requests at 20 per API process. Scheduled checks SHALL skip, not wait or queue, when no shared request slot is available immediately.

#### Scenario: Shared capacity is full
- **WHEN** all 20 shared request slots are held at a scheduled instant
- **THEN** the scheduler skips that occurrence without queuing it

### Requirement: Bound scheduled check time

A scheduled check SHALL use the configured endpoint timeout. On timeout, it SHALL cancel the request and record the safe timeout outcome. Shutdown cancellation SHALL NOT replace the latest outcome.

#### Scenario: Scheduled check reaches its timeout
- **WHEN** a scheduled request exceeds its endpoint timeout
- **THEN** the system cancels it and records a safe timeout outcome

### Requirement: Bound manual and overview capacity waits

Manual and overview checks SHALL wait for endpoint capacity only within their existing deadlines. An endpoint timeout SHALL start when the check is requested and include capacity wait and HTTP time. If a manual operation times out before it gets a slot, it SHALL send no request and SHALL return the safe `endpoint_timeout` result. The overview scan's 12-second budget SHALL also bound its waits.

#### Scenario: Manual operation times out while waiting
- **WHEN** capacity stays full until the endpoint timeout expires
- **THEN** the operation sends no request and returns `endpoint_timeout`

#### Scenario: Overview scan exhausts its budget
- **WHEN** endpoint work remains incomplete at the 12-second scan budget
- **THEN** the system cancels unfinished work and returns safe timeout outcomes

### Requirement: Preserve manual-only overview behavior

Overview status SHALL use the latest scheduled outcome for endpoints with valid effective schedules and SHALL NOT probe those endpoints. Endpoints without effective schedules SHALL keep the existing overview scan, 15-second cache, in-flight sharing, and 12-second budget. Cancellation SHALL stop only the requesting caller's wait, not the shared scan. Scheduled checks SHALL NOT join or populate the overview cache.

#### Scenario: Overview has scheduled and manual-only endpoints
- **WHEN** overview status reads a mixed configuration
- **THEN** scheduled endpoints use their latest scheduled outcomes
- **AND** manual-only endpoints use the overview scan

### Requirement: Show scheduled endpoint status

Before an endpoint's first scheduled check completes, overview status SHALL expose an `Unknown` signal timestamped at the status-read time. Later signals SHALL map expected status to `Healthy`, unexpected status to `Warning`, and timeout or transport failure to `Critical`. Each signal SHALL retain the check's observation time.

#### Scenario: No scheduled outcome exists
- **WHEN** overview reads a scheduled endpoint before its first completed check
- **THEN** it reports `Unknown` without sending a request

#### Scenario: Scheduled outcome exists
- **WHEN** overview reads an endpoint with a completed scheduled check
- **THEN** it reports the mapped outcome and its observation time without sending a request

### Requirement: Define scheduled status freshness

The system SHALL keep the latest completed scheduled outcome until another scheduled check completes. It SHALL NOT mark the outcome stale by age. A manual check SHALL NOT replace scheduled status. Shutdown cancellation SHALL NOT replace the last outcome with a timeout. Restart SHALL clear all in-memory scheduled outcomes.

#### Scenario: Scheduled status ages
- **WHEN** no new scheduled check completes after a prior result
- **THEN** overview retains that result and its original observation time

#### Scenario: Manual check or shutdown occurs
- **WHEN** a manual check runs or shutdown cancels scheduled work
- **THEN** scheduled overview status remains unchanged

### Requirement: Keep scheduled checks out of operation history

Scheduled checks SHALL NOT create operation-execution history entries. The system SHALL retain only each endpoint's latest scheduled outcome in process memory. Manual operations SHALL continue to use the existing operation-history path.

#### Scenario: Scheduled check completes
- **WHEN** a scheduled check succeeds or fails
- **THEN** the latest in-memory status is updated without an operation-history entry

### Requirement: Preserve configured scheduled requests

Scheduled checks SHALL use only configured `GET` or `HEAD` methods. They SHALL accept no caller-provided URL, method, headers, or body. They SHALL send at most one request, follow no redirects, retry none, and read no response content.

#### Scenario: Scheduled endpoint redirects or returns a body
- **WHEN** a scheduled endpoint returns a redirect or response body
- **THEN** the system does not follow the redirect or read the body
- **AND** it compares the status with the configured expected status

### Requirement: Preserve scheduled-check privacy bounds

Results, status, logs, and traces SHALL NOT contain URLs, query values, response headers, response data, or raw exception text. The named client's framework request logs SHALL remain suppressed, and its trace data SHALL continue to suppress or redact `url.full`. Metrics SHALL NOT use endpoint-specific labels.

#### Scenario: Scheduled check emits observability data
- **WHEN** a scheduled check emits logs, traces, metrics, or status
- **THEN** the data contains no URL, query value, response header, response data, or raw exception text
- **AND** the named client's request logs remain suppressed and its trace data suppresses or redacts `url.full`
- **AND** metrics contain no endpoint-specific labels
