# Architecture Decision Records

Architecture Decision Records (ADRs) capture significant, durable choices in
Control Plane. They preserve context, alternatives, and trade-offs that would
otherwise be scattered across issues and pull requests.

## When to write an ADR

Create an ADR for a decision with durable, cross-cutting impact on plugin
contracts, service composition, operation safety, test architecture, security,
or public project boundaries. Routine implementation details belong in issues,
pull requests, and code documentation.

## Status lifecycle

- **Proposed**: under discussion.
- **Accepted**: governs the project.
- **Superseded**: replaced by a newer ADR; link to the replacement.
- **Deprecated**: no longer applicable without a direct replacement.

Do not rewrite an accepted ADR to reverse a decision. Create a new ADR and
link it to the earlier record.

## Naming

Use a zero-padded, monotonic identifier followed by a short lowercase slug,
for example `0001-plugin-contracts.md`. Never reuse a number.

## Template

- [ADR template (not a decision record)](0000-template.md)

## Decision records

- [ADR-0001: Use a plugin registration contract](0001-plugin-registration-contract.md)
- [ADR-0002: Let plugins opt into service registration](0002-plugin-service-registration-opt-in.md)
- [ADR-0003: Keep agent-facing operations advisory and explicit](0003-advisory-explicit-agent-operations.md)
- [ADR-0004: Stage end-to-end tests around Aspire](0004-stage-end-to-end-tests-around-aspire.md)
- [ADR-0005: Disable Copilot agent operations by default](0005-copilot-agent-operations-disabled-by-default.md)
