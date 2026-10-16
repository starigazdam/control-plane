# Spec Delta

## Purpose

Describe how the host consumes external plugins as out-of-process HTTP and JSON
services, normalizes them to the existing Core contracts, and bounds and reports
their calls. This capability does not describe process supervision, hot reload,
authentication, contract versioning, or declarative views.

## ADDED Requirements

### Requirement: Run external plugins out of process over HTTP

The host SHALL consume an external plugin as an out-of-process HTTP and JSON
service. The consumer SHALL deploy and operate each external plugin process. The
host SHALL NOT spawn, supervise, or restart an external plugin process. The host
SHALL read a static configuration list of entries, each with a `name` and a
`baseUrl`. The host SHALL load the external plugin set at startup only, and a
restart SHALL apply a configuration change.

#### Scenario: Host consumes a deployed external plugin
- **WHEN** the host starts with a configured external plugin entry
- **THEN** the host calls that plugin over HTTP at its `baseUrl` and does not start a plugin process

#### Scenario: External plugin process stops after startup
- **WHEN** an external plugin process stops after the host has started
- **THEN** the host does not restart or supervise that process

#### Scenario: Configuration changes while the host runs
- **WHEN** the configuration list changes while the host runs
- **THEN** the host does not apply the change until it restarts

### Requirement: Discover external capabilities at startup

The host SHALL call `GET /capabilities` once for each configured plugin during
registration, synchronously. Each capabilities call SHALL be bounded by a
10-second timeout. The capabilities response SHALL list the remote status
provider ids and the remote operation definitions.

#### Scenario: Plugin returns capabilities
- **WHEN** a configured plugin responds to `GET /capabilities`
- **THEN** the host reads its remote status provider ids and remote operation definitions

#### Scenario: Capabilities call exceeds its timeout
- **WHEN** a capabilities call does not complete within 10 seconds
- **THEN** the host treats that plugin as unreachable and continues startup

### Requirement: Validate the capabilities response shape

The response SHALL be JSON whose top-level shape is an object with a
`statusProviderIds` array and an `operations` array. A response that is not JSON
or whose top-level shape omits either array SHALL be invalid capabilities. An
operation entry that omits a required field or supplies a wrongly typed required
field SHALL be excluded and reported as a `Warning` signal on `{name}.bridge`.

#### Scenario: Operation entry is missing a required field
- **WHEN** a capabilities response contains an operation entry with a missing or wrongly typed required field
- **THEN** the host excludes that entry, reports a `Warning` signal on `{name}.bridge`, and registers the remaining capabilities

### Requirement: Namespace external capabilities by plugin name

A configured `name` SHALL be 1-32 lowercase ASCII letters, digits, or hyphens,
and SHALL start with a letter. The `name` SHALL be the namespace for every
exposed id. The host SHALL expose each remote operation and remote status
provider as `{name}.{remoteId}`. The host SHALL fail startup when two configured
plugins share a name. The host SHALL fail startup when a configured name does not
match the grammar.

#### Scenario: Plugin capabilities are namespaced
- **WHEN** a configured plugin named `sample` exposes a remote operation id `ping`
- **THEN** the host exposes that operation as `sample.ping`

#### Scenario: Two configured plugins share a name
- **WHEN** two configured plugin entries use the same `name`
- **THEN** the host fails startup with a clear duplicate-name error

#### Scenario: Configured name does not match the grammar
- **WHEN** a configured `name` is empty, too long, starts with a digit or hyphen, or contains an unsupported character
- **THEN** the host fails startup with a clear invalid-name error

### Requirement: Exclude remote ids that are not valid

A remote id SHALL be 1-64 ASCII letters, digits, dots, underscores, or hyphens.
The status provider id `bridge` SHALL be reserved. The host SHALL exclude a
status provider whose remote id is `bridge`, and a capability whose remote id
does not match the grammar. The host SHALL report each exclusion as a `Warning`
signal on `{name}.bridge`.

