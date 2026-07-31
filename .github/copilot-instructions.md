# Copilot Instructions

## Delivery rule
Every vertical or feature slice must be covered by an end-to-end test before it is treated as complete.

## Test strategy
1. Prefer .NET Aspire orchestration for end-to-end coverage whenever feasible.
2. If Aspire cannot be used for a dependency yet, keep the substitute path temporary and document the blocker.
3. Validate the full observe + act loop, not just one side of the workflow.

## Implementation standard
1. Build production-intent slices, not MVP-only shortcuts.
2. Keep operations explicit and opinionated.
3. Add runnable `.http` examples for plugin endpoints, with secrets loaded from `.env`.
