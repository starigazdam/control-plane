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

Each configured plugin serves endpoints relative to its `baseUrl`. The bridge
maps them to the Core contracts in camelCase.

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
    "errorCode": "external_operation_timeout",
    "output": { "<name>": "<value>" }
  }
  ```

`StatusLevel` and `OperationExecutionStatus` serialize as strings, matching the
host's existing `JsonStringEnumConverter`. The implementation publishes this
contract as OpenAPI in `docs/`. This change specifies that publication; it does
not write the OpenAPI file now.

Alternatives considered: a hand-written request model per operation was rejected
because it duplicates the Core records. A binary or streaming transport was
rejected because it adds complexity without a first-slice need.

### Add one bridge plugin project

One bridge plugin project, `ControlPlane.ExternalPlugins`, hosts the bridge. It
reads configuration, fetches capabilities, and creates one bridge status
provider or operation per remote capability. Adapters for custom consumer APIs
live in the consumer's process, never in this repository.

Alternatives considered: a separate bridge assembly per remote plugin was
rejected because it multiplies projects without adding a boundary. Letting the
host call remote plugins directly was rejected because it would put transport
concerns in the host and `ControlPlaneWorkbenchService`.

### Extend the registration contract with descriptors

`IPluginRegistration` gains an instance overload and a factory overload for each
capability kind: `AddStatusProvider(IStatusProvider)`,
`AddStatusProvider(Func<IStatusProvider>)`, `AddOperation(IOperation)`, and
`AddOperation(Func<IOperation>)`. `PluginRegistration` collects a descriptor for
each registration instead of a `Type` set. Type descriptors keep the current
deduplication by type. Instance and factory descriptors are recorded separately,
so one plugin can register N remote capabilities.

`DiscoveredPluginCatalog` changes from `Type` lists to descriptor lists. The
host registers a type descriptor as a transient `IStatusProvider` or
`IOperation`, an instance descriptor as a singleton, and a factory descriptor
as a singleton created from its factory. Built-in plugins are updated only as
the changed contract requires. There is no backward-compatibility requirement
because the project is new.

Alternatives considered: keeping `Type`-only registration and constructing each
remote bridge through DI was rejected because the bridge must capture per-remote
values, such as the remote id and the plugin `baseUrl`, which a parameterless or
DI-resolved type cannot express. Registering every bridge as a type with a
custom constructor was rejected for the same reason.

### Fetch capabilities once at startup

The bridge fetches `GET /capabilities` once per plugin during registration,
synchronously, each call bounded by a 10-second timeout. Successful discovery
creates the namespaced bridges. Failure discovery registers one status provider
`{name}.bridge` that reports `Warning`, registers no operations, and lets the
host start.

Alternatives considered: lazy discovery on first use was rejected because it
moves a network call onto the request path and makes operations appear and
disappear. Background discovery was rejected because it complicates startup and
the first slice does not need it.

### Namespace and validate ids

A plugin `name` is 1-32 lowercase ASCII letters, digits, or hyphens, starting
with a letter. The name is the namespace, so the host exposes
`{name}.{remoteId}` for each operation and status provider. A duplicate name
fails startup. An invalid name fails startup. A remote id must be 1-64 ASCII
letters, digits, dots, underscores, or hyphens; any capability that violates
that grammar is excluded and reported as a `Warning` signal.

Alternatives considered: case-insensitive or unvalidated names were rejected
because ids must be stable and predictable. Allowing arbitrary remote ids was
rejected because an exposed id must compose with the host's existing lookup.

### Bound calls and map failures to stable results

Each call to an external plugin uses a fixed 10-second timeout with no retry. A
remote operation call that times out, fails at the transport layer, returns a
non-2xx response, or returns a malformed response produces an
`OperationExecutionResult` with status `Failed` and a stable error code. The
host records that result through the existing execution history path. A failed
remote status call produces a `Warning` snapshot.

Stable error codes are `external_operation_timeout`,
`external_operation_transport_failed`, `external_operation_unexpected_status`,
and `external_operation_invalid_response`. These are local codes that the bridge
produces when the remote call fails; a remote plugin's own error code is not
trusted as a host code.

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
wire request payload carries only a project id and input, so identity forwarding
is not possible by construction.

Alternatives considered: forwarding the initiator or correlation id was rejected
because #57 owns the identity contract and the first slice must not invent one.
Adding a plugin-specific authentication scheme was rejected for the same reason.

### Verify with a fabricated plugin process

The end-to-end tests start a fabricated external plugin as a separate Kestrel
process. The tests cover observe through act through the bridge, an unreachable
plugin, a duplicate name, and a call timeout. The tests make no live external
calls. The change also adds `.http` examples for the bridge and the fixture
plugin and a short authoring guide in `docs/`.

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
- **Excluded remote ids:** excluding an invalid remote id hides a plugin
  capability. The `Warning` signal makes the exclusion visible.
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
