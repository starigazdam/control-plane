# Spec Delta

## Purpose

Lets operators inspect configured HTTP endpoint health through a bounded manual check and a status signal, without allowing callers to choose arbitrary destinations or send request bodies.

## ADDED Requirements

### Requirement: Load and validate configured endpoints

The system SHALL load endpoint definitions from external runtime configuration. Each endpoint SHALL have a unique non-empty ID, a display name, an absolute HTTP or HTTPS URL without user information, and an expected status code. Configuration SHALL allow only GET or HEAD. Invalid definitions SHALL not be used for requests.

#### Scenario: Valid configured endpoint
- **WHEN** configuration contains a unique endpoint with an allowed URL and method
- **THEN** the system makes that endpoint available by its configured ID

#### Scenario: Invalid or duplicate endpoint definition
- **WHEN** configuration contains an invalid definition or duplicate ID
- **THEN** the system does not make that definition available
- **AND** the status provider reports a configuration warning without exposing the URL

### Requirement: Check only a configured endpoint

The system SHALL provide a named manual operation that accepts a configured endpoint ID. It SHALL NOT accept a URL, request body, or caller-supplied headers. An unknown or unavailable ID SHALL be rejected without making an HTTP request.

#### Scenario: Operator checks a configured endpoint
- **WHEN** the operation receives a known endpoint ID
- **THEN** the system sends one request using that endpoint's configured GET or HEAD method

#### Scenario: Caller supplies an unknown endpoint ID
- **WHEN** the operation receives an ID that is not configured
- **THEN** the system returns a rejected result
- **AND** the system sends no HTTP request

### Requirement: Bound endpoint requests

Each endpoint request SHALL use a finite timeout and honor cancellation. The system SHALL NOT follow redirects, read or return response bodies, or include configured URLs, response headers, or raw exception text in operation results, status descriptions, or logs.

#### Scenario: Endpoint does not respond before timeout
- **WHEN** an endpoint exceeds its configured bounded timeout
- **THEN** the check ends with a safe timeout result
- **AND** the system remains available for other requests

#### Scenario: Endpoint redirects or returns a body
- **WHEN** an endpoint returns a redirect or response body
- **THEN** the system does not follow the redirect or expose the response body

### Requirement: Return a safe check summary

The system SHALL report whether the observed HTTP status matches the configured expected status, the observation time, and the elapsed duration. It SHALL map network failures and unexpected status codes to a safe failure result with a stable error code. It SHALL NOT persist or return endpoint secrets or response payloads.

#### Scenario: Endpoint returns the expected status
- **WHEN** a configured endpoint returns its expected HTTP status
- **THEN** the operation returns a successful summary with the status and duration

#### Scenario: Endpoint returns an unexpected status or network failure
- **WHEN** a configured endpoint returns a different status or the request fails
- **THEN** the operation returns a failed summary with a stable error code
- **AND** the summary contains no response body, configured URL, or raw exception text

### Requirement: Show configured endpoint status

The overview SHALL expose a status signal for each configured endpoint. A matching response SHALL be Healthy; an unexpected status or request failure SHALL be Warning. Missing or invalid configuration SHALL be visible as Warning rather than silently omitted.

#### Scenario: Overview reads a healthy endpoint
- **WHEN** the status provider checks an endpoint and receives its expected status
- **THEN** the overview shows a Healthy signal for that endpoint

#### Scenario: Overview reads an unavailable endpoint
- **WHEN** the status provider cannot complete a bounded check or receives an unexpected status
- **THEN** the overview shows a Warning signal without exposing sensitive endpoint data

### Requirement: Keep scheduled checks outside this change

This change SHALL provide only manual checks and on-demand status checks. It SHALL NOT start background schedules or parse cron expressions. Scheduled checks require a separate approved design and must not be treated as complete under this capability.

#### Scenario: Configuration includes a schedule
- **WHEN** endpoint configuration includes an unsupported schedule field
- **THEN** the system does not start scheduled requests
- **AND** the status provider reports that scheduling is not supported by this capability
