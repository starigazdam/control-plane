# Copilot Instructions

- Every vertical or feature slice needs an end-to-end test.
- Prefer .NET Aspire for e2e coverage; keep any workaround temporary and documented.
- Validate the full observe + act loop.
- Build production-intent slices, keep operations explicit, and include runnable `.http` examples with secrets from `.env`.
- For GitHub issues, use a separate feature branch + worktree and require human PR approval before merge.
