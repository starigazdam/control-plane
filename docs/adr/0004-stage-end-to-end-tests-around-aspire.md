# ADR-0004: Stage end-to-end tests around Aspire

**Date:** 2026-09-01

**Status:** Accepted

## Context

Every feature slice should eventually verify the complete observe-and-act
loop. The repository's engineering rules prefer .NET Aspire as the
end-to-end testing foundation, but that foundation is not yet first-class.

## Decision

Retain end-to-end coverage as a requirement for vertical slices and stage its
implementation around the adoption of .NET Aspire rather than introducing a
temporary parallel test architecture.

## Alternatives considered

- Dropping end-to-end coverage was rejected because unit-level checks alone do
  not verify the full operational loop.
- Adding an unrelated temporary test framework was rejected because it would
  create a migration burden when Aspire becomes the standard foundation.

## Consequences

Current work must identify the deferred coverage and return to it when Aspire
support is available. The project avoids treating the temporary absence of an
end-to-end harness as permission to lower the long-term quality bar.

## Links

- Issue: #29
- [Engineering rules](../engineering-rules.md)
