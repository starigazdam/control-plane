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
`baseUrl`. `baseUrl` SHALL be an absolute `http` or `https` URI. The host SHALL
load the external plugin set at startup only, and a restart SHALL apply a
configuration change.

#### Scenario: Host consumes a deployed external plugin
- **WHEN** the host starts with a configured external plugin entry
- **THEN** the host calls that plugin over HTTP at its `baseUrl` and does not start a plugin process

#### Scenario: Configured baseUrl is missing or invalid
- **WHEN** a configured plugin entry has a missing `baseUrl`, or a `baseUrl` that is not an absolute `http` or `https` URI
- **THEN** the host fails startup with a clear error that names the `baseUrl` setting

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

The response SHALL be a JSON object with a `statusProviderIds` array and an
`operations` array. A response that is not JSON or that omits either array SHALL
be invalid capabilities. An operation entry SHALL require a string `id` and a
string `displayName`; `description` SHALL default to empty,
`requiresConfirmation` to `false`, and `parameters` to empty. An operation entry
with a missing or wrongly typed required field SHALL be excluded with a `Warning`
signal on `{name}.bridge`.

#### Scenario: Operation entry is missing a required field
- **WHEN** a capabilities response contains an operation entry with a missing or wrongly typed required field
- **THEN** the host excludes that entry, reports a `Warning` signal on `{name}.bridge`, and registers the remaining capabilities

### Requirement: Validate capability sub-entries

A parameter entry SHALL require a string `name` and a string `type`;
`displayName` SHALL default to `name`, `description` to empty, `isRequired` to
`false`, and `defaultValue` to null. An operation with an invalid parameter entry
SHALL be excluded with a `Warning` signal on `{name}.bridge`. A non-string
`statusProviderIds` element SHALL be excluded with a `Warning` signal on
`{name}.bridge`.

#### Scenario: Operation entry has an invalid parameter entry
- **WHEN** a capabilities response contains an operation whose parameter entry omits a required field or supplies a wrongly typed required field
- **THEN** the host excludes that operation, reports a `Warning` signal on `{name}.bridge`, and registers the remaining capabilities

#### Scenario: Status provider id element is not a string
- **WHEN** a capabilities response contains a non-string element in `statusProviderIds`
- **THEN** the host excludes that element, reports a `Warning` signal on `{name}.bridge`, and registers the remaining capabilities

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
The first character of a remote id SHALL be an ASCII letter or digit. The status
provider id `bridge` SHALL be reserved. The host SHALL exclude a status provider
whose remote id is `bridge`, and a capability whose remote id does not match the
grammar. The host SHALL report each exclusion as a `Warning`
signal on `{name}.bridge`.

#### Scenario: Capabilities contain an invalid remote id
- **WHEN** a capabilities response lists a capability whose remote id does not match the id grammar
- **THEN** the host registers no capability for it and reports a `Warning` signal on `{name}.bridge`

#### Scenario: Remote status provider id is reserved
- **WHEN** a capabilities response lists a status provider whose remote id is `bridge`
- **THEN** the host registers no status provider for it and reports a `Warning` signal on `{name}.bridge`

### Requirement: Exclude duplicate remote ids

The host SHALL exclude every entry in a duplicate group when two status provider
ids, or two operation ids, are equal. The comparison SHALL be ordinal
case-insensitive, matching the workbench operation lookup. The host SHALL report
each exclusion as a `Warning` signal on `{name}.bridge`.

#### Scenario: Status provider ids duplicate
- **WHEN** a capabilities response lists two status provider ids that are equal under ordinal case-insensitive comparison
- **THEN** the host excludes every entry in that duplicate group and reports a `Warning` signal on `{name}.bridge`

#### Scenario: Operation ids duplicate
- **WHEN** a capabilities response lists two operation ids that are equal under ordinal case-insensitive comparison
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

### Requirement: Resolve external endpoints from baseUrl

The bridge SHALL resolve each endpoint by appending its path to `baseUrl` with
exactly one `/` separator, preserving any path in `baseUrl`. In each path the
bridge SHALL use the remote id captured at discovery, not the requested operation
id, and SHALL percent-encode it as one path segment. The bridge SHALL
percent-encode `projectId`.

#### Scenario: baseUrl carries a path
- **WHEN** a configured `baseUrl` contains a path
- **THEN** the bridge preserves that path and appends the endpoint path with exactly one `/` separator

#### Scenario: Remote id is used on the wire
- **WHEN** a caller invokes an exposed external operation
- **THEN** the bridge uses the remote id captured at discovery, percent-encoded as one path segment, in the wire path

### Requirement: Register a bridge status provider for every plugin

The host SHALL register one status provider `{name}.bridge` for every
configured plugin. The provider SHALL report `Healthy` when discovery succeeded
with no excluded capability. The provider SHALL report `Warning` when the plugin
is unreachable, returns a non-2xx response or invalid capabilities, or when one
or more capabilities were excluded. The provider SHALL reflect startup discovery
only and SHALL NOT change after startup. The fixed signal ids are `unreachable`
and `invalid-capabilities`.

#### Scenario: Plugin discovery succeeds without exclusions
- **WHEN** a configured plugin is reachable and returns no excluded capability
- **THEN** the host registers `{name}.bridge` reporting `Healthy`

#### Scenario: Plugin is unreachable at startup
- **WHEN** a configured plugin is unreachable at startup
- **THEN** the host registers `{name}.bridge` reporting `Warning` with the fixed signal id `unreachable` and starts

#### Scenario: Plugin returns invalid capabilities at startup
- **WHEN** a configured plugin returns a non-2xx response or invalid capabilities at startup
- **THEN** the host registers `{name}.bridge` reporting `Warning` with the fixed signal id `invalid-capabilities` and starts

