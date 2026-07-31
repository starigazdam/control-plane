# Control Plane Engineering Rules

## Vertical delivery standard
1. Every vertical or feature slice must include end-to-end test coverage before it is considered complete.
2. A vertical is complete only when observe + act behavior is verified through realistic integration flow (status + operation execution path).

## End-to-end testing approach
1. Prefer .NET Aspire orchestration for end-to-end test execution whenever feasible.
2. If Aspire cannot be used for a specific dependency in the first pass, document the blocker and keep the substitute test path temporary.
3. Aspire-based coverage is the target steady-state for all verticals.

## Scope policy
1. Do not gate delivery behind an MVP-only subset.
2. Build verticals to production-intent quality, including observability, operation safety checks, and testability.
