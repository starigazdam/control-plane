# Tasks

All tasks below are unstarted. This is a proposal-only plan; nothing here is
authorized for implementation, and every checkbox stays unchecked.

## 1. Change the plugin registration contract

- [ ] 1.1 Add instance and factory overloads to `IPluginRegistration` and collect a descriptor for each registration in `PluginRegistration`, keeping type deduplication. The factory overloads take `Func<IServiceProvider, IStatusProvider>` and `Func<IServiceProvider, IOperation>`; verify that N instance or factory registrations produce N descriptors and a repeated type registration produces one.
- [ ] 1.2 Change `DiscoveredPluginCatalog` from `Type` lists to descriptor lists and update `AddControlPlanePlugins` so a type descriptor registers as transient and an instance or factory descriptor registers as a singleton created by invoking its factory with the service provider; verify `dotnet build src/ControlPlane.slnx` succeeds and the existing plugin tests pass.
- [ ] 1.3 Update the built-in plugins to the changed registration contract only where required; verify every built-in plugin still registers and the host starts.
- [ ] 1.4 Change `IPluginServiceRegistration.RegisterServices` to `RegisterServices(IServiceCollection services, IConfiguration configuration)`, pass the application `IConfiguration` from `AddControlPlanePlugins`, and update each built-in plugin that implements the interface; verify the host starts and each such plugin still registers its services.
- [ ] 1.5 Fail startup with a clear error when two registered operations, or two registered status providers, built-in or external, expose ids that are equal under ordinal case-insensitive comparison; verify the duplicate-id check rejects each collision case and the host starts when all ids are unique.

## 2. Add the external plugins bridge

- [ ] 2.1 Add the `ControlPlane.ExternalPlugins` project with its settings type, configuration binding, and solution and host references. Implement `IPluginServiceRegistration` so the bridge binds its section from the passed `IConfiguration`, calls `services.AddHttpClient()`, and keeps the configured plugin list for its `Register` call; verify `dotnet build src/ControlPlane.slnx` succeeds.
- [ ] 2.2 Parse the static plugin list of `name` and `baseUrl` entries, validate the name grammar, require an absolute `http` or `https` `baseUrl`, and fail startup on an invalid name, a duplicate name, or a missing or invalid `baseUrl`; verify each failure case stops startup with a clear error that names the setting.
- [ ] 2.3 Fetch `GET /capabilities` once per plugin during registration with a 10-second timeout and one short-lived `HttpClient` because the container does not exist yet, and parse status provider ids and operation definitions. Treat a response that is not JSON or whose top-level shape omits `statusProviderIds` or `operations` as invalid capabilities; verify a fabricated plugin returns capabilities and a stalled plugin times out.
- [ ] 2.4 Exclude a remote id that violates the id grammar (including a first character that is not a letter or digit), the reserved status provider id `bridge`, every entry of a duplicate status provider or operation id group (ordinal case-insensitive), an operation entry with a missing or wrongly typed required field or an invalid parameter entry, and a non-string `statusProviderIds` element. Report each exclusion as a `Warning` on `{name}.bridge`; verify each exclusion registers no capability and produces the `Warning`.
- [ ] 2.5 Always register `{name}.bridge`: report `Healthy` on clean discovery and `Warning` with the fixed signal id `unreachable`, the fixed signal id `invalid-capabilities`, or the excluded capabilities otherwise. The provider reflects startup discovery only and does not change after startup. On an unreachable, non-2xx, or invalid capabilities response register no operations and keep the host up; verify the host starts and the overview shows the `Warning`.
- [ ] 2.6 Implement the bridge adapters that normalize remote status providers to `IStatusProvider` and remote operations to `IOperation`, namespaced as `{name}.{remoteId}`; verify the exposed ids and definitions in a focused test.
- [ ] 2.7 Implement the wire calls `GET /status/{id}?projectId={projectId}`, `POST /operations/{id}/validate`, and `POST /operations/{id}/execute` with the design payloads, resolving `IHttpClientFactory` for runtime calls. Use the remote id captured at discovery, percent-encoded as one path segment, in each path, and percent-encode `projectId`; verify requests and responses match the design mapping.