#### Scenario: Plugin capabilities are excluded
- **WHEN** one or more capabilities of a reachable plugin are excluded
- **THEN** the host keeps the remaining capabilities and registers `{name}.bridge` reporting `Warning` that lists the excluded capabilities

#### Scenario: Bridge provider reflects startup discovery only
- **WHEN** a plugin that succeeded at discovery fails after startup
- **THEN** the `{name}.bridge` provider keeps reporting its startup result and does not change

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

### Requirement: Map a failed status call to a warning snapshot

A status call SHALL fail when it times out, fails at the transport layer, returns
a non-2xx response, or returns a body that does not deserialize to a
`StatusSnapshot` with known `StatusLevel` values. `GetStatusAsync` SHALL NOT
throw for a failed call; it SHALL return a `Warning` snapshot. For every returned
snapshot, the bridge SHALL set `providerId` to `{name}.{remoteId}` and
`projectId` to the requested project id.

#### Scenario: Remote status call fails
- **WHEN** a remote status call times out, fails at the transport layer, returns a non-2xx response, or returns a body that does not deserialize to a `StatusSnapshot` with known `StatusLevel` values
- **THEN** the bridge returns a `Warning` snapshot and does not throw

#### Scenario: Returned snapshot identity
- **WHEN** the bridge returns a status snapshot for a plugin named `sample` with remote id `ping`
- **THEN** the snapshot carries `providerId` `sample.ping` and `projectId` set to the requested project id

### Requirement: Map a failed execute call to a stable result

A remote execute call that times out, fails at transport, returns a non-2xx
response, or returns an invalid response SHALL produce an
`OperationExecutionResult` with status `Failed` and a stable bridge code. The
host SHALL record the result in execution history.

#### Scenario: Remote execute call fails at the bridge
- **WHEN** a remote execute call times out, fails at transport, returns a non-2xx response, or returns an invalid response
- **THEN** the bridge returns a `Failed` result with a stable bridge code and the host records it in execution history

### Requirement: Define a well-formed execute response

A well-formed remote execute response SHALL carry a string `status` and a string
`message`; `errorCode` and `output` are optional. The bridge SHALL pass the
remote `status`, `message`, `errorCode`, and `output` through unchanged, and SHALL
set `startedAtUtc` and `completedAtUtc` from its own call timing.

#### Scenario: Remote execute response is well formed
- **WHEN** a remote execute response carries a string `status` and a string `message`
- **THEN** the bridge passes the remote `status`, `message`, `errorCode`, and `output` through unchanged and sets the timestamps from its own call timing

### Requirement: Use stable bridge error codes

A bridge-side execute failure SHALL map to a stable bridge code: a timeout SHALL
map to `external_operation_timeout`; a connection or protocol failure SHALL map
to `external_operation_transport_failed`; and a non-2xx HTTP status SHALL map to
`external_operation_unexpected_status`.

#### Scenario: Remote execute call times out
- **WHEN** a remote execute call exceeds the bridge timeout
- **THEN** the bridge returns a `Failed` result with `external_operation_timeout`

#### Scenario: Remote execute call fails at the transport layer
- **WHEN** a remote execute call fails at the connection or protocol layer
- **THEN** the bridge returns a `Failed` result with `external_operation_transport_failed`

#### Scenario: Remote execute call returns a non-2xx status
- **WHEN** a remote execute call returns a non-2xx HTTP status
- **THEN** the bridge returns a `Failed` result with `external_operation_unexpected_status`

### Requirement: Map an invalid execute response to the invalid-response code

An unparseable body, a missing required field, a `status` value outside
`Succeeded`, `Failed`, or `Rejected`, or a remote `errorCode` that begins with
`external_operation_` SHALL map to `external_operation_invalid_response`.

#### Scenario: Remote execute status is not recognized
- **WHEN** a remote execute response carries a `status` value outside `Succeeded`, `Failed`, or `Rejected`
- **THEN** the bridge returns a `Failed` result with `external_operation_invalid_response`

#### Scenario: Remote errorCode uses a bridge prefix
- **WHEN** a well-formed remote execute response carries an `errorCode` that begins with `external_operation_`
- **THEN** the bridge returns a `Failed` result with `external_operation_invalid_response`

### Requirement: Propagate caller cancellation

Caller cancellation SHALL propagate `OperationCanceledException`. Only the
bridge's own timeout SHALL map to `external_operation_timeout`.

#### Scenario: Caller cancels an external call
- **WHEN** the caller's cancellation token is cancelled during an external call
- **THEN** the bridge propagates `OperationCanceledException` and does not map the cancellation to `external_operation_timeout`

### Requirement: Reject a remote operation when the validate call fails

When a remote validate call times out, fails at the transport layer, returns a
non-2xx response, or returns a malformed body, the bridge `ValidateAsync` SHALL
return one error string that contains a stable bridge code. The error string
SHALL use the format `"<code>: <detail>"`. The workbench SHALL reject the
execution with `operation_validation_failed` and SHALL NOT call the remote
execute.

#### Scenario: Remote validate call fails
- **WHEN** a remote validate call times out, fails at the transport layer, returns a non-2xx response, or returns a malformed body
- **THEN** `ValidateAsync` returns one error string with the format `"<code>: <detail>"` and the workbench rejects with `operation_validation_failed` without calling execute

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
an external plugin. The bridge SHALL disable trace-context propagation on its
`HttpClient`. The host SHALL NOT authenticate an external call. An operator
SHALL run an external plugin on a trusted network only until #57.

#### Scenario: Host calls an external plugin
- **WHEN** the host calls an external plugin
- **THEN** the request carries no actor identity, credentials, `traceparent`, `tracestate`, or other correlation header
