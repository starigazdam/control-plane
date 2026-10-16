# Tasks

All tasks below are unstarted. This is a proposal-only plan; nothing here is
authorized for implementation, and every checkbox stays unchecked.

## 1. Change the plugin registration contract

- [ ] 1.1 Add instance and factory overloads to `IPluginRegistration` and collect a descriptor for each registration in `PluginRegistration`, keeping type deduplication; verify that N instance or factory registrations produce N descriptors and a repeated type registration produces one.
- [ ] 1.2 Change `DiscoveredPluginCatalog` from `Type` lists to descriptor lists and update `AddControlPlanePlugins` so a type descriptor registers as transient and an instance or factory descriptor registers as a singleton; verify `dotnet build src/ControlPlane.slnx` succeeds and the existing plugin tests pass.
- [ ] 1.3 Update the built-in plugins to the changed registration contract only where required; verify every built-in plugin still registers and the host starts.

## 2. Add the external plugins bridge

- [ ] 2.1 Add the `ControlPlane.ExternalPlugins` project with its settings type, configuration binding, and solution and host references; verify `dotnet build src/ControlPlane.slnx` succeeds.
- [ ] 2.2 Parse the static plugin list of `name` and `baseUrl` entries, validate the name grammar, and fail startup on an invalid name or a duplicate name; verify each failure case stops startup with a clear error that names the setting.
- [ ] 2.3 Fetch `GET /capabilities` once per plugin during registration with a 10-second timeout, and parse status provider ids and operation definitions; verify a fabricated plugin returns capabilities and a stalled plugin times out.
- [ ] 2.4 Exclude a remote id that violates the id grammar and report each exclusion as a `Warning` signal; verify an invalid remote id registers no capability and produces the `Warning`.
- [ ] 2.5 On an unreachable, non-2xx, or malformed capabilities response, register `{name}.bridge` reporting `Warning` with `unreachable` or `invalid capabilities`, register no operations, and keep the host up; verify the host starts and the overview shows the `Warning`.
- [ ] 2.6 Implement the bridge adapters that normalize remote status providers to `IStatusProvider` and remote operations to `IOperation`, namespaced as `{name}.{remoteId}`; verify the exposed ids and definitions in a focused test.
- [ ] 2.7 Implement the wire calls `GET /status/{id}?projectId={projectId}`, `POST /operations/{id}/validate`, and `POST /operations/{id}/execute` with the design payloads; verify requests and responses match the design mapping.

## 3. Bound calls and map failures

- [ ] 3.1 Apply a fixed 10-second timeout to each remote call and implement no retry; verify a stalled call times out and sends no retry.
- [ ] 3.2 Map a timeout, transport error, non-2xx response, or malformed response to an `OperationExecutionResult` with status `Failed` and a stable `ErrorCode`, recorded through the existing execution history path; verify each code and the recorded history entry.
- [ ] 3.3 Map a failed remote status call to a `Warning` snapshot; verify the snapshot level and signal.
- [ ] 3.4 Map remote `requiresConfirmation` to `OperationDefinition.RequiresConfirmation` as metadata only, and forward no actor identity, correlation id, or credentials; verify the outgoing request body and the exposed definition.

## 4. Verify the slice end to end

- [ ] 4.1 Add a fabricated external plugin that runs as a separate Kestrel process in tests; verify observe through act works through the bridge.
- [ ] 4.2 Cover an unreachable plugin, a duplicate name, and a call timeout in the end-to-end tests; verify each behaves as specified with no live external system.
- [ ] 4.3 Add `.http` examples for the bridge and the fixture plugin and a short authoring guide in `docs/`; verify `python3 scripts/check_plugin_http_examples.py` passes.
- [ ] 4.4 Publish the wire contract as OpenAPI in `docs/`; verify the document matches the design payloads and the fixture plugin.
- [ ] 4.5 Document the trusted-network boundary and the #57 dependency in the authoring guide; verify the guide contains no hostnames, addresses, credentials, or private infrastructure references.
- [ ] 4.6 Run `dotnet build src/ControlPlane.slnx`, the focused tests, the full available test suite, `npm run lint --prefix ui`, `npm run build --prefix ui`, and the documented OpenSpec and plugin-example checks; record environment limits separately.

## Workflow follow-up

- Obtain an independent high-capability review of this exact proposal revision. Resolve blocking findings and re-review a material change.
- Obtain maintainer design approval before implementation. This proposal does not authorize implementation, merge, deployment, or archive.
- Start implementation only under a separate explicit instruction. Never create the initial proposal and implement it in the same worker run.
- Keep issue #56 open until the implementation is merged.
- Sequence the implementation after #6 merges, because the #56 implementation touches `ServiceCollectionExtensions.cs` and `Program.cs`, which open work for #6 also changes.
