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
  `baseUrl`. Require `baseUrl` to be an absolute `http` or `https` URI with no
  query, fragment, or userinfo; fail
  startup with a clear error that names the setting when `baseUrl` is missing or
  invalid. Load the plugin set at startup only. A restart applies changes. Do not
  add hot reload.
- Fetch `GET /capabilities` once per plugin during registration, synchronously,
  with a 10-second timeout. Expose the wire endpoints `GET /capabilities`,
  `GET /status/{id}?projectId={projectId}`, `POST /operations/{id}/validate`,
  and `POST /operations/{id}/execute`. The startup fetch runs before the
  container exists, so it uses one short-lived `HttpClient`; runtime remote
  calls use `IHttpClientFactory`.
- Use the plugin `name` as a namespace. Expose operations and status providers
  as `{name}.{remoteId}`. Fail startup on a duplicate or invalid name, and fail
  startup when two registered operations, or two registered status providers,
  built-in or external, expose ids equal under ordinal case-insensitive
  comparison. Register one status provider `{name}.bridge` for every configured
  plugin. Exclude a remote id that violates the id grammar, the reserved status
  provider id `bridge` (ordinal case-insensitive), every entry of a duplicate id
  group (ordinal
  case-insensitive), a non-string `statusProviderIds` element, and an operation
  entry that omits its required string `id` or `displayName`, carries an invalid
  parameter entry, or carries a wrongly typed optional field, and an `operations`
  element that is not an object, and report each exclusion as a `Warning` on
  `{name}.bridge`.
- Keep the host available when a plugin is unreachable, returns a non-2xx
  response, or returns invalid capabilities at startup. The `{name}.bridge`
  provider reports `Healthy` when discovery succeeds with no exclusions, and
  `Warning` with the signal ids `unreachable` or `invalid-capabilities`, or the
  excluded capabilities otherwise. Register no operations for a failed plugin and
  still start.
- Bound every call at a fixed 10-second timeout with no retry. Map a failed
  execute call to an `OperationExecutionResult` with `Failed` and an explicit
  stable bridge code: timeout to `external_operation_timeout`, transport error
  to `external_operation_transport_failed`, non-2xx response to
  `external_operation_unexpected_status`, and unparseable body, missing required
  field, unknown `status`, or a remote `errorCode` beginning with
  `external_operation_` to `external_operation_invalid_response`. Record the
  result through the existing execution history path. Pass a well-formed remote
  execute result's `status`, `message`, `errorCode`, and `output` through
  unchanged, and set the timestamps from the bridge's own call timing. Return a
  stable bridge code from `ValidateAsync` when a remote validate call fails, so
  the workbench rejects with `operation_validation_failed` and does not execute.
  Map a failed status call to a `Warning` snapshot without throwing, with
  `providerId` `{name}.{remoteId}` and the requested `projectId`. Caller
  cancellation propagates `OperationCanceledException`; only the bridge's own
  timeout maps to `external_operation_timeout`.
- Change `IPluginRegistration` to support instance and factory registration so
  one plugin can register many remote status providers and operations. Factory
  overloads take `Func<IServiceProvider, IStatusProvider>` and
  `Func<IServiceProvider, IOperation>` and register as singletons, so a bridge
  can resolve `IHttpClientFactory`. Change `DiscoveredPluginCatalog` from `Type`
  lists to descriptors. Change `IPluginServiceRegistration.RegisterServices` to
  `RegisterServices(IServiceCollection services, IConfiguration configuration)`
  with no backward-compatible overload, so the bridge binds its section during
  registration, registers a named client with `services.AddHttpClient("<name>")`,
  and keeps its plugin list for
  its `Register` call. The project is new, so there is no backward-compatibility
  requirement; update built-in plugins as needed.
- Map remote `requiresConfirmation` to `OperationDefinition.RequiresConfirmation`
  as metadata only, exactly like the built-ins.
- Do not forward actor identity, a correlation id, or credentials in this
  slice, and add no authentication. Disable trace-context propagation on the
  named bridge `HttpClient`, registered with `AddHttpClient("<name>")`, and on
  the short-lived startup client, so a request carries no `traceparent`,
  `tracestate`, or other correlation header; built-in plugins are not affected.
  Document that operators must run external plugins on a trusted network until
  #57.
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
  registration and records capability descriptors, so one plugin can expose many
  remote status providers and operations. It changes
  `IPluginServiceRegistration.RegisterServices` to the
  `RegisterServices(IServiceCollection, IConfiguration)` signature with no
  backward-compatible overload, and it adds a uniqueness requirement that fails
  startup when two registered operations, or two registered status providers,
  built-in or external, expose ids that are equal under ordinal case-insensitive
  comparison.

## Impact

- New project `ControlPlane.ExternalPlugins` with its settings and configuration
  binding. Proposed modifications to
  `src/ControlPlane.Core/Plugins/IPluginRegistration.cs`,
  `src/ControlPlane.Core/Plugins/IPluginServiceRegistration.cs`,
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
