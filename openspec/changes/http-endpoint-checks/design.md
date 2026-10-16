# Design

## Context

See `proposal.md` for motivation and `specs/http-endpoint-checks/spec.md` for the behavior contract. At `develop` SHA `81f327a5927dc2959a5e6b9ecc07cf11b0c35117`, the API discovers plugins from supplied assemblies and binds settings in `ServiceCollectionExtensions`. `IStatusProvider` runs during overview reads. `IOperation` calls pass through `ControlPlaneWorkbenchService`, which persists result output. The current path does not enforce authentication, authorization, confirmation, or immutable redacted audit. Do not treat operation metadata as enforcement.

No HTTP endpoint plugin, scheduler, or cron dependency exists in this revision. `ServiceDefaults` configures shared `HttpClient` resilience and telemetry. `ControlPlane.Api.E2E` uses `WebApplicationFactory`; `ControlPlane.AppHost.Tests` uses Aspire and currently covers service health and project configuration. The engineering rules require an end-to-end observe-and-act test for each feature slice.

## Goals / Non-Goals

**Goals:**

- Add a configuration-backed, manual endpoint-check capability through existing plugin contracts.
- Share request and result handling between the manual operation and overview status provider.
- Keep all outbound requests bounded and all returned or persisted summaries free of endpoint URLs and response payloads.
- Keep the capability eligible for a future shared agent projection without a plugin-specific agent tool.

**Non-Goals:**

- Scheduled checks, cron parsing, background work, or persisted last-check state.
- POST, request bodies, custom headers, response-body assertions, or arbitrary URLs in operation input.
- New identity, authorization, confirmation, audit, or redaction infrastructure. Coordinate security design with #51 and use only approved shared controls when available.
- Agent-specific tool implementation. The shared projection remains governed by #48.
- Live external endpoint calls in automated tests.

## Decisions

### Use configured endpoints and ID-only operation input

Read endpoint definitions from the configured external JSON file. Bind each operation call to one configured endpoint ID. Reject invalid URLs and unsupported methods during configuration validation. Require absolute HTTP or HTTPS URLs and reject URL user information. Do not copy endpoint values into committed examples. Do not accept a URL, body, or headers from a request.

The configuration is the trusted destination list. Do not add a private-network blocklist because deployments can need to monitor private service endpoints. Disable automatic redirects so a configured destination cannot redirect the checker to a second destination.

### Restrict the first slice to GET and HEAD

Use only GET and HEAD. This keeps the first slice read-oriented and excludes the POST webhook example in issue #6. Defer POST and any other method until a separate design defines its side-effect and confirmation boundary. This is a proposed scope change, not an approved change to the issue.

### Reuse one checker and the existing plugin contracts

Create one injectable checker for endpoint lookup, HTTP requests, timeout handling, and safe result mapping. The operation and status provider call that checker. Register both through `IControlPlanePlugin` and `IPluginRegistration`; bind settings through the API host's existing configuration setup. Use `IHttpClientFactory` and the existing shared HTTP-client defaults. Do not add a cron library or another package for this slice.

### Keep outcomes safe before persistence

Return only endpoint ID, configured display name, observed status code when available, elapsed duration, observation time, and a stable error code. Do not return URL, response headers, response body, or raw exception text. The workbench persists operation output, so the operation must create a safe summary before returning it. Do not claim this provides comprehensive log, telemetry, or history redaction.

### Keep overview checks live and bounded

The status provider checks configured endpoints when the overview requests status. It uses the same timeout and safe result mapping as the manual operation. A failed or invalid endpoint produces a Warning signal. Missing or invalid configuration remains visible as a Warning instead of taking down the host or silently hiding the plugin.

A finite per-request timeout bounds each check. The design does not add a result cache or new persistence. Many configured endpoints can still slow an overview read; keep the first configuration small and record this as a limitation.

### Keep agent parity on the shared path

Register the operation and provider once through the existing plugin contracts. Do not add a handwritten agent tool. When #48 defines and implements the shared capability projection, this plugin must use that path and the same governed workbench service. This proposal does not claim that projection exists today.

## Risks / Trade-offs

- **Configured destinations can still reach private services:** this is required for endpoint monitoring. Mitigate by accepting destinations only from trusted runtime configuration, rejecting caller-supplied URLs, and disabling redirects.
- **Endpoint URLs may contain sensitive data:** reject URL user information and never include the URL in results, status descriptions, or logs. Avoid response bodies and headers.
- **Overview latency can grow with endpoint count:** apply a finite timeout to each request, avoid retries for the manual check, and keep the first configuration small. Add concurrency limits or cached status only if measured need justifies them.
- **Existing operation history is not fully redacted:** return only safe fields from this operation, but do not claim that other persisted fields or telemetry are protected. The broader issue remains with #51.
- **Scope differs from issue #6:** scheduled checks and POST are deferred. Keep #6 open until the maintainer accepts the scope change or assigns those behaviors to follow-up work.
- **Agent projection is not yet approved:** no plugin-specific workaround is allowed. Track integration against #48 and do not claim agent parity before its shared contract is approved and implemented.

## Migration Plan

No database migration is required. Add only placeholder configuration examples with the feature disabled or empty by default. Existing operations and plugins remain unchanged. Automated tests use a local fabricated HTTP handler or test server and make no live external calls. Deployment and live endpoint activation are outside this proposal.
