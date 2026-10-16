# Proposal

## Why

Control Plane has no generic way to check configured HTTP endpoints. Operators need a named operation and a status signal that use the existing plugin model. A manual, read-only first slice limits risk and effort while establishing the capability contract.

## What Changes

- Add a `ControlPlane.HttpEndpoint` plugin with configured endpoint definitions, a status provider, and a named manual check operation.
- Limit the first slice to `GET` and `HEAD` requests. The caller selects a configured endpoint by ID; the caller cannot provide a URL, request body, or headers.
- Return bounded status and result summaries. Do not return response bodies or expose full configured URLs in ordinary output, status, or logs.
- Keep endpoint definitions in external runtime configuration. Do not commit live endpoint values or secrets.
- Defer scheduled checks and cron parsing. They need a separate design for schedule limits, cached state, restart behavior, and ownership. Keep them outside this change and preserve them as follow-up work for issue #6.
- Declare capabilities through existing `IControlPlanePlugin`, `IPluginRegistration`, `IStatusProvider`, and `IOperation` contracts. Do not add a plugin-specific agent tool. Agent projection remains subject to the approved design and implementation of #48.

## Capabilities

### New Capabilities

- `http-endpoint-checks`: Check configured HTTP endpoints manually through a named operation and expose their latest check status.

### Modified Capabilities

None. The existing plugin-registration and operation-execution requirements do not change.

## Impact

- New `src/ControlPlane.HttpEndpoint` project, host registration, configuration binding, and safe placeholder settings.
- New API operation and status-provider behavior through existing contracts. No new endpoint URL is accepted from an operation request.
- New runnable `.http` example and fabricated-fixture tests, including a full Aspire-backed observe-and-act test.
- No new database schema, UI framework, scheduler, cron parser, external credentials, or agent-specific tool.
- This proposal narrows issue #6's current scope: scheduled checks and the POST example are not part of this first slice. Treat that narrowing as proposed, not approved, until the design receives independent review and human approval.
- This change does not add authentication, authorization, enforced confirmation, or redacted immutable audit. The existing workbench does not provide those controls. Do not claim that operation metadata enforces them. Keep calls on the existing workbench path and coordinate any new security behavior with the approved #51 design; do not invent a plugin-specific security path.
