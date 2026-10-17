# Design

## Context

See `proposal.md` and the delta specs for the proposed behavior. At the
inspected base, the host discovers plugins from assemblies supplied to
`AddControlPlanePlugins`. It creates each plugin with `Activator.CreateInstance`,
calls an optional `IPluginServiceRegistration.RegisterServices` before
`Register`, then registers each collected type as a transient `IStatusProvider`
or `IOperation`. `DiscoveredPluginCatalog` holds plugin names and `Type` lists.
`ControlPlaneWorkbenchService` consumes `IEnumerable<IStatusProvider>` and
`IEnumerable<IOperation>` from dependency injection.

The Core contracts are type-based. `IControlPlanePlugin` exposes a `Name` and a
`Register(IPluginRegistration)` method. `IPluginRegistration` exposes only
`AddStatusProvider<TStatusProvider>()` and `AddOperation<TOperation>()`.
`IOperation` exposes a `Definition`, `ValidateAsync`, and `ExecuteAsync`.
`IStatusProvider` exposes an `Id` and `GetStatusAsync`. The records involved are
`OperationDefinition`, `OperationParameter`, `OperationExecutionResult`,
`StatusSnapshot`, and `StatusSignal`. `OperationExecutionStatus` has `Succeeded`,
`Failed`, and `Rejected`. `StatusLevel` has `Unknown`, `Healthy`, `Warning`, and
`Critical`.

`RequiresConfirmation` is metadata only. The host does not enforce
confirmation, authentication, authorization, or immutable audit today. This
change does not add or rely on any of those controls.

## Goals / Non-Goals

**Goals:**

- Consume separately deployed external plugins over HTTP and JSON.
- Normalize remote capabilities to the existing `IStatusProvider` and
  `IOperation` contracts.
- Namespace every exposed id and validate plugin names and remote ids.
- Keep the host available when a plugin is unreachable or invalid.
- Bound startup discovery and every call time.
- Map every remote failure to a stable status and a stable error code.

**Non-Goals:**

- Authentication, authorization, actor or correlation forwarding, confirmation
  enforcement, request limits, and redaction. These belong to #57.
- Declarative views. These belong to #58.
- Contract versioning. Add it only when a second contract version exists.
- Named pipe or stdio transport, hot reload, process supervision, SDK packages,
  remote ES modules, and per-plugin adapters in this repository.
- Live external systems in automated tests.

## Decisions

### Use out-of-process HTTP and JSON only

External plugins run out of process. The consumer deploys and operates them, and
the host never spawns, supervises, or restarts them. Transport is HTTP and JSON
only. This keeps the host boundary small and lets each consumer choose a runtime.
The host loads the plugin set at startup only, so a restart applies a change.

Alternatives considered: a shared in-process library was rejected because it
reintroduces the compile-and-redeploy coupling that #56 removes. Named pipe or
stdio transport was rejected as an extra platform-specific surface for the first
slice. Host-side process supervision was rejected because the consumer owns the
plugin lifecycle.

### Define the wire contract as a direct mapping of the Core records

Each configured plugin serves endpoints relative to its `baseUrl`. A configured
`baseUrl` must be an absolute `http` or `https` URI with no query, fragment, or
userinfo; a missing or invalid value fails startup with a clear error that names
the setting. The bridge resolves each
endpoint by appending its path to `baseUrl` with exactly one `/` separator,
preserving any path in `baseUrl`. The bridge maps the payloads to the Core
contracts in camelCase.

- `GET /capabilities` returns the remote status provider ids and operation
  definitions.

  ```json
  {
    "statusProviderIds": ["<remote id>"],
    "operations": [
      {
        "id": "<remote id>",
        "displayName": "<text>",
        "description": "<text>",
        "requiresConfirmation": false,
        "parameters": [
          {
            "name": "<text>",
            "displayName": "<text>",
            "description": "<text>",
            "isRequired": false,
            "type": "<text>",
            "defaultValue": null
          }
        ]
      }
    ]
  }
  ```

- `GET /status/{id}?projectId={projectId}` returns a `StatusSnapshot`.

  ```json
  {
    "projectId": "<text>",
    "providerId": "<remote id>",
    "overallLevel": "Warning",
    "observedAtUtc": "<timestamp>",
    "signals": [
      {
        "id": "<text>",
        "title": "<text>",
        "description": "<text>",
        "level": "Warning",
        "observedAtUtc": "<timestamp>",
        "source": null,
        "link": null
      }
    ]
  }
  ```

- `POST /operations/{id}/validate` accepts a project id and input, and returns a
  list of validation error strings.

  ```json
  { "projectId": "<text>", "input": { "<name>": "<value>" } }
  ```

  ```json
  ["<validation error>"]
  ```

