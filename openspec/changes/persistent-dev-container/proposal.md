# Proposal

## Why

Issue #37 needs a standing, persistent development instance of Control Plane
that an operator can reach directly on an explicitly selected private-network
address. Today the repository has no container delivery path: no image, no
Compose definition, no build identity, and no way to run the compiled UI and the
API as one deployable unit. The only supported entry point is the Aspire
development orchestration, so there is nothing a separate deployment-
configuration repository could run as a standing instance. A tunnel could be
used for off-network diagnostics, but routine access must stay direct rather
than depending on a tunnel as the only path.

## What Changes

- Add one non-root container image that serves the compiled React UI and the
  JSON API from a single origin using the standard ASP.NET Core static-file and
  default-document pipeline. An unmapped API path returns HTTP 404 and never the
  UI document; mapped JSON API responses are unchanged, and no catch-all fallback
  or error middleware is added.
- Make production UI builds default to same-origin API requests while preserving
  an explicit `VITE_API_BASE_URL` override (including an empty relative value)
  and the existing development fallback.
- Add a read-only version endpoint that returns only a JSON object with a
  `commitSha` string, and record the build commit in the image so an image
  reports its full build SHA while local non-image development reports the
  literal `unknown`.
- Require a valid build identity for image builds: a build input `BUILD_SHA`
  that is exactly 40 lowercase hexadecimal characters. Missing, empty, or
  malformed metadata must fail the build rather than produce an apparently
  identified image. The SHA is baked into configuration and an OCI revision
  label; it records provenance, not a signature or cryptographic attestation.
- Add a small accessible UI build badge sourced from the version endpoint. Any
  HTTP or network failure, or missing, null, wrong-type, or invalid data,
  resolves to the literal `Build unknown` (distinct from the version endpoint's
  `commitSha` literal `unknown`) and must not block the overview or tabs.
- Add a Compose definition with exactly one control-plane service and one named
  SQLite volume mounted at `/data`. The service requires an explicit
  `CONTROL_PLANE_IMAGE` reference, defaults its bind address to loopback, and
  listens on container HTTP port 8080. The service sets an explicit production
  environment (`ASPNETCORE_ENVIRONMENT=Production`) and an explicit container
  hosting switch (`ContainerHosting__Enabled=true`, default false when absent)
  so container HTTP is served without HTTPS redirection while other deployments
  and Aspire development keep their existing behavior. Data must survive restart
  and container recreation with the same volume.
- Add a generic deployment interpolation example limited to image, bind address,
  and port, plus generic container documentation. Keep the SQLite provider and
  database path, the existing `CopilotAgent__Enabled=false` setting, the
  explicit production environment, and the explicit container hosting switch in
  `compose.yaml`, not in the three-key example. The image build context excludes
  both the committed `.env` and local dotenv variants, local databases, Git
  metadata, local `bin`/`obj`/`dist`/`node_modules`, and test evidence; no dotenv
  file is copied into the final image. The final image contains the compiled UI,
  the published application, and required runtime libraries, and excludes
  secrets, local runtime databases and evidence, source dependency caches, build
  toolchains, and unnecessary development assets.
- Add bounded container and browser acceptance coverage in one new focused
  workflow (`.github/workflows/container.yml`) that uses only fabricated records
  and a rejected unknown operation, plus a container smoke path. Browser
  acceptance verifies the overview data, the build badge, Projects and Execution
  History, same-origin default requests, and the absence of application load
  failures. The existing `.github/workflows/ci.yml` backend, UI, and Aspire gates
  stay unchanged.
- Keep the existing Aspire orchestration, the plugin contracts, and the
  operation execution behavior unchanged. Production container serving is
  additive and does not replace the development/test path.

Two implementation units are separately gated after approval: public
application delivery (image, Compose, build identity, generic docs, and
container/browser acceptance in the new workflow), and a separate deployment
configuration in another repository (host setup, service record, reserved
port/interface, and image selection). This proposal only requests approval for
the planning artifacts.

Before any human design approval, this corrected revision requires an
independent high-capability specification review by Opus, the reviewer the
maintainer named to replace Fable for all remaining reviews of this unit. The
earlier Fable review of revision c23d3e5 returned REQUEST_CHANGES; that is a
historical result, not an Opus review, and not approval. The Opus review of this
corrected revision is PENDING: it has not been invoked, performed, or approved.
The review is bound to this artifact revision, so a material spec change
requires a new Opus review before implementation. Specification review is not
final implementation review, CI success, merge approval, deployment permission,
or archive authority. Each unit waits only for its own gate; the later live-host
authorization is not a precondition for application code. Gate records live in
the issue or pull request discussion and are bound to the artifact revision they
approve.

## Capabilities

### New Capabilities

- `container-delivery`: a single-origin, non-root application image that serves
  the compiled UI and JSON API, exposes a build-identity version endpoint
  consumed by an accessible UI badge, persists application data in a named
  SQLite volume across restart and recreation, is configured through an
  explicit image reference and an operator-selected bind address, and is
  covered by bounded container and browser acceptance built on fabricated data.

### Modified Capabilities

None. The baseline `plugin-registration` and `operation-execution` specs
describe existing behavior that this change does not alter.

## Impact

- New artifacts (proposed, not implemented here): `Dockerfile`, `.dockerignore`,
  `compose.yaml`, `deployment/env.example`, `docs/container-dev.md`, a new
  version controller under `src/ControlPlane.Api/Controllers/`, a focused
  container smoke script, and `.github/workflows/container.yml`.
- Proposed modifications: `src/ControlPlane.Api/Program.cs` (static-file
  serving, the `ContainerHosting` switch and production middleware set, and
  version endpoint registration), `ui/src/App.tsx` and its component tests
  (build badge and same-origin API default), and `ui/package.json` (one
  browser-test dev dependency if required at implementation time).
- Preserved unchanged: `.github/workflows/ci.yml` and its existing backend, UI,
  and Aspire gates, and the `src/ControlPlane.ServiceDefaults/Extensions.cs`
  development behavior. No unrelated CI refactor.
- APIs: a new read-only `GET /api/version` returning `{ "commitSha": <string> }`.
  No existing endpoint changes.
- Dependencies: no pinning of browser or base-image versions is decided here;
  future implementation may use official Node 22 and .NET 10 base images and one
  browser-test dev dependency with versions verified then.
- Out of scope: WorkItem schema and migrations, backup automation, a production
  database, authentication, authorization, confirmation, redaction, immutable
  audit, high availability, registry publication, auto-promotion, TLS, and real
  external operations. The existing operation-safety gaps remain tracked by #14
  and #51, and agent parity remains a draft under #48.
