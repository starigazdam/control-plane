# Design

## Context

See `proposal.md` for motivation. This design records the current baseline that
the change builds on and the technical choices for delivering issue #37.

The inspected baseline is source revision
`574e484dcd36115597b9a82dc76a00da966863dc`. Observed behavior:

- `src/ControlPlane.Api/Program.cs` selects SQLite by default and Postgres by
  configuration, creates the database with `EnsureCreated`, registers plugins,
  and maps controllers. It does not serve a compiled UI and exposes no version
  endpoint. It calls `UseHttpsRedirection()` unconditionally.
- `src/ControlPlane.AppHost/AppHost.cs` already orchestrates the API, the Vite
  UI, Postgres, the Service Bus emulator, and YARP. Aspire is present and is the
  canonical development path; this change does not replace it.
- `ui/src/App.tsx` uses one `API_BASE_URL` constant for every call. It honors
  `VITE_API_BASE_URL` and otherwise uses `http://localhost:5000`. The UI uses
  tabs, not URL routes, and has no build badge.
- `src/ControlPlane.Api/Services/ControlPlaneWorkbenchService.cs` persists
  projects and operation execution history. The recorded actor is client
  supplied and no authentication, authorization, or confirmation is enforced.
- `src/ControlPlane.Api/Controllers/OperationsController.cs` exposes `execute`
  and `history`. An unknown operation is rejected and still recorded, which
  gives an external-action-free persistence probe.
- `src/ControlPlane.ServiceDefaults/Extensions.cs` maps `/health` and `/alive`
  only when `IsDevelopment()`. Those paths must not be promised in a production
  container unless a later implementation deliberately adds a chosen readiness
  path.
- The tree has no `Dockerfile`, `.dockerignore`, `compose.yaml`,
  `deployment/env.example`, or container-version feature.

## Goals / Non-Goals

**Goals:**

- One non-root container image serving the compiled UI and the JSON API from a
  single origin, preserving the existing Aspire development path.
- A build identity source and a read-only `GET /api/version` that a small,
  accessible UI badge consumes without blocking the rest of the UI.
- A named SQLite volume that preserves project and execution-history records
  across restart and container recreation.
- Explicit image, bind, and port configuration with no default public
  exposure.
- Bounded, fabricated-data container and browser acceptance in CI plus a
  documented handoff to a separately reviewed host deployment.

**Non-Goals:**

- Actual deployment to any host. This change is planning only.
- Authentication, authorization, confirmation, redaction, or immutable audit.
  Those remain tracked by #14 and #51.
- A WorkItem schema, migrations, backup automation, or a production database.
- Registry publication, auto-promotion, high availability, TLS, or reverse
  proxy configuration.
