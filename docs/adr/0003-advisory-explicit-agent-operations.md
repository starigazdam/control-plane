# ADR-0003: Keep agent-facing operations advisory and explicit

**Date:** 2026-09-01

**Status:** Accepted

## Context

Control Plane presents operational visibility and actions. Agent-facing
suggestions must remain understandable and controlled rather than silently
executing commands with side effects.

## Decision

Expose concrete, named operations that return advisory information or require
an explicit operation invocation. Do not automatically execute commands merely
because an agent suggests them.

## Alternatives considered

- Automatically executing suggested commands was rejected because it obscures
  side effects and removes an intentional action boundary.
- Generic operation builders were rejected in favor of concrete operations so
  each action remains specific and reviewable.

## Consequences

New operations must make their behavior and side effects explicit. The product
can support useful guidance without representing suggestions as automatic
execution authority.

## Links

- Issue: #29
- [Plugin philosophy](../plugin-philosophy.md)
