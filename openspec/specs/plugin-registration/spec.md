# Plugin Registration Specification

## Purpose

Describe how the host registers plugins today. This baseline covers the
assembly-driven discovery and registration that exists in the inspected source.
It does not describe, and must not be read as promising, a dynamic plugin loader,
automatic plugin scanning outside the supplied assemblies, project-scoped
plugin enablement, agent-tool projection, or typed operation schemas.

## Requirements

### Requirement: Discover concrete plugins from supplied assemblies

The host SHALL discover plugins only from the assemblies supplied to
`AddControlPlanePlugins`, and SHALL consider only concrete types that implement
`IControlPlanePlugin`.

#### Scenario: Supplied assembly defines a concrete plugin

- **WHEN** a supplied assembly defines a non-abstract class that implements `IControlPlanePlugin`
- **THEN** the host includes that type in the discovered plugin set

#### Scenario: Type does not implement the plugin contract

- **WHEN** a supplied assembly defines a type that does not implement `IControlPlanePlugin`
- **THEN** the host excludes that type from the discovered plugin set

#### Scenario: Type is abstract or an interface

- **WHEN** a supplied assembly defines an abstract class or an interface
- **THEN** the host excludes that type even if it is assignable to `IControlPlanePlugin`

#### Scenario: No assembly is supplied

- **WHEN** the caller passes no assemblies
- **THEN** the host discovers no plugins and registers no plugin capabilities

### Requirement: Instantiate each discovered plugin and record its name

The host SHALL create each discovered plugin through `Activator.CreateInstance`
and SHALL throw `InvalidOperationException` when the created instance is not an
`IControlPlanePlugin`. For each plugin, the host SHALL record the value of the
plugin's `Name` property.

#### Scenario: Plugin instantiates successfully

- **WHEN** a discovered plugin type has a usable parameterless constructor
- **THEN** the host instantiates it and records its `Name`

#### Scenario: Plugin cannot be instantiated as the contract

- **WHEN** instantiation does not yield an `IControlPlanePlugin`
- **THEN** the host throws `InvalidOperationException`

### Requirement: Register plugin capabilities through IPluginRegistration

Plugins SHALL register status providers and operations through
`IPluginRegistration` by calling `AddStatusProvider<TStatusProvider>` and
`AddOperation<TOperation>`. The concrete `PluginRegistration` SHALL collect the
registered types in sets so a type is recorded once.

#### Scenario: Plugin registers a status provider and an operation

- **WHEN** a plugin calls `AddStatusProvider<TStatusProvider>` and `AddOperation<TOperation>`
- **THEN** the host collects both concrete types for later registration

#### Scenario: Plugin registers the same type twice

- **WHEN** a plugin calls `AddStatusProvider<TStatusProvider>` more than once with the same type
- **THEN** the host records that type once

### Requirement: Allow optional plugin service registration before wiring

A plugin that implements `IPluginServiceRegistration` SHALL have
`RegisterServices(IServiceCollection)` invoked during discovery. That call SHALL
happen before `plugin.Register` is called and before the host wires status
providers and operations.

#### Scenario: Plugin opts into service registration

- **WHEN** a plugin implements `IPluginServiceRegistration`
- **THEN** the host invokes `RegisterServices` before `Register` and before status providers and operations are wired

#### Scenario: Plugin does not opt into service registration

- **WHEN** a plugin does not implement `IPluginServiceRegistration`
- **THEN** the host performs no plugin service registration for that plugin

### Requirement: Resolve status providers and operations into the container

The host SHALL register each collected status-provider type as a transient
`IStatusProvider` and each collected operation type as a transient `IOperation`.
The host SHALL also record a `DiscoveredPluginCatalog` with the plugin names, the
status-provider types, and the operation types.

#### Scenario: Collected types become container registrations

- **WHEN** plugins have registered status providers and operations
- **THEN** the host resolves each status-provider type as `IStatusProvider` and each operation type as `IOperation`

#### Scenario: Catalog reflects discovery

- **WHEN** discovery completes
- **THEN** the `DiscoveredPluginCatalog` contains the recorded plugin names, status-provider types, and operation types

## Evidence

Non-normative. Observations in this section come from source inspection unless a
named test is called out.

- Contracts: `src/ControlPlane.Core/Plugins/IControlPlanePlugin.cs`,
  `src/ControlPlane.Core/Plugins/IPluginRegistration.cs`,
  `src/ControlPlane.Core/Plugins/IPluginServiceRegistration.cs`.
- Host discovery and wiring: `src/ControlPlane.Api/Infrastructure/ServiceCollectionExtensions.cs`,
  `src/ControlPlane.Api/Infrastructure/PluginRegistration.cs`,
  `src/ControlPlane.Api/Infrastructure/DiscoveredPluginCatalog.cs`.
- Rationale: `docs/adr/0001-plugin-registration-contract.md` and
  `docs/adr/0002-plugin-service-registration-opt-in.md`.

No automated test in the inspected repository proves every scenario above.
Discovery and registration are verified by code inspection. The existing
`tests/ControlPlane.Api.E2E/ServiceBusDlqToQueueSliceTests.cs` exercises a plugin
through HTTP, but it does not isolate this registration contract.