- `POST /operations/{id}/execute` accepts a project id and input, and returns an
  `OperationExecutionResult`.

  ```json
  { "projectId": "<text>", "input": { "<name>": "<value>" } }
  ```

  ```json
  {
    "status": "Failed",
    "message": "<text>",
    "startedAtUtc": "<timestamp>",
    "completedAtUtc": "<timestamp>",
    "errorCode": "sample_failed",
    "output": { "<name>": "<value>" }
  }
  ```

The bridge sets `startedAtUtc` and `completedAtUtc` itself from its own call
timing and ignores the remote timestamps. A well-formed execute response
requires the string `status` with a value of `Succeeded`, `Failed`, or
`Rejected` and the string `message`; `errorCode` must not begin with
`external_operation_`, and `output` is optional. Any other response is invalid.

`StatusLevel` and `OperationExecutionStatus` serialize as strings, matching the
host's existing `JsonStringEnumConverter`. The implementation publishes this
contract as OpenAPI in `docs/`. This change specifies that publication; it does
not write the OpenAPI file now.

Alternatives considered: a hand-written request model per operation was rejected
because it duplicates the Core records. A binary or streaming transport was
rejected because it adds complexity without a first-slice need.

### Add one bridge plugin project

One bridge plugin project, `ControlPlane.ExternalPlugins`, hosts the bridge. It
reads configuration, fetches capabilities, and creates one status provider
`{name}.bridge` per plugin plus one bridge status provider or operation per
remote capability. Adapters for custom consumer APIs live in the consumer's
process, never in this repository.

The bridge implements `IPluginServiceRegistration`. During `RegisterServices` it
binds its section from the `IConfiguration` the host passes, registers a named
`HttpClient` with `services.AddHttpClient("<name>")`, and keeps the configured
plugin list for its `Register` call. The host passes the application
`IConfiguration` to every `IPluginServiceRegistration`; the bridge no longer
relies on a static or `BuildServiceProvider()` workaround.

At archive, the baseline `plugin-registration` Purpose is amended to state that
external out-of-process plugins are consumed through the assembly-supplied
`ControlPlane.ExternalPlugins` bridge, specified in `external-plugins`, and that
no in-process dynamic assembly loader exists.

Alternatives considered: a separate bridge assembly per remote plugin was
rejected because it multiplies projects without adding a boundary. Letting the
host call remote plugins directly was rejected because it would put transport
concerns in the host and `ControlPlaneWorkbenchService`.

### Extend the registration contract with descriptors

`IPluginRegistration` gains an instance overload and a factory overload for each
capability kind: `AddStatusProvider(IStatusProvider)`,
`AddStatusProvider(Func<IServiceProvider, IStatusProvider>)`,
`AddOperation(IOperation)`, and
`AddOperation(Func<IServiceProvider, IOperation>)`. `PluginRegistration` collects
a descriptor for each registration instead of a `Type` set. Type descriptors
keep the current deduplication by type. Instance and factory descriptors are
recorded separately, so one plugin can register N remote capabilities.

`DiscoveredPluginCatalog` changes from `Type` lists to descriptor lists. The
host registers a type descriptor as a transient `IStatusProvider` or
`IOperation`, an instance descriptor as a singleton, and a factory descriptor
as a singleton created by invoking its factory with the `IServiceProvider`. The
factory receives the service provider so a bridge can resolve
`IHttpClientFactory` at runtime. Built-in plugins are updated only as the
changed contract requires. There is no backward-compatibility requirement
because the project is new.

Alternatives considered: keeping `Type`-only registration and constructing each
remote bridge through DI was rejected because the bridge must capture per-remote
values, such as the remote id and the plugin `baseUrl`, which a parameterless or
DI-resolved type cannot express. Registering every bridge as a type with a
custom constructor was rejected for the same reason.

### Fetch capabilities once at startup

The bridge fetches `GET /capabilities` once per plugin during registration,
synchronously, each call bounded by a 10-second timeout. The startup fetch runs
before the service container exists, so it uses one short-lived `HttpClient`;
remote calls at runtime use `IHttpClientFactory`. The host always registers one
status provider `{name}.bridge` per configured plugin. That provider reports
`Healthy` when discovery succeeds with no exclusions. It reports `Warning` when
the plugin is unreachable, when the response is invalid capabilities, or when
one or more capabilities are excluded; an exclusion warning lists the excluded
capabilities. The provider reflects startup discovery only, so it does not
change after startup. The signal ids are the fixed values `unreachable` and
`invalid-capabilities`. Failure discovery registers no operations for that
plugin and lets the host start.

Alternatives considered: lazy discovery on first use was rejected because it
moves a network call onto the request path and makes operations appear and
disappear. Background discovery was rejected because it complicates startup and
the first slice does not need it.