#### Scenario: Capabilities contain an invalid remote id
- **WHEN** a capabilities response lists a capability whose remote id does not match the id grammar
- **THEN** the host registers no capability for it and reports a `Warning` signal on `{name}.bridge`

#### Scenario: Remote status provider id is reserved
- **WHEN** a capabilities response lists a status provider whose remote id is `bridge`
- **THEN** the host registers no status provider for it and reports a `Warning` signal on `{name}.bridge`

### Requirement: Exclude duplicate remote ids

The host SHALL exclude every entry in a duplicate group when two status provider
ids, or two operation ids, are equal. The comparison SHALL be case-sensitive.
The host SHALL report each exclusion as a `Warning` signal on `{name}.bridge`.

#### Scenario: Status provider ids duplicate
- **WHEN** a capabilities response lists two status provider ids that are equal
- **THEN** the host excludes every entry in that duplicate group and reports a `Warning` signal on `{name}.bridge`

#### Scenario: Operation ids duplicate
- **WHEN** a capabilities response lists two operation ids that are equal
- **THEN** the host excludes every entry in that duplicate group and reports a `Warning` signal on `{name}.bridge`

### Requirement: Normalize external capabilities to Core contracts

The host SHALL normalize each remote status provider to `IStatusProvider` and
each remote operation to `IOperation` through the bridge plugin. The host SHALL
keep every adapter for a custom consumer API in the consumer's process. This
repository SHALL NOT host a per-plugin adapter for a custom consumer API.

#### Scenario: Remote status provider is normalized
- **WHEN** the bridge normalizes a remote status provider
- **THEN** the host exposes it as an `IStatusProvider`

#### Scenario: Remote operation is normalized
- **WHEN** the bridge normalizes a remote operation
- **THEN** the host exposes it as an `IOperation`

### Requirement: Expose remote capabilities through defined wire endpoints

The bridge SHALL fetch status through `GET /status/{id}?projectId={projectId}`.
It SHALL validate an operation through `POST /operations/{id}/validate`. It SHALL
execute an operation through `POST /operations/{id}/execute`. The request and
response payloads SHALL map the Core records directly in camelCase, as defined in
the change design.

#### Scenario: Remote status is read
- **WHEN** a caller reads a remote status provider
- **THEN** the bridge calls `GET /status/{id}?projectId={projectId}` and maps the response to a `StatusSnapshot`

#### Scenario: Remote operation is validated and executed
- **WHEN** a caller invokes a remote operation
- **THEN** the bridge calls `POST /operations/{id}/validate` and `POST /operations/{id}/execute` with the defined payloads

### Requirement: Register a bridge status provider for every plugin

The host SHALL register exactly one status provider `{name}.bridge` for every
configured plugin. The provider SHALL report `Healthy` when discovery succeeded
with no excluded capability. The provider SHALL report `Warning` when the plugin
is unreachable, when the plugin returns a non-2xx response or invalid
capabilities, or when one or more capabilities were excluded.

#### Scenario: Plugin discovery succeeds without exclusions
- **WHEN** a configured plugin is reachable and returns no excluded capability
- **THEN** the host registers `{name}.bridge` reporting `Healthy`

#### Scenario: Plugin is unreachable at startup
- **WHEN** a configured plugin is unreachable at startup
- **THEN** the host registers `{name}.bridge` reporting `Warning` with `unreachable` and starts

#### Scenario: Plugin returns invalid capabilities at startup
- **WHEN** a configured plugin returns a non-2xx response or invalid capabilities at startup
- **THEN** the host registers `{name}.bridge` reporting `Warning` with `invalid capabilities` and starts

#### Scenario: Plugin capabilities are excluded
- **WHEN** one or more capabilities of a reachable plugin are excluded
- **THEN** the host keeps the remaining capabilities and registers `{name}.bridge` reporting `Warning` that lists the excluded capabilities