- Agent parity (#48) and any agent tool, MCP server, or agent-parity
  implementation.

## Baseline vs Proposed Behavior

| Area | Baseline (existing) | Proposed (this change) |
| --- | --- | --- |
| UI/API origin | Vite dev server to API on `localhost:5000`; Aspire YARP in dev | One container serves compiled UI and JSON API same-origin |
| Unknown API path | Not applicable (no SPA hosting) | HTTP 404, never the UI document |
| Version endpoint | None | `GET /api/version` returning `{ "commitSha": string }` |
| UI build badge | None | Accessible badge; degrades to `Build unknown` |
| Persistence | SQLite file next to the API, or Aspire Postgres | Named SQLite volume at `/data`, survives restart/recreate |
| Image/bind config | Aspire-managed | `CONTROL_PLANE_IMAGE`, loopback-default bind, port 8080 |
| Aspire path | Canonical dev entry point | Unchanged; still canonical for development and tests |

The baseline specs `plugin-registration` and `operation-execution` are
unchanged. All new behavior belongs to the `container-delivery` delta.

## Decisions

1. **Single-origin, non-root image.** Serve the published API and the compiled
   React UI from one ASP.NET Core container. No proxy process, extra Node
   runtime, orchestrator, or plugin framework is added. Rationale: the UI has no
   router and the existing Aspire YARP path already serves one origin in
   development, so reusing that pattern is sensible. This is design rationale,
   not production acceptance evidence: the current Aspire tests do not
   establish that the proposed production UI works. Alternative rejected: a
   separate reverse proxy or Node sidecar, which adds a process and
   configuration surface without new behavior.

2. **Standard static-file pipeline, no SPA catch-all.** Reuse ASP.NET Core
   default documents and static files. Because the tab UI needs no URL routes,
   do not add a SPA fallback or error middleware; an unmapped API path must
   return HTTP 404 and not the UI document. Rationale: a catch-all fallback
   would silently mask unknown API paths. The current host has no blanket
   JSON-404 handler, so no JSON error body is required for an unmapped route;
   mapped JSON API responses stay distinct from the unknown-route behavior.

3. **Same-origin production API default.** `ui/src/App.tsx` production default
   becomes same-origin while keeping an explicit `VITE_API_BASE_URL` override
   (including an empty relative value) and the existing development fallback.
   Rationale: baking `http://localhost:5000` into an image would break a
   standing deployment. Alternative rejected: a runtime URL injected into the
   bundle, which is heavier than the build-time default.

4. **SQLite in a named volume.** Use SQLite at `/data/control-plane.db` inside a
   named volume and prepare writable ownership for the non-root runtime user.
   Preserve project and execution-history records across restart and recreation
   with the same volume. This adds no WorkItem schema, migrations, backup
   automation, or production database. Rationale: SQLite is the inspected
   default and is sufficient for a development instance.

5. **Strict build identity.** Require `BUILD_SHA` to be exactly 40 lowercase
   hexadecimal characters for image builds; missing, empty, or malformed input
   fails the build rather than producing an apparently identified image. Bake
   the SHA into `Build__CommitSha` and the OCI `revision` label. This is
   provenance, not a signature or cryptographic deployment attestation.
   Alternative rejected: defaulting a missing SHA to `unknown` at build time,
   which would let an unidentified image look deliberate.

6. **Version endpoint shape.** `GET /api/version` returns only an object with a
   string `commitSha`. An image returns its full baked SHA; normal local
   non-image development without metadata returns the literal `unknown`. The
   endpoint discloses no paths, hostnames, environment variables, or runtime
   inventory.

7. **Accessible, nonblocking badge.** Show a small build badge; any shortened
   display exposes the full SHA in accessible text or a title. HTTP or network
   failure, or missing, null, wrong-type, or invalid data, resolves to
   `Build unknown` and does not block the overview or tabs. The endpoint is
   shared application metadata served by the API, not a UI-only plugin
   capability, and no new agent surface is added.

8. **Compose shape.** One control-plane service, one named SQLite volume,
   `restart: unless-stopped`. Require an explicit `CONTROL_PLANE_IMAGE` tag or
   digest; the documented local example uses a commit-SHA tag, which stays
   mutable and is not a digest or a signature. Compose does not build
   implicitly. Registry publication is out of scope: the rollout builds and tags
   a local image or selects an already available approved image. Avoid
   `container_name` so test projects remain isolated.

9. **Bind and port.** `CONTROL_PLANE_BIND_ADDRESS` defaults to loopback;
   `CONTROL_PLANE_PORT` defaults to 8080. The application listens on container
   HTTP port 8080 and container HTTP must work without a redirect loop.
   Production container mode must not call unconditional
   `UseHttpsRedirection()`; existing development behavior is preserved outside
   that mode. No default all-interface bind.

10. **Generic interpolation example.** `deployment/env.example` is a generic
    Compose interpolation example limited to three interpolation keys: image,
    bind address, and port. Do not copy or inject a whole host environment file.
    Application settings do not belong in that example: the SQLite provider and
    database path, and the disabled Copilot agent, are set explicitly in
    `compose.yaml`. Never enable real external operations or copy live
    credentials for this development deployment.

11. **Build-context and image hygiene.** The build context excludes local
    `.env` variants, local databases, Git metadata, local
    `bin`/`obj`/`dist`/`node_modules`, and test evidence. The final image contains
    the compiled UI, the published application, and required runtime libraries,
    and excludes secrets, local runtime databases and evidence, source
    dependency caches, build toolchains, and unnecessary development assets.
    Named-volume preservation must survive ordinary restart and recreation;
    `down -v` is not documented as an upgrade step.

12. **Scope boundary.** This is development-only and unauthenticated. It is not
    acceptable for sensitive data, live action credentials, public port
    forwarding, or automated calls to the execute endpoint. LAN or
    private-network access does not provide authentication.

13. **Version choices deferred to implementation.** Do not hard-pin a browser
    dependency version or a base-image digest in a requirements document. Name
    the required behavior. Future implementation may use official Node 22 and
    .NET 10 images and one browser-test dev dependency, with compatible versions
    verified then. The runtime image stays free of extra browser and curl
    packages.

## Future Implementation Loci

Exact paths in the current tree. "New" means the file does not exist yet.

- Existing to modify: `src/ControlPlane.Api/Program.cs` (static-file serving,
  production HTTP behavior, version endpoint registration),
  `ui/src/App.tsx` (same-origin default and build badge),
  `ui/src/App.test.tsx` (badge and fallback coverage), and `ui/package.json` only
  if one browser-test dev dependency is needed.
- New: `Dockerfile`, `.dockerignore`, `compose.yaml`, `deployment/env.example`,
  `docs/container-dev.md`, `src/ControlPlane.Api/Controllers/VersionController.cs`,
  a focused container smoke script, and `.github/workflows/container.yml`.
- Preserved unchanged: `.github/workflows/ci.yml` with its existing backend, UI,
  and Aspire gates. No unrelated CI refactor.
- Preserve unchanged: `src/ControlPlane.AppHost/AppHost.cs` and
  `src/ControlPlane.ServiceDefaults/Extensions.cs` development behavior. A
  production readiness path is not promised; `/api/version` proves process
  reachability and `/api/overview` probes the data path, but neither alone
  proves full health.
- Acceptance may reuse `tests/ControlPlane.Api.Tests` and
  `tests/ControlPlane.Api.E2E` (`WebApplicationFactory`) for endpoint-level
  coverage and `tests/ControlPlane.AppHost.Tests` for the Aspire path.

## Known Security Limitations

- The development deployment is unauthenticated and single-operator. Anyone who
  can reach the selected interface can read data and call the execute endpoint.
- Operation authorization, confirmation, redaction, and immutable audit remain
  absent and are tracked by #14 and #51. `RequiresConfirmation` metadata on an
  operation definition proves none of these.
- The build SHA is provenance metadata, not a signature or an attestation that
  a running container matches an approved artifact.
- Private-network reachability is not an authentication boundary.

## Acceptance Evidence

Each criterion maps to setup, action, and observable result, and is marked as
automated CI or separately authorized live only.

| Criterion | Setup -> Action -> Observable | Kind |
| --- | --- | --- |
| Non-root single-origin serving | Build image -> request `/` and `/api/unknown` as the runtime user -> UI document at `/` and HTTP 404 (not the UI document) at `/api/unknown`, with a non-root UID | Automated |
| Build identity | Build with valid and invalid `BUILD_SHA` -> build twice -> valid build succeeds with recorded SHA; invalid build fails | Automated |
| Version endpoint | Run image and local build -> `GET /api/version` -> `{ "commitSha": <sha> }` in the image and `unknown` locally | Automated |
| UI badge | Serve valid, then unavailable, version data -> load UI -> badge shows commit with accessible full SHA, or `Build unknown` without blocking tabs | Automated |
| Volume persistence | Start with an empty named volume -> write fabricated records and a rejected unknown operation, restart, recreate -> same correlation id, operation id, project, and count | Automated |
| Bind safety | Start without a bind address -> inspect listener -> loopback only, no all-interface bind | Automated |
| No secrets or live actions | Inspect build context and delivered image -> context omits local env/db/vcs/build-cache/test evidence; image has compiled UI, published app, and runtime libs, with no secrets, local runtime databases/evidence, source caches, or build toolchains | Automated |
| Browser walkthrough | Run browser acceptance against the tested image -> overview, badge, Projects, Execution History, same-origin requests, no load failures | Automated |
| Smoke build identity | Run smoke check -> served version equals the exact built identity | Automated |
| Host deployment | Separately reviewed configuration in another repository -> selected private interface, reserved port, service record, image selection, startup/restart/direct-access verification | Separately authorized live only |

Acceptance uses only fabricated records. It never makes a live cloud, broker, or
agent call. The persistence check records the seeded row's correlation id,
operation id, and project before restart and verifies those exact values after,
rather than trusting a 200 status or a nonempty result. Count preservation is
scoped to the isolated fixture with no concurrent writer. Expected CI cleanup
of test-owned resources is separate from persistent-volume deletion; there is no
broad prune and no unapproved live cleanup.

## Risks / Trade-offs

- [An image without a valid build identity could be mistaken for a verified
  artifact] -> Fail the build on missing or malformed `BUILD_SHA` and label the
  metadata as provenance only.
- [Unconditional HTTPS redirection breaks container HTTP] -> Serve HTTP in the
  container mode without a redirect loop and keep development behavior
  unchanged.
- [A SPA catch-all masks unknown API paths] -> Use the standard static-file
  pipeline without a catch-all and assert HTTP 404, not the UI document, for
  unknown API paths.
- [A mutable image tag is mistaken for an immutable digest] -> State that a
  commit-SHA tag is mutable and not a digest or signature, and document a digest
  as an option, not a claim.
- [Volume loss through `down -v`] -> Document `down -v` as not an upgrade step
  and keep volume deletion out of routine procedures.
- [CI browser dependencies drift] -> Verify compatible versions at
  implementation time and avoid a requirements-level pin.
- [The development instance looks like a production environment] -> Document
  the unauthenticated, development-only boundary and keep live operations and
  credentials out.

## Migration / Rollout / Rollback

- Migration: none for this proposal; no data is changed. A later image restart
  must preserve the same external named volume.
- Rollout (future, separately authorized): build and tag a local image (or
  select an already available approved image) under a commit-SHA tag, then have
  the separate deployment configuration select the image and interface. Registry
  publication is out of scope; a commit-SHA tag is mutable and is not a digest or
  a signature. This repository does not perform the host deployment.
- Rollback (future, separately authorized): select a previous tested image
  without deleting the volume. Cross-version rollback safety is not claimed for
  unknown future schema changes; an incompatible database change requires a
  separately approved recovery plan.

## Approval Gates

1. Independent high-capability specification review by Fable, the reviewer named
   by the maintainer, of this exact artifact revision. This review is PENDING and
   has not been invoked or approved. A material spec change requires a new Fable
   review before implementation.
2. Human approval of these planning artifacts after the specification review.
3. Separate authorization before applying the change, split into public
   application delivery (unit A) and the companion deployment configuration
   (unit B). Each unit waits only for its own authorization; the later live-host
   gates are not a precondition for application code.
4. Separate live authorization before group 5 live startup, direct browser
   access, and restart verification. Group 4 only prepares and statically
   reviews the setup script and service record and never starts a service.
5. Normal review, green CI, and human approval before merge. A proposal pull
   request uses `Refs #37` and leaves #37 open.
6. Spec sync and archive only after evidence-backed completion.

Specification review is not final implementation review, CI success, merge
approval, deployment permission, or archive authority.

## Open Questions

None that change the specs, approach, or task breakdown. Base-image and
browser-dependency versions are deferrable to implementation and are named as
behavior, not pinned here.
