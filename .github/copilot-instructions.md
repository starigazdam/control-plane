# Copilot Instructions

- Every vertical or feature slice needs an end-to-end test.
- Prefer .NET Aspire for e2e coverage; keep any workaround temporary and documented.
- Validate the full observe + act loop.
- Build production-intent slices, keep operations explicit, and include runnable `.http` examples with configuration from `.env` (placeholders only; real values live in `.env.local`).
- For GitHub issues, use a separate feature branch + worktree and require human PR approval before merge.
- Keep CI green before merge: .NET restore/build (tests run once test projects exist) and UI install/lint/build; never claim a check or test that does not exist.
- This repository is public: keep credentials, environment topology, and internal references out of code, tests, docs, issues, PRs, and screenshots — see `SECURITY.md`.
