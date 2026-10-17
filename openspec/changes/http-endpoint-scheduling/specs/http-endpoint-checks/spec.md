# Spec Delta

## Purpose

Adds bounded scheduled checks for configured HTTP endpoints while preserving manual checks and safe result handling.

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

### Requirement: Validate cron schedule syntax

Schedules SHALL use five numeric fields: minute `0-59`, hour `0-23`, day-of-month `1-31`, month `1-12`, and day-of-week `0-6` where Sunday is `0`. The parser SHALL support `*`, lists, ascending ranges, and positive steps on a wildcard or range. It SHALL reject names, aliases, seconds, years, macros, descending ranges, and out-of-range values.

#### Scenario: Cron expression uses supported syntax
- **WHEN** a five-field numeric expression uses supported values and operators
- **THEN** the system accepts it

#### Scenario: Cron expression uses unsupported syntax
- **WHEN** an expression uses an unsupported field, operator, or value
- **THEN** the system rejects that schedule

### Requirement: Match cron calendar fields

If day-of-month and day-of-week are both restricted, either field may match the day. Otherwise, the restricted field controls the day match. A next occurrence SHALL be strictly later than the evaluation instant.

#### Scenario: Both day fields are restricted
- **WHEN** day-of-month and day-of-week are both restricted
- **THEN** a date matches when either field matches

#### Scenario: Calculate the next occurrence
- **WHEN** the system calculates an occurrence from an evaluation instant
- **THEN** the result is strictly later than that instant

### Requirement: Handle invalid schedules without disabling manual checks

An invalid endpoint schedule SHALL disable scheduling only for that endpoint. An invalid global default SHALL disable scheduling only for endpoints that inherit it. Neither error SHALL fail startup. The system SHALL report one safe warning for each invalid endpoint schedule and one for an invalid global default. A valid endpoint override SHALL remain active when the global default is invalid.

#### Scenario: Invalid endpoint schedule
- **WHEN** an endpoint schedule is invalid
- **THEN** the endpoint stays available for valid manual checks
- **AND** the system reports one safe warning for its configuration entry

#### Scenario: Invalid inherited default
- **WHEN** the global default is invalid and an endpoint has no valid override
- **THEN** the endpoint remains manual-only
- **AND** the system reports one safe warning for the global setting

#### Scenario: Valid override with invalid default
- **WHEN** an endpoint has a valid schedule and the global default is invalid
- **THEN** the endpoint uses its own schedule

### Requirement: Start scheduled checks in UTC

The scheduler SHALL run in the API process. It SHALL interpret schedules in UTC and SHALL run the first check at the first matching instant strictly after startup. It SHALL NOT run an immediate startup check.

#### Scenario: Scheduler starts
- **WHEN** the API starts with valid effective schedules
- **THEN** each endpoint's next future UTC occurrence is registered
- **AND** no immediate request is sent

### Requirement: Apply configuration and shutdown rules

The scheduler SHALL load configuration once at startup. Configuration changes SHALL require an API restart. Application shutdown SHALL stop future triggers and cancel in-flight scheduled checks.

#### Scenario: Configuration changes while active
- **WHEN** endpoint configuration changes while the API runs
- **THEN** the active schedules remain unchanged until restart

#### Scenario: Application shuts down
- **WHEN** application shutdown begins
- **THEN** the scheduler starts no further checks and cancels its in-flight requests

### Requirement: Do not replay missed occurrences

The scheduler SHALL NOT queue or replay occurrences missed while stopped or busy. After a missed or skipped occurrence, it SHALL calculate the next future occurrence.

#### Scenario: Process misses an occurrence
- **WHEN** the API is stopped or busy at a scheduled instant
- **THEN** the system skips that occurrence and does not replay it later

### Requirement: Limit scheduled overlap and concurrency

The system SHALL allow at most one scheduled check per endpoint at a time. It SHALL cap active endpoint requests across scheduled checks, overview scans, and manual operations at 20 per API process. A scheduled occurrence SHALL be skipped, not queued, when the endpoint is active or shared capacity is unavailable.

#### Scenario: Same endpoint is still active
- **WHEN** a scheduled check is active at the endpoint's next occurrence
- **THEN** the system skips the new occurrence without queuing it

#### Scenario: Shared capacity is full
- **WHEN** all 20 shared request slots are active at an occurrence
- **THEN** the system skips that occurrence without queuing it

### Requirement: Preserve check timeout and overview coordination

Scheduled checks SHALL use the configured endpoint timeout and shared request-capacity gate. They SHALL NOT join or populate the overview cache. Overview scans SHALL retain their in-flight sharing, 15-second cache, 12-second budget, and caller-cancellation behavior. Manual operations SHALL continue to perform fresh checks.

#### Scenario: Scheduled check reaches its timeout
- **WHEN** a scheduled request exceeds its endpoint timeout
- **THEN** the system cancels it and records a safe timeout outcome

#### Scenario: Overview scan overlaps a scheduled check
- **WHEN** both need endpoint capacity at the same time
- **THEN** both use the shared 20-request limit
- **AND** the scheduled result does not alter the overview cache

### Requirement: Show latest scheduled results in status

For a scheduled endpoint, overview status SHALL use its latest completed scheduled outcome and SHALL NOT trigger another request for that endpoint. Before the first completed check, status SHALL be `Unknown`. Map expected status to `Healthy`, unexpected status to `Warning`, and timeout or transport failure to `Critical`. Keep the outcome observation time.

#### Scenario: No scheduled result exists
- **WHEN** overview status reads an endpoint before its first scheduled check completes
- **THEN** it reports `Unknown` and sends no request for that endpoint

#### Scenario: A scheduled result exists
- **WHEN** overview status reads an endpoint with a completed scheduled check
- **THEN** it reports the mapped result and its observation time without another request

### Requirement: Keep scheduled outcomes out of operation history

Scheduled checks SHALL NOT create operation-execution history entries. The system SHALL retain only each endpoint's latest scheduled outcome in process memory and SHALL clear it on restart. Manual operations SHALL continue to use the existing operation-history path.

#### Scenario: Scheduled check completes
- **WHEN** a scheduled check succeeds or fails
- **THEN** the latest in-memory status is updated without an operation-history entry

#### Scenario: API restarts
- **WHEN** the API restarts
- **THEN** prior scheduled outcomes are cleared and the system waits for the next occurrence

### Requirement: Keep scheduled requests bounded and private

Scheduled checks SHALL use only configured `GET` or `HEAD` methods. They SHALL accept no caller-provided URL, method, headers, or body. They SHALL send at most one request, follow no redirects, retry none, and read no response content.

#### Scenario: Endpoint redirects or returns a body
- **WHEN** a scheduled endpoint returns a redirect or response body
- **THEN** the system does not follow the redirect or read the body
- **AND** it compares the status with the configured expected status

### Requirement: Limit scheduled observability data

Results, status, logs, and traces SHALL NOT contain configured URLs, query values, response headers, response bodies, or raw exception text. The named client's request logs and URL-bearing trace data SHALL remain suppressed. Metrics SHALL NOT use endpoint-specific labels.

#### Scenario: Scheduled check emits observability data
- **WHEN** a scheduled check emits logs, traces, metrics, or status
- **THEN** the data contains no URL, query value, response data, or raw exception text
- **AND** metrics contain no endpoint-specific labels
