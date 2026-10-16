# Proposal

## Why

Control Plane has no generic way to check configured HTTP endpoints. Operators need a named operation and a status signal that use the existing plugin model. A manual, read-only first slice limits risk and effort while establishing the capability contract.

## What Changes

- Add a `ControlPlane.HttpEndpoint` plugin with configured endpoint definitions, a status provider, and a named manual check operation.
- Limit this slice to `GET` and `HEAD`. The caller selects a configured endpoint by ID and cannot provide a URL, request body, or headers.
- Return bounded summaries. Do not expose configured URLs, response headers, bodies, or raw exceptions in operation output, status, logs, or traces.
- Read endpoint definitions from `HttpEndpoint__ConfigPath`. An empty value disables the plugin. Commit only an empty placeholder.
- Defer scheduled checks and POST to separate follow-up issues. Keep issue #6 open until its full scope is complete or the maintainer changes its acceptance criteria.
- Declare capabilities through existing plugin contracts. Do not add a plugin-specific agent tool. Agent projection remains subject to the approved design and implementation of #48.

## Capabilities

### New Capabilities

- `http-endpoint-checks`: Check configured HTTP endpoints manually through a named operation and expose their status.

### Modified Capabilities

None. The existing plugin-registration and operation-execution requirements do not change.

## Impact

- Add `src/ControlPlane.HttpEndpoint`, host registration, configuration binding, and an empty `.env` placeholder for `HttpEndpoint__ConfigPath`.
- Add the operation ID `http-endpoint-check` and a status provider through existing contracts.
- Add a runnable `.http` example and an Aspire-backed observe-and-act test with a local fabricated HTTP endpoint.
- Add no database schema, scheduler, cron parser, external credentials, or agent-specific tool.
- This proposal defers issue #6's scheduled checks and POST example to follow-up issues. This slice does not satisfy all of issue #6's current acceptance criteria. Keep #6 open.
- This change does not add authentication, authorization, enforced confirmation, or immutable audit. The existing workbench does not provide those controls. Keep calls on the existing workbench path and coordinate shared security work with #51; do not invent a plugin-specific security path.
