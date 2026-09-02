# Plugin `.http` Examples

Every Control Plane plugin that exposes operations or status providers must
ship a runnable `.http` example. This is a standard for **all** plugins, not
a one-off for a single implementation, and is enforced in CI by
[`scripts/check_plugin_http_examples.py`](../scripts/check_plugin_http_examples.py)
(part of the `ADR and Hermes consistency` workflow).

## Requirements

1. **One `.http` file per plugin project**, named after the plugin (for
   example `src/ControlPlane.Azure/azure.http`). Keep it next to the
   plugin's code so contributors find it while reading the plugin.
2. **Runnable outside Control Plane** — plain REST Client / `.http` syntax
   (VS Code REST Client, JetBrains HTTP Client, etc.), no dependency on this
   repo's tooling beyond the running API.
3. **Secrets loaded from `.env`** — the file must document which `.env`
   keys the plugin needs and instruct contributors to put real values in
   `.env.local` (gitignored). Never put a real secret, hostname, tenant ID,
   or connection string in the committed `.http` file — only placeholders
   containing `example`.
4. **Reference a `{{HOST}}` variable** pointing at the local API
   (`http://localhost:5149` by default) so contributors can override it per
   environment without editing every request.
5. Cover the plugin's operations with at least one request per operation,
   plus a request to check `/api/operations/history` so the result is
   observable.

## Template

Use [`ControlPlane.CopilotAgent/copilot-agent.http`](../src/ControlPlane.CopilotAgent/copilot-agent.http)
as the reference example — it documents prerequisites, the `.env.local`
settings it needs, and one runnable request per operation.

## CI enforcement

`scripts/check_plugin_http_examples.py` discovers every project under
`src/ControlPlane.*` that defines an `Operations/` or `StatusProviders/`
directory (i.e. an actual plugin, not `ControlPlane.Core` or
`ControlPlane.ServiceDefaults`) and fails the build if:

- the plugin has no `.http` file,
- the plugin has more than one `.http` file (keep one canonical example),
- the `.http` file doesn't reference `{{HOST}}` or `.env`, or
- the `.http` file contains a token-like string that isn't an
  `example`-prefixed placeholder (a lightweight secret-scanning heuristic).
