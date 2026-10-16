# Operation Execution Specification

## Purpose

Describe the existing request-to-persistence path for operation execution in the
API: a request enters through the controller, the workbench service resolves the
operation, validates it, executes it, and persists an execution history entry.
This baseline covers only that inspected path. It does not describe, and must not
be read as promising, server-enforced authentication, authorization,
confirmation, immutable or redacted audit, a dynamic operation registry, or agent
parity.

## Requirements

### Requirement: Reject requests for unknown operations

When a request names an operation that the service cannot resolve, the service
SHALL produce a rejected result with error code `operation_not_found` and SHALL
persist an execution history entry for the request. The service SHALL NOT
execute any operation in this case.

#### Scenario: Operation id is not known

- **WHEN** a request arrives with an operation id that does not resolve
- **THEN** the service returns a rejected result with error code `operation_not_found`
- **AND** the service persists a history entry for the request

### Requirement: Reject requests that fail validation before execution

When the resolved operation reports validation errors, the service SHALL produce
a rejected result with error code `operation_validation_failed` whose message
joins the validation errors. The service SHALL NOT call the operation's
`ExecuteAsync` and SHALL persist an execution history entry.

#### Scenario: Operation validation returns errors

- **WHEN** the resolved operation returns one or more validation errors
- **THEN** the service returns a rejected result with error code `operation_validation_failed`
- **AND** the service does not call `ExecuteAsync`
- **AND** the service persists a history entry for the request

### Requirement: Execute and persist valid operations

When the operation resolves and validation reports no errors, the service SHALL
call the operation's `ExecuteAsync` and SHALL persist the returned result. The
persisted history entry SHALL include the operation result, the correlation id,
the request time, and the recorded initiator.

#### Scenario: Valid request executes

- **WHEN** the operation resolves and validation reports no errors
- **THEN** the service calls `ExecuteAsync`
- **AND** the service persists the returned result
- **AND** the persisted entry includes the correlation id from the request

### Requirement: Normalize operation lookup

The service SHALL trim the requested operation id before lookup and SHALL match
operation ids case-insensitively. A blank or whitespace-only operation id SHALL
not resolve.

#### Scenario: Operation id has surrounding whitespace or different casing

- **WHEN** a request supplies an operation id with surrounding whitespace or different casing than the registered id
- **THEN** the service resolves the matching operation

#### Scenario: Operation id is blank

- **WHEN** a request supplies a blank or whitespace-only operation id
- **THEN** the service resolves no operation and rejects the request

### Requirement: Clamp and order execution history

`GetExecutionHistoryAsync` SHALL clamp the requested `take` value to the range 1
through 200 and SHALL return entries ordered by descending database `Id`. The
history endpoint SHALL default `take` to 50 when the query value is omitted.

#### Scenario: Requested take is outside the allowed range

- **WHEN** a caller requests fewer than 1 or more than 200 entries
- **THEN** the service clamps the value into the range 1 through 200

#### Scenario: History is returned most recent first

- **WHEN** a caller requests execution history
- **THEN** the service returns entries ordered by descending database `Id`

### Requirement: Return execution outcomes as HTTP success results

The operations controller SHALL return HTTP 200 with the execution history entry
for both rejected and successful operations. A rejected outcome SHALL be
represented by the result status and error code in the response body, not by an
HTTP error status.

#### Scenario: Execute endpoint rejects an operation

- **WHEN** the workbench service rejects a request
- **THEN** the controller responds with HTTP 200 and a body containing the rejected result

## Notes

Non-normative. This section records known limitations of the inspected code. It
is not a security promise.

- The recorded initiator is the trimmed client-supplied `RequestedBy` when
  non-blank, otherwise the literal fallback `local-engineer`. This is current
  behavior only. It is not a verified identity, and nothing in this path
  authenticates or authorizes the caller.
- The inspected path provides no server-enforced authentication, authorization,
  or confirmation step, and no immutable or redacted audit trail.
  `RequiresConfirmation`-style metadata on an operation definition is not
  enforced here and proves none of those properties.
- The existing `tests/ControlPlane.Api.E2E/ServiceBusDlqToQueueSliceTests.cs`
  test named `ServiceBusDlqToQueueFlow_is_exposed_and_executable` proves only
  operation exposure, an execution response, and history presence in its
  fixture. It is not proof of real Service Bus replay, server-side security,
  every scenario above, or agent parity.

## Evidence

Non-normative. Observations come from source inspection unless a named test is
called out.

- Workbench service: `src/ControlPlane.Api/Services/ControlPlaneWorkbenchService.cs`
  (`ExecuteAsync`, `GetExecutionHistoryAsync`, `ResolveOperation`).
- Controller: `src/ControlPlane.Api/Controllers/OperationsController.cs`.
- Contracts: `src/ControlPlane.Api/Contracts/ExecuteOperationRequest.cs` and
  `src/ControlPlane.Api/Contracts/OperationHistoryEntry.cs`.