## 3. Bound calls and map failures

- [ ] 3.1 Apply a fixed 10-second timeout to each remote call and implement no retry; verify a stalled call times out and sends no retry, and that caller cancellation propagates `OperationCanceledException` without mapping to `external_operation_timeout`.
- [ ] 3.2 Map each execute failure to its stable bridge code: timeout to `external_operation_timeout`, transport error to `external_operation_transport_failed`, non-2xx response to `external_operation_unexpected_status`, and unparseable body, missing required field, `status` outside `Succeeded`/`Failed`/`Rejected`, or a remote `errorCode` beginning with `external_operation_` to `external_operation_invalid_response`. Record the result through the existing execution history path, pass a well-formed remote result's `status`, `message`, `errorCode`, and `output` through unchanged, and set `startedAtUtc`/`completedAtUtc` from the bridge's own call timing; verify each code, the pass-through, the timestamp handling, and the recorded history entry.
- [ ] 3.3 Map a failed remote status call to a `Warning` snapshot without throwing, and set `providerId` to `{name}.{remoteId}` and `projectId` to the requested project id on every returned snapshot; verify the snapshot level, the fixed signal id, the identity fields, and that a failed call never throws.
- [ ] 3.4 Map remote `requiresConfirmation` to `OperationDefinition.RequiresConfirmation` as metadata only. Disable trace-context propagation on the bridge `HttpClient` and forward no actor identity, credentials, `traceparent`, `tracestate`, or other correlation header; verify the outgoing request headers and body and the exposed definition.
- [ ] 3.5 Return one error string from `ValidateAsync` in the format `"<code>: <detail>"` when a remote validate call times out, fails at the transport layer, returns a non-2xx response, or returns a malformed body, so the workbench rejects with `operation_validation_failed` and does not call execute; verify each bridge code, the string format, and that execute is not called.

## 4. Verify the slice end to end

- [ ] 4.1 Add a fabricated external plugin that runs as a separate Kestrel process in tests; verify observe -> confirm -> act works through the bridge, including that `requiresConfirmation` appears in the REST project details and the operation is reachable through the same REST path as built-ins.
- [ ] 4.2 Cover an unreachable plugin, invalid capabilities at startup, a duplicate name, an excluded id, an execute timeout recorded in execution history with `external_operation_timeout`, and a validate failure in the end-to-end tests; verify each behaves as specified with no live external system.
- [ ] 4.3 Add `.http` examples for the bridge and the fixture plugin and a short authoring guide in `docs/`; verify `python3 scripts/check_plugin_http_examples.py` passes.
- [ ] 4.4 Publish the wire contract as OpenAPI in `docs/`; verify an automated test deserializes each fixture response against the OpenAPI schema.
- [ ] 4.5 Document the trusted-network boundary and the #57 dependency in the authoring guide; verify the authoring guide, the `.http` files, the OpenAPI `servers` entries, and the committed configuration placeholder contain no hostnames, addresses, credentials, or private infrastructure references beyond `localhost` or `example.invalid`.
- [ ] 4.6 Run `dotnet build src/ControlPlane.slnx`, the focused tests, the full available test suite, `npm run lint --prefix ui`, `npm run build --prefix ui`, and the documented OpenSpec and plugin-example checks; record environment limits separately.
- [ ] 4.7 At archive, amend the baseline `openspec/specs/plugin-registration/spec.md` Purpose to state that external out-of-process plugins are consumed through the assembly-supplied `ControlPlane.ExternalPlugins` bridge, specified in `openspec/specs/external-plugins/spec.md`, and that no in-process dynamic assembly loader exists.

## Workflow follow-up

- Obtain an independent high-capability review of this exact proposal revision. Resolve blocking findings and re-review a material change.
- Obtain maintainer design approval before implementation. This proposal does not authorize implementation, merge, deployment, or archive.
- Start implementation only under a separate explicit instruction. Never create the initial proposal and implement it in the same worker run.
- Keep issue #56 open until the implementation is merged.
- Sequence the implementation after #6 merges, because the #56 implementation touches `ServiceCollectionExtensions.cs` and `Program.cs`, which open work for #6 also changes.
