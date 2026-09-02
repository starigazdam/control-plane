# Control Plane Engineering Rules

- Every vertical or feature slice must have an end-to-end test.
- Prefer .NET Aspire for e2e coverage; temporary workarounds must be documented.
- Verify the full observe + act loop.
- Build production-intent slices, not MVP shortcuts.
- For GitHub issues, use a separate feature branch + worktree and require human PR approval before merge.
- CI must be green before merge: .NET restore/build (tests run once test projects exist) and UI install/lint/build.
- Keep every public artifact generic: no credentials, environment topology, or internal references — see `SECURITY.md`.
- Every plugin with operations or status providers ships a runnable `.http` example loading secrets from `.env` — see `docs/plugin-http-examples.md`; enforced by `scripts/check_plugin_http_examples.py`.
