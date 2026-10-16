# Design

## Context

See `proposal.md` and `specs/http-endpoint-checks/spec.md` for the proposed behavior. At `develop` SHA `81f327a5927dc2959a5e6b9ecc07cf11b0c35117`, the API discovers plugins from supplied assemblies. `IStatusProvider` runs during overview reads. `IOperation` calls pass through `ControlPlaneWorkbenchService`, which persists operation results. The inspected path does not enforce authentication, authorization, confirmation, or immutable redacted audit.

`StatusLevel` supports `Unknown`, `Healthy`, `Warning`, and `Critical`. `ControlPlane.ServiceDefaults` applies `AddStandardResilienceHandler`, service discovery, HTTP client logs, and HTTP client OpenTelemetry instrumentation to clients by default. The checker must override those defaults where they conflict with its contract. Aspire E2E tests use a local distributed application and can configure the API resource environment.

## Goals / Non-Goals

**Goals:**

- Add configured manual checks through existing plugin contracts.
- Keep requests, overview work, and endpoint count within explicit bounds.
- Build safe results before the workbench persists them.
- Prevent configured URLs, headers, bodies, and raw exceptions from reaching output, logs, or traces.
- Keep the capability eligible for a future shared agent projection.

**Non-Goals:**

- Scheduled checks, cron parsing, POST, request bodies, custom headers, or response-body assertions.
- New identity, authorization, confirmation, audit, or general-purpose redaction infrastructure.
- Live external endpoint calls in automated tests.

## Decisions

### Use configured endpoints and ID-only input

Read definitions from `HttpEndpoint__ConfigPath`. Resolve relative paths against the API content root, which is the repository root in repository-hosted development. An unset or empty path disables the plugin and returns an `Unknown` snapshot with no signals. If a non-empty path names a missing, unreadable, or malformed file, fail startup with a clear error that names the setting and path, not any endpoint URL.

Allow at most 20 endpoint definitions. Require IDs of 1-64 ASCII letters, digits, dots, underscores, or hyphens; IDs must be unique without regard to case. Require a non-empty display name and an absolute HTTP or HTTPS URL without user information. Require expected status codes from 100 through 599. Invalid definitions are excluded and produce a `Warning` signal identified by array index. Exclude every definition in a duplicate-ID group.

Accept only `GET` and `HEAD`. A `POST` or other method makes that definition invalid and causes no request. Ignore `cron` fields and never start scheduled work. Report one safe `Warning` signal when scheduling fields are present; keep otherwise-valid definitions available for manual checks. Commit an empty `.env` value for `HttpEndpoint__ConfigPath`; do not commit endpoint URLs.

The configuration is the trusted destination list. Do not accept caller-supplied URLs or add a private-network blocklist, because deployments can need private service checks. Disable redirects so the configured destination cannot redirect the checker elsewhere.

### Restrict this slice to manual GET and HEAD checks

Use only `GET` and `HEAD`. This defers the POST webhook example from issue #6 until a separate design addresses its side-effect and confirmation boundary. Defer cron until a separate design defines schedule limits, result ownership, restart behavior, and resource limits. Keep issue #6 open. Create separate follow-up issues for these deferred behaviors after the reviewed spec is accepted.

### Use a dedicated HTTP client with one-request semantics

Create one named `IHttpClientFactory` client for endpoint checks. Override the shared resilience defaults for this client: do not retry, do not use a circuit breaker, and do not add a second timeout layer. Set `AllowAutoRedirect` to false on its primary handler. Apply a per-request cancellation timeout from endpoint settings. Use `ResponseHeadersRead` and dispose each response without reading its content. The same checker serves manual operations and status checks, so both paths send at most one request per endpoint check.

Use a default timeout of 5 seconds. Accept configured timeouts from 1 through 10 seconds. Run at most 20 endpoint checks concurrently. Share one in-flight overview scan per API process among concurrent overview callers and cache its results for 15 seconds. Enforce a 12-second overall scan budget; cancel unfinished requests at that limit and map them to `endpoint_timeout`. A caller's cancellation stops its wait but does not cancel a scan that other callers share. Manual operations bypass the overview cache and perform one fresh check.

### Suppress URL-bearing logs and traces

Do not emit endpoint URLs, headers, bodies, or raw exceptions from plugin code. Suppress all named-client framework request logs, including exception messages, and redact or suppress its `url.full` trace attribute. Keep unrelated HTTP client logging and telemetry unchanged. Add tests that capture the named-client logs and activities and verify they contain no configured URL or query value.

### Return safe outcomes and map status levels

Use the stable operation ID `http-endpoint-check`. The operation accepts one configured endpoint ID and uses the existing validation path. An unknown ID is rejected before network access; the workbench records its existing `operation_validation_failed` result and history entry.

A successful check returns the endpoint ID, observed status, observation time, and elapsed duration. A failed check returns a stable code: `endpoint_unexpected_status`, `endpoint_timeout`, or `endpoint_request_failed`. Do not return endpoint URLs, response headers, response bodies, or exception text. Never read response content.

Map a matching status to `Healthy`, an unexpected HTTP status to `Warning`, and timeout or transport failure to `Critical`. Map invalid configuration entries and unsupported schedule fields to `Warning`. Map a disabled plugin to `Unknown` with no signals. These are the available `StatusLevel` values; they represent issue #6's Healthy, Degraded, and Unhealthy intent as Healthy, Warning, and Critical.

### Verify the complete observe-and-act loop with Aspire

Add an Aspire-backed E2E test that starts a local fabricated HTTP server, passes its temporary URL through runtime configuration, reads endpoint status, invokes `http-endpoint-check`, and verifies the persisted safe history result. The test must make no live external requests. Use a real loopback server for redirect behavior; a mocked `DelegatingHandler` does not test the primary handler's redirect policy.

## Risks / Trade-offs

- **Configured destinations can reach private services:** this is required for endpoint monitoring. Mitigate with trusted runtime configuration, ID-only input, one request per check, and disabled redirects.
- **Overview reads cause outbound requests:** concurrent callers share one per-process scan, results cache for 15 seconds, the scan has a 12-second budget, and the list is capped at 20 endpoints. Manual operations can still cause repeated requests. Existing API authentication gaps remain; this is not a substitute for #51.
- **GET is not guaranteed to be side-effect free:** operators must configure only endpoints intended for monitoring. POST remains deferred.
- **URLs can contain sensitive values:** suppress URL-bearing logs and trace attributes; never return URL, headers, body, or raw exceptions.
- **Existing operation history is not generally redacted:** this operation returns only its safe summary. Do not claim that other stored fields or telemetry are protected.
- **Issue #6 remains partly open:** scheduled checks and POST move to follow-up issues. This slice alone does not satisfy all current acceptance criteria.
- **Agent projection is not yet approved:** use no plugin-specific workaround. Track future integration against #48.

## Migration Plan

No database migration is required. Add an empty `HttpEndpoint__ConfigPath` placeholder to `.env`. Empty configuration disables the plugin. Tests create temporary configuration and a local HTTP server. Deployment and activation against live endpoints are outside this proposal.
