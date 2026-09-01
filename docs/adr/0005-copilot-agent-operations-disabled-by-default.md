# ADR-0005: Disable Copilot agent operations by default

**Date:** 2026-09-01

**Status:** Accepted

## Context

The Copilot Agent plugin can expose operations that invoke an external command
line agent. Loading the plugin should not itself activate those operations in
every environment.

## Decision

Load the Copilot Agent plugin while defaulting `CopilotAgent__Enabled` to
`false`. Operators explicitly enable it through local configuration when they
intend to activate agent operations.

## Alternatives considered

- Enabling operations whenever the plugin loads was rejected because the
  activation boundary would be implicit.
- Removing the plugin when disabled was rejected because availability and
  configuration state should remain observable.

## Consequences

The default configuration is safe and predictable. Users who need the feature
must deliberately opt in, and status/operation responses can explain how to
enable it.

## Links

- Issue: #29
- Related code: `src/ControlPlane.CopilotAgent/CopilotAgentSettings.cs`
