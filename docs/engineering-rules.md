# Control Plane Engineering Rules

- Every vertical or feature slice must have an end-to-end test.
- Prefer .NET Aspire for e2e coverage; temporary workarounds must be documented.
- Verify the full observe + act loop.
- Build production-intent slices, not MVP shortcuts.
- For GitHub issues, use a separate feature branch + worktree and require human PR approval before merge.