### Namespace and validate ids

A plugin `name` is 1-32 lowercase ASCII letters, digits, or hyphens, starting
with a letter. The name is the namespace, so the host exposes
`{name}.{remoteId}` for each operation and status provider. A duplicate name
fails startup. An invalid name fails startup. A remote id must be 1-64 ASCII
letters, digits, dots, underscores, or hyphens, and must start with a letter or
digit. The status provider id `bridge` is reserved because it collides with
`{name}.bridge`; that reserved-id comparison is ordinal case-insensitive. The
host excludes a capability that violates the grammar or
uses the reserved id, and excludes every entry in a duplicate id group when two
status provider ids, or two operation ids, are equal. The comparison is ordinal
case-insensitive, matching the workbench operation lookup. Each exclusion is
reported as a `Warning` on `{name}.bridge`.

The host also fails startup when two registered operations, or two registered
status providers, built-in or external, expose ids that are equal under ordinal
case-insensitive comparison. This matches the workbench constructor, which
builds its operation index with `StringComparer.OrdinalIgnoreCase` and would
throw on a case-insensitive duplicate. Because type and factory descriptors
expose an id only on an instance, this host-wide check runs after the container
is built: the host resolves `IEnumerable<IOperation>` and
`IEnumerable<IStatusProvider>` in a scope before it accepts requests, and a
failure stops startup. A plugin `name` equal to a built-in id prefix can collide
with a built-in id, and a remote operation id or status provider id that
otherwise looks valid can still fail startup for that reason.

The capabilities body must be JSON with a top-level object that contains a
`statusProviderIds` array and an `operations` array; otherwise the host treats
the response as invalid capabilities. An operation entry requires the string
`id` and the string `displayName`; `description` defaults to empty,
`requiresConfirmation` to `false`, and `parameters` to empty. A parameter entry
requires the string `name` and the string `type`; `displayName` defaults to
`name`, `description` to empty, `isRequired` to `false`, and `defaultValue` to
null. An operation with an invalid parameter entry is excluded with a `Warning`
on `{name}.bridge`; a non-string `statusProviderIds` element is excluded with a
`Warning`. An optional field with the wrong type, such as a non-boolean
`requiresConfirmation` or a non-array `parameters`, and an `operations` element
that is not an object, each exclude that operation with a `Warning` on
`{name}.bridge`. The remaining capabilities still register.

Alternatives considered: case-insensitive or unvalidated plugin names were
rejected because ids must be stable and predictable. Allowing arbitrary remote
ids was rejected because an exposed id must compose with the host's existing
lookup.

### Bound calls and map failures to stable results

Each call to an external plugin uses a fixed 10-second timeout with no retry.
Runtime remote calls resolve `IHttpClientFactory` from the container. The bridge
uses the remote id captured at discovery in each path and percent-encodes it as
one path segment, and percent-encodes `projectId`.

A remote execute call that fails produces an `OperationExecutionResult` with
status `Failed` and the mapped stable bridge code. The host records that result
through the existing execution history path. The mapping is explicit:

- a timeout maps to `external_operation_timeout`;
- a connection or protocol failure maps to `external_operation_transport_failed`;
- a non-2xx HTTP status maps to `external_operation_unexpected_status`;
- an unparseable body, a missing required field, a `status` value outside
  `Succeeded`, `Failed`, or `Rejected`, or a remote `errorCode` that begins with
  `external_operation_` maps to `external_operation_invalid_response`.

A well-formed remote execute result passes its `status`, `message`, `errorCode`,
and `output` through unchanged, and the bridge sets `startedAtUtc` and
`completedAtUtc` from its own call timing. Caller cancellation propagates
`OperationCanceledException`; only the bridge's own timeout maps to
`external_operation_timeout`.

A status call that fails produces a `Warning` snapshot instead of throwing. A
status call fails when it times out, fails at the transport layer, returns a
non-2xx response, or returns a body that does not deserialize to a
`StatusSnapshot` with known `StatusLevel` values. The `Warning` snapshot carries
the fixed signal id `status-call-failed`. Every returned snapshot gets
`providerId` `{name}.{remoteId}` and `projectId` set to the requested project id.
`GetStatusAsync` propagates caller cancellation and does not treat it as a failed
status call.

A remote validate call that fails returns one error string from `ValidateAsync`
instead, with the format `"<code>: <detail>"`, so the workbench rejects the
execution with `operation_validation_failed` and does not call execute. It uses
the same condition-to-code mapping as execute.

Alternatives considered: retrying a failed call was rejected because the first
slice must keep side effects bounded and the maintainer settled on no retry.
Infinite or per-call configured timeouts were rejected because a fixed bound is
simpler to reason about.

### Keep confirmation metadata and identity forwarding out of scope

