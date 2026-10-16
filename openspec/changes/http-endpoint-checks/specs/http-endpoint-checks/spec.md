# Spec Delta

## Purpose

Lets operators check configured HTTP endpoints through a bounded manual operation and status signals, without allowing callers to choose destinations or send request bodies.

## ADDED Requirements

### Requirement: Load and validate configured endpoints

The system SHALL load endpoint definitions from `HttpEndpoint__ConfigPath` and resolve relative paths from the API content root. An unset or empty path SHALL disable the plugin and return an `Unknown` snapshot with no signals. A configured missing, unreadable, or malformed file SHALL fail startup with a clear error that names the setting and path without endpoint URLs.

#### Scenario: Endpoint configuration is disabled
- **WHEN** `HttpEndpoint__ConfigPath` is unset or empty
- **THEN** the plugin makes no requests and exposes an `Unknown` snapshot with no signals

#### Scenario: Configured file is missing or malformed
- **WHEN** a non-empty `HttpEndpoint__ConfigPath` names a missing, unreadable, or malformed file
- **THEN** the host fails startup with a clear configuration error
- **AND** the error does not expose endpoint URLs

#### Scenario: Invalid endpoint entry
- **WHEN** a valid JSON file contains an invalid entry
- **THEN** the system excludes that entry from requests
- **AND** the status provider reports a `Warning` identified by the entry index

Each valid endpoint SHALL have a unique case-insensitive ID of 1-64 ASCII letters, digits, dots, underscores, or hyphens; a non-empty display name; an absolute HTTP or HTTPS URL without user information; an expected status from 100 through 599; a `GET` or `HEAD` method; and a timeout from 1 through 10 seconds, defaulting to 5 seconds. The file SHALL contain no more than 20 entries. Invalid entries SHALL not be used.

#### Scenario: Duplicate endpoint IDs
- **WHEN** two or more definitions have the same ID without regard to case
- **THEN** the system excludes every definition in that duplicate group
- **AND** the status provider reports a safe configuration warning for each excluded entry

#### Scenario: Unsupported method or schedule field
- **WHEN** a definition uses `POST` or another unsupported method
- **THEN** the system excludes that definition and makes no request for it
- **WHEN** a definition includes a `cron` field
- **THEN** the system does not schedule requests and reports one safe warning
- **AND** an otherwise valid definition remains available for manual checks

### Requirement: Check only a configured endpoint

The system SHALL provide the operation ID `http-endpoint-check`, which accepts only a configured endpoint ID. It SHALL NOT accept a URL, request body, or caller-supplied headers. An unknown ID SHALL be rejected during validation without an HTTP request; the existing workbench SHALL persist the rejection with `operation_validation_failed`.

#### Scenario: Operator checks a configured endpoint
- **WHEN** the operation receives a known endpoint ID
- **THEN** the system sends one request using its configured `GET` or `HEAD` method
- **AND** the system persists only the safe operation summary

#### Scenario: Caller supplies an unknown endpoint ID
- **WHEN** the operation receives an ID that is not configured
- **THEN** the workbench records a rejected `operation_validation_failed` result
- **AND** the system sends no HTTP request

### Requirement: Bound endpoint requests

Each check SHALL honor cancellation and use a timeout from 1 through 10 seconds, with a 5-second default. The system SHALL send at most one request per check, SHALL NOT retry or follow redirects, and SHALL NOT buffer or read response content.

#### Scenario: Endpoint check times out
- **WHEN** an endpoint does not respond within its check timeout
- **THEN** the system cancels the request and returns `endpoint_timeout`
- **AND** the system remains available for other requests

#### Scenario: Endpoint redirects or returns a body
- **WHEN** an endpoint returns a redirect or response body
- **THEN** the system does not follow the redirect or read the response body
- **AND** it compares the returned status with the configured expected status

#### Scenario: Transient response does not cause a retry
- **WHEN** an endpoint returns a transient 503 response
- **THEN** the system sends no retry request
- **AND** the operation reports `endpoint_unexpected_status`

### Requirement: Bound overview scans

Overview scans SHALL check at most 20 endpoints concurrently, share one in-flight scan per API process among concurrent callers, cache results for 15 seconds, and stop after a 12-second overall budget. A caller's cancellation SHALL stop its wait but SHALL NOT cancel a scan that other callers share.

#### Scenario: Concurrent overview calls share a scan
- **WHEN** multiple callers request overview status during an active scan
- **THEN** the system shares that scan and its results
- **AND** a caller's cancellation does not cancel the shared scan

#### Scenario: Overview scan exceeds its budget
- **WHEN** endpoint checks remain incomplete at the 12-second overview budget
- **THEN** the system cancels unfinished requests
- **AND** the overview returns safe `endpoint_timeout` signals within the budget

### Requirement: Return safe check summaries

A successful summary SHALL include the endpoint ID, observed status, observation time, and elapsed duration. A failed summary SHALL use `endpoint_unexpected_status`, `endpoint_timeout`, or `endpoint_request_failed`. Results, status descriptions, logs, and traces SHALL NOT contain configured URLs, response headers, response bodies, or raw exception text. The named client's framework request logs SHALL be suppressed, and its trace data SHALL suppress or redact `url.full`.

#### Scenario: Endpoint returns the expected status
- **WHEN** a configured endpoint returns its expected HTTP status
- **THEN** the operation returns a successful safe summary

#### Scenario: Endpoint returns an unexpected status or network failure
- **WHEN** a configured endpoint returns a different status or the request fails
- **THEN** the operation returns a failed safe summary with its stable error code
- **AND** its persisted history contains no URL, response data, or raw exception text

#### Scenario: Logs and traces record a check
- **WHEN** the named client emits logs or trace data for a request
- **THEN** those records contain no configured URL, query value, or raw exception text

### Requirement: Show configured endpoint status

The overview SHALL expose one signal per valid endpoint. A matching response SHALL map to `Healthy`; an unexpected status SHALL map to `Warning`; timeout or transport failure SHALL map to `Critical`. Invalid entries and unsupported schedule fields SHALL map to safe `Warning` signals. The disabled plugin SHALL return `Unknown` with no signals.

#### Scenario: Overview reads a healthy endpoint
- **WHEN** the status provider receives the expected status
- **THEN** the overview shows a `Healthy` signal for that endpoint

#### Scenario: Overview reads a degraded or unavailable endpoint
- **WHEN** the status provider receives an unexpected HTTP status
- **THEN** the overview shows a `Warning` signal
- **WHEN** the request times out or fails at the transport layer
- **THEN** the overview shows a `Critical` signal without sensitive endpoint data

### Requirement: Keep scheduled checks and POST outside this change

This change SHALL provide manual `GET` and `HEAD` checks only. It SHALL NOT send `POST` requests, start background schedules, or parse cron expressions. These behaviors require separate follow-up issues and approved designs; this change SHALL not be treated as their completion.

#### Scenario: Configuration contains a POST method or schedule
- **WHEN** configuration contains a `POST` method or `cron` field
- **THEN** the system sends no POST request and starts no schedule
- **AND** it reports the safe warning specified for that definition
