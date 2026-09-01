# ADR-0001: Use a plugin registration contract

**Date:** 2026-09-01

**Status:** Accepted

## Context

Control Plane integrates multiple operational domains while keeping each
feature slice independently discoverable and composable. A host-owned contract
is needed so a plugin can expose its status providers and operations without
coupling the host to each concrete plugin.

## Decision

Define plugins through `IControlPlanePlugin` and have each plugin register its
capabilities through `IPluginRegistration`. The host discovers plugin types and
invokes the registration contract during startup.

## Alternatives considered

- Hard-coding every plugin in the host was rejected because it makes the host
  change whenever a domain plugin is added.
- Letting plugins modify host internals directly was rejected because it would
  weaken the boundary between the host and plugin slices.

## Consequences

Plugins have a consistent integration point and the host retains ownership of
registration. Changes to either contract are architectural changes and should
consider compatibility across all plugins.

## Links

- Issue: #29
- Related code: `src/ControlPlane.Core/Plugins/`
