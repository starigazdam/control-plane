# Proposal

## Why

Control Plane plugins must currently be compiled into the host. The host
discovers plugins only from assemblies supplied to `AddControlPlanePlugins`, so
an operator cannot add a plugin without changing and redeploying the host.
Issue #56 asks for external plugins over HTTP as a first slice. Separately
deployed plugins let the host consume new operational capabilities while it
keeps the existing plugin model.

## What Changes

- Add out-of-process external plugins over HTTP and JSON only. The consumer
  deploys and operates each plugin process. The host never spawns, supervises,
  or restarts a plugin.
- Add one bridge plugin project, `ControlPlane.ExternalPlugins`, that normalizes
  the wire contract to `IStatusProvider` and `IOperation`. Adapters for custom
  consumer APIs stay in the consumer's process, never in this repository.
- Read a static configuration list of plugin entries, each with a `name` and a
  `baseUrl`. Load the plugin set at startup only. A restart applies changes. Do
  not add hot reload.
- Fetch `GET /capabilities` once per plugin during registration, synchronously,
  with a 10-second timeout. Expose the wire endpoints `GET /capabilities`,
  `GET /status/{id}?projectId={projectId}`, `POST /operations/{id}/validate`,
  and `POST /operations/{id}/execute`.
- Use the plugin `name` as a namespace. Expose operations and status providers
  as `{name}.{remoteId}`. Fail startup on a duplicate or invalid name. Exclude a
  remote id that violates the id grammar and report it as a `Warning` signal.
- Keep the host available when a plugin is unreachable, returns a non-2xx
  response, or returns malformed capabilities at startup. Register one status
  provider `{name}.bridge` that reports `Warning`, register no operations for
  that plugin, and still start.
- Bound every call at a fixed 10-second timeout with no retry. Map a failed
  operation call to an `OperationExecutionResult` with `Failed` and a stable
  `ErrorCode`, recorded through the existing execution history path. Map a
  failed status call to a `Warning` snapshot.
- Change `IPluginRegistration` to support instance and factory registration so
  one plugin can register many remote status providers and operations. Change
  `DiscoveredPluginCatalog` from `Type` lists to descriptors. The project is
  new, so there is no backward-compatibility requirement; update built-in
  plugins as needed.
- Map remote `requiresConfirmation` to `OperationDefinition.RequiresConfirmation`
  as metadata only, exactly like the built-ins.
- Do not forward actor identity, a correlation id, or credentials in this
  slice, and add no authentication. Document that operators must run external
  plugins on a trusted network until #57.
- Publish the wire contract as OpenAPI in `docs/` at implementation time. Add a
  fabricated fixture plugin that runs as a separate Kestrel process in tests,
  `.http` examples for the bridge and the fixture, and a short authoring guide.

## Capabilities

### New Capabilities

- `external-plugins`: Consume separately deployed HTTP and JSON plugins as
  status providers and operations, namespace their ids, bound and report their
  calls, and keep the host available when a plugin is unreachable or invalid.

### Modified Capabilities

- `plugin-registration`: The registration contract supports instance and factory
  registration and records capability descriptors instead of only types, so one
  plugin can expose many remote status providers and operations.

## Impact

- New project `ControlPlane.ExternalPlugins` with its settings and configuration
  binding. Proposed modifications to
  `src/ControlPlane.Core/Plugins/IPluginRegistration.cs`,
  `src/ControlPlane.Api/Infrastructure/PluginRegistration.cs`,
  `src/ControlPlane.Api/Infrastructure/DiscoveredPluginCatalog.cs`,
  `src/ControlPlane.Api/Infrastructure/ServiceCollectionExtensions.cs`, and
  `src/ControlPlane.Api/Program.cs`. Built-in plugins are updated only as the
  changed contract requires.
- New wire contract documentation published as OpenAPI under `docs/`, a short
  authoring guide under `docs/`, `.http` examples for the bridge and the fixture
  plugin, and a fixture external plugin that runs as a separate Kestrel process
  in tests.
- APIs: the host exposes no new endpoint. External plugins serve
  `GET /capabilities`, `GET /status/{id}?projectId={projectId}`,
  `POST /operations/{id}/validate`, and `POST /operations/{id}/execute`.
- No database schema change. Execution results use the existing history path.
- Out of scope: security in all forms (authentication, actor and correlation
  forwarding, confirmation enforcement, limits, redaction) is tracked by #57.
  Declarative views are tracked by #58. Contract versioning is deferred until a
  second contract version exists. Named pipe and stdio transport, hot reload,
  process supervision, SDK packages, remote ES modules, and per-plugin adapters
  in this repository are out of scope.
- No ADR. This repository moves rationale into OpenSpec (#55), so the rationale
  lives in `design.md`.
- Keep issue #56 open until the implementation is merged.
- This change touches `ServiceCollectionExtensions.cs` and `Program.cs`, which
  open work for #6 also changes. Sequence the implementation after #6 merges.
