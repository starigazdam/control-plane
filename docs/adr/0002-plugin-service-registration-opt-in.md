# ADR-0002: Let plugins opt into service registration

**Date:** 2026-09-01

**Status:** Accepted

## Context

Some plugins need their own dependencies and configuration bindings, while
simple plugins should not pay for a more complex integration contract.

## Decision

Keep `IControlPlanePlugin` focused on capability registration. A plugin that
needs dependency-injection setup may additionally implement
`IPluginServiceRegistration`, which supplies an explicit opt-in service
registration extension.

## Alternatives considered

- Requiring service registration from every plugin was rejected because it adds
  ceremony to plugins without services.
- Allowing arbitrary host-level registration outside a dedicated contract was
  rejected because ownership and startup behavior would be harder to inspect.

## Consequences

The base plugin contract stays small, while plugins with dependencies have a
clear, discoverable extension point. Plugin authors must only use the optional
contract when service setup is actually required.

## Links

- Issue: #29
- Related ADR: ADR-0001