A remote `requiresConfirmation` value maps to
`OperationDefinition.RequiresConfirmation` as metadata, exactly like the
built-ins. The host does not enforce it. The host does not forward actor
identity, a correlation id, or credentials, and it does not authenticate an
external call.

Operators must run external plugins on a trusted network only until #57. The
bridge disables trace-context propagation on its named bridge `HttpClient`,
registered with `AddHttpClient("<name>")`, by configuring the primary handler
with `ConfigurePrimaryHttpMessageHandler` and setting
`SocketsHttpHandler.ActivityHeadersPropagator` to a no-output propagator. It
applies the same setting to the short-lived startup client. Built-in plugins are
not affected. An external request therefore carries no actor identity,
credentials, `traceparent`, `tracestate`, or other correlation header. Without
that setting the .NET `HttpClient` pipeline would inject W3C `traceparent` and
`tracestate` whenever an ambient `Activity` exists, and ASP.NET Core creates one
per request.

Alternatives considered: forwarding the initiator or correlation id was rejected
because #57 owns the identity contract and the first slice must not invent one.
Adding a plugin-specific authentication scheme was rejected for the same reason.

### Verify with a fabricated plugin process

The end-to-end tests start a fabricated external plugin as a separate Kestrel
process. The tests cover observe, confirm, and act through the bridge, including
that `requiresConfirmation` appears in the REST project details and the operation
is reachable through the same REST path as built-ins, an unreachable plugin,
invalid capabilities at startup, a duplicate name, an excluded id, an execute
timeout recorded in execution history with `external_operation_timeout`, and a
validate failure. The tests make no live external calls. The change also adds
`.http` examples for the bridge and the fixture plugin and a short authoring
guide in `docs/`.

Alternatives considered: a mocked `HttpMessageHandler` alone was rejected
because it does not exercise the real HTTP transport, the fixture plugin
process, or startup discovery. It is still useful for focused unit tests.

## Risks / Trade-offs

- **Blocking startup:** the synchronous capabilities fetch can add up to 10
  seconds per plugin to startup. This is a known ceiling of the first slice.
  Mitigate by keeping the configured plugin count small and by treating a
  restart as the way to apply changes. A future change can move discovery off
  the startup path if needed.
- **Trust boundary:** the slice has no authentication and forwards no
  credentials, so any reachable plugin can serve arbitrary capabilities. Run
  external plugins on a trusted network only until #57.
- **Breaking registration contract:** instance and factory registration and
  descriptor-based catalogs change an accepted contract (ADR-0001). No
  backward-compatibility is required because the project is new, but every
  built-in plugin and the catalog consumers must be updated together.
- **Remote failure modes:** the host now depends on network calls for status and
  operations. A failed call is bounded and mapped to a stable result, and
  startup stays available when a plugin is unreachable, but a slow remote plugin
  adds latency to an operation.
- **No retry:** a transient remote failure fails the operation. This is
  intentional for the first slice; a caller can retry the operation.
- **Unknown remote outcome on timeout:** `external_operation_timeout` means the
  remote outcome is unknown. The remote may complete its side effect after the
  bridge timeout while execution history records `Failed`.
- **Cancelled execute:** a cancelled execute propagates
  `OperationCanceledException` and writes no history row, even if the remote side
  effect ran.
- **Response size:** the slice sets no cap on a remote response body.
  Response-size limits are deferred to #57.
- **Excluded remote ids:** excluding an invalid remote id hides a plugin
  capability. The `Warning` on `{name}.bridge` makes the exclusion visible.
- **No version negotiation:** a plugin and the host share one contract version.
  A future contract change needs a versioning decision.
- **Test fixture drift:** the fixture plugin can drift from the OpenAPI
  contract. Keep the fixture and the OpenAPI document in the same change and
  verify them together.

## Migration Plan

No database migration is required. Add a committed placeholder for the external
plugin configuration with an empty plugin list. An empty list registers no
external plugins and changes no existing behavior. The implementation adds the
`ControlPlane.ExternalPlugins` project and updates the registration contract and
the built-in plugins in one coordinated change. Deployment and activation
against live plugins are outside this proposal. Sequence the implementation
after #6 merges, because both changes touch `ServiceCollectionExtensions.cs` and
`Program.cs`.

## Open Questions

- Should the host limit how many external plugins or capabilities it accepts?
  The maintainer deferred limits to #57.
- Should a remote status read be cached across concurrent overview callers, as
  the HTTP endpoint checks capability does? This change does not add caching;
  each overview read calls the remote provider.
- Should the fixture plugin ship inside an existing test project or in a new
  test project? This is an implementation detail that does not change the
  behavior specified here.
- When a second contract version exists, how should the host select a version?
  Version negotiation is deferred by this change.
