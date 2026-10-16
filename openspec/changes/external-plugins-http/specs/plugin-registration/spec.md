# Spec Delta

## MODIFIED Requirements

### Requirement: Register plugin capabilities through IPluginRegistration

Plugins SHALL register status providers and operations through
`IPluginRegistration`. The contract SHALL support type, instance, and factory
registration: `AddStatusProvider<TStatusProvider>` and
`AddOperation<TOperation>` for types, an instance overload
`AddStatusProvider(IStatusProvider)` or `AddOperation(IOperation)`, and a
factory overload `AddStatusProvider(Func<IServiceProvider, IStatusProvider>)` or
`AddOperation(Func<IServiceProvider, IOperation>)`. The concrete
`PluginRegistration` SHALL collect a descriptor for each registration so one
plugin can register many status providers and many operations. The host SHALL
record a repeated type registration once, and SHALL record each instance
registration and each factory registration separately.

#### Scenario: Plugin registers a status provider and an operation
- **WHEN** a plugin calls `AddStatusProvider<TStatusProvider>` and `AddOperation<TOperation>`
- **THEN** the host collects a type descriptor for each capability

#### Scenario: Plugin registers the same type twice
- **WHEN** a plugin calls `AddStatusProvider<TStatusProvider>` more than once with the same type
- **THEN** the host records that type once

#### Scenario: Plugin registers many capabilities as instances or factories
- **WHEN** a plugin registers N status provider instances or factories
- **THEN** the host collects N descriptors

### Requirement: Resolve status providers and operations into the container

The host SHALL register each collected descriptor as its capability contract. A
type descriptor SHALL register as a transient `IStatusProvider` or a transient
`IOperation`. An instance descriptor SHALL register its instance as a singleton.
A factory descriptor SHALL register a singleton created by invoking its factory
with the `IServiceProvider`, so a factory can resolve services such as
`IHttpClientFactory`. The host SHALL also record a `DiscoveredPluginCatalog`
that holds the recorded plugin names and the capability descriptors instead of
type lists.

#### Scenario: Collected types become container registrations
- **WHEN** plugins have registered status providers and operations as types
- **THEN** the host registers each type descriptor as a transient `IStatusProvider` or `IOperation`

#### Scenario: Collected instance or factory descriptors become singletons
- **WHEN** a plugin has registered a status provider or operation as an instance or a factory
- **THEN** the host registers it as a singleton

#### Scenario: Catalog reflects discovery
- **WHEN** discovery completes
- **THEN** the `DiscoveredPluginCatalog` holds the recorded plugin names and capability descriptors