### Requirement: Keep the host available when discovery fails

When discovery fails for a plugin, the host SHALL register no operations for
that plugin and SHALL still start.

#### Scenario: Failed plugin contributes no operations
- **WHEN** a plugin is unreachable or returns invalid capabilities at startup
- **THEN** the host registers no operations for that plugin and still starts

### Requirement: Bound every external call

The bridge SHALL apply a fixed 10-second timeout to each call to an external
plugin. The bridge SHALL NOT retry a failed call.

#### Scenario: External call exceeds its timeout
- **WHEN** an external call does not complete within 10 seconds
- **THEN** the bridge cancels the call and does not retry it

### Requirement: Map remote failures to stable results

A remote execute call that times out, fails at the transport layer, returns a
non-2xx response, or returns a malformed response SHALL produce an
`OperationExecutionResult` with status `Failed` and a stable bridge code. A
well-formed remote execute response SHALL pass its `status`, `message`,
`errorCode`, and `output` through unchanged. The host SHALL record the result
through the existing execution history path. A failed remote status call SHALL
produce a `Warning` snapshot.

#### Scenario: Remote execute call fails at the bridge
- **WHEN** a remote execute call times out, fails at the transport layer, returns a non-2xx response, or returns a malformed response
- **THEN** the bridge returns a `Failed` result with a stable bridge code and the host records it in execution history

#### Scenario: Remote execute response is well formed
- **WHEN** a remote execute response is well formed
- **THEN** the bridge passes the remote `status`, `message`, `errorCode`, and `output` through unchanged

#### Scenario: Remote status call fails
- **WHEN** a remote status call fails
- **THEN** the bridge returns a `Warning` snapshot

### Requirement: Use stable bridge error codes

A bridge-side failure SHALL use one of the stable bridge codes
`external_operation_timeout`, `external_operation_transport_failed`,
`external_operation_unexpected_status`, or
`external_operation_invalid_response`. A bridge code SHALL be produced only for
a bridge-side failure. A remote `status` value outside `Succeeded`, `Failed`, or
`Rejected` SHALL be a malformed response and SHALL map to
`external_operation_invalid_response`.

#### Scenario: Remote execute status is not recognized
- **WHEN** a remote execute response carries a `status` value outside `Succeeded`, `Failed`, or `Rejected`
- **THEN** the bridge returns a `Failed` result with `external_operation_invalid_response`

### Requirement: Reject a remote operation when the validate call fails

When a remote validate call times out, fails at the transport layer, returns a
non-2xx response, or returns a malformed body, the bridge `ValidateAsync` SHALL
return one error string that contains a stable bridge code. The workbench SHALL
reject the execution with `operation_validation_failed` and SHALL NOT call the
remote execute.

#### Scenario: Remote validate call fails
- **WHEN** a remote validate call times out, fails at the transport layer, returns a non-2xx response, or returns a malformed body
- **THEN** `ValidateAsync` returns one error string with a stable bridge code and the workbench rejects with `operation_validation_failed` without calling execute

### Requirement: Preserve remote confirmation as metadata only

The host SHALL map a remote `requiresConfirmation` value to
`OperationDefinition.RequiresConfirmation`. That value SHALL remain metadata
only. The host SHALL NOT enforce confirmation, authentication, or authorization
for an external call.

#### Scenario: Remote operation requires confirmation
- **WHEN** a remote operation definition sets `requiresConfirmation`
- **THEN** the host records it on `OperationDefinition.RequiresConfirmation` and does not enforce it

### Requirement: Do not forward caller identity

The host SHALL NOT forward actor identity, a correlation id, or credentials to
an external plugin. The host SHALL NOT authenticate an external call. An
operator SHALL run an external plugin on a trusted network only until #57.

#### Scenario: Host calls an external plugin
- **WHEN** the host calls an external plugin
- **THEN** the request carries no actor identity, correlation id, or credentials
