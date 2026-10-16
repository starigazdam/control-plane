# Tasks

All tasks below are unstarted. This is a proposal-only plan; nothing here is
authorized for implementation, and every checkbox stays unchecked. The Fable
specification review is PENDING and has not been performed. Each unit starts
only after its own gate in group 1; the later live-host gates are not a
precondition for application code.

## 1. Approval gates

- [ ] 1.1 Obtain an independent high-capability specification review by Fable, the reviewer named by the maintainer, of this exact artifact revision before human design approval; record it as PENDING until performed, and treat any material spec change as invalidating the review (verify: the review is recorded as pending and no approval is claimed)
- [ ] 1.2 Obtain explicit human approval of these planning artifacts after the specification review and before any implementation; record the approval in the change review (verify: approval is recorded and this document is unchanged until then)
- [ ] 1.3 Obtain separate authorization for public application delivery (unit A) before starting group 2; unit A requires gates 1.1, 1.2, and 1.3 before any application code, while the unit-B authorization (1.4) and the later live-host authorization (1.5) do not block application code (verify: authorization is recorded)
- [ ] 1.4 Obtain separate authorization for the companion deployment configuration (unit B) before starting group 4 (verify: authorization is recorded)
- [ ] 1.5 Obtain separate authorization for live verification before starting group 5 (verify: authorization is recorded)

## 2. Public application delivery

- [ ] 2.1 Add `Dockerfile` as a multi-stage build that compiles the UI, publishes the API, serves both from one container, and runs as non-root; verify the image builds and the process reports a non-root runtime UID
- [ ] 2.2 Add `.dockerignore` excluding local `.env` variants, local databases, Git metadata, local `bin`/`obj`/`dist`/`node_modules`, and test evidence from the build context; verify the context excludes each category
- [ ] 2.3 Update `src/ControlPlane.Api/Program.cs` to serve the compiled UI through the default static-file pipeline, return HTTP 404 (not the UI document) for `/api/unknown` without adding error middleware, and avoid an HTTP redirect loop in container mode while preserving development behavior; verify endpoint tests cover root serving, the HTTP 404 unknown-route behavior, and absent redirect loop
- [ ] 2.4 Add `src/ControlPlane.Api/Controllers/VersionController.cs` exposing `GET /api/version` that returns only `{ "commitSha": <string> }`; verify a local run returns `unknown` and the body contains only that property
- [ ] 2.5 Enforce the `BUILD_SHA` build input (exactly 40 lowercase hexadecimal characters) and bake it into `Build__CommitSha` plus the OCI revision label; verify a valid value succeeds and missing, empty, or malformed values fail the build with no apparently identified image
- [ ] 2.6 Update `ui/src/App.tsx` so a production build defaults API requests to the same origin while preserving an explicit `VITE_API_BASE_URL` (including an empty relative value) and the existing development fallback; verify with UI tests for default, empty, and explicit overrides
- [ ] 2.7 Add a small accessible build badge that exposes the full SHA in accessible text or a title and degrades to `Build unknown` on HTTP/network failure or missing, null, wrong-type, or invalid data without blocking the overview or tabs; verify in `ui/src/App.test.tsx`
- [ ] 2.8 Add `compose.yaml` with one control-plane service, one named SQLite volume mounted at `/data`, `restart: unless-stopped`, a required `CONTROL_PLANE_IMAGE` reference with no implicit build, loopback-default `CONTROL_PLANE_BIND_ADDRESS`, and `CONTROL_PLANE_PORT` defaulting to 8080, plus the SQLite provider and database path and the disabled Copilot agent as explicit service settings; verify `docker compose config` resolves and a missing image reference is rejected
- [ ] 2.9 Add `deployment/env.example` as a generic interpolation example limited to image, bind, and port only; set the SQLite provider and database path and the disabled Copilot agent explicitly in `compose.yaml` instead; verify the example exposes only these three inputs and no host environment file
- [ ] 2.10 Add `docs/container-dev.md` documenting build, run, persistence, and the development-only unauthenticated boundary; verify every documented command runs as written
- [ ] 2.11 Verify named-volume ownership for the non-root runtime user by writing and updating the database in the mounted volume and confirming a non-root UID can do so (verify: runtime UID check plus a successful volume write, not just a `USER` line)
- [ ] 2.12 Verify the final image contains the compiled UI, the published application, and the required runtime libraries, and excludes secrets, local runtime databases and evidence, source dependency caches, build toolchains, and unnecessary development assets (verify: image content inspection that does not remove required build output or runtime dependencies)

## 3. Container and browser acceptance in CI

- [ ] 3.1 Add a focused container smoke script and a new `.github/workflows/container.yml` workflow that builds the image with a known commit SHA, starts it, and asserts the served version equals that exact build identity; verify the workflow passes
- [ ] 3.2 Add a container persistence check that seeds a fabricated project and a rejected unknown operation, records the correlation id, operation id, and project, restarts and recreates the container with the same volume, and verifies those exact values with a preserved count; verify no live cloud, broker, or agent call is made
- [ ] 3.3 Add one browser-test dev dependency and browser acceptance that verifies overview data, the build badge, Projects and Execution History, same-origin default requests, and the absence of application load failures against a real container; verify the acceptance passes
- [ ] 3.4 Add the container, persistence, and browser jobs to the new `.github/workflows/container.yml` only, leaving `.github/workflows/ci.yml` and its existing backend, UI, and Aspire gates unchanged, with explicit cleanup of test-owned resources kept separate from persistent-volume deletion and no broad prune; verify each job runs and only test-owned resources are removed
- [ ] 3.5 Verify CI fails the image build for a missing or malformed `BUILD_SHA` and produces no apparently identified image; verify the failure is reported as a build failure

## 4. Companion deployment configuration (separate repository; separate review)

These tasks belong to a separate deployment-configuration repository and its own
review and authorization. This repository does not implement them and must not
copy the other repository's script. This group only prepares and statically
reviews the setup script and service record with fabricated or static checks; it
must not start a service, and it does not claim verified live browser access.
Live startup and access belong to group 5.

- [ ] 4.1 Add the host setup script and service record in the separate deployment-configuration repository under its own review (verify: the reviewed script exists in that repository and is not present here)
- [ ] 4.2 Configure the operator-selected private-network interface, the reserved host port, and the explicit image selection in the script (verify: static review shows only the selected interface is configured and no all-interface default is used)
- [ ] 4.3 Statically check the setup script and service record for startup, restart survival, and direct-access configuration using fabricated placeholders, without starting any service (verify: static checks pass and no service is started)
- [ ] 4.4 Confirm this repository's artifacts contain no real host, address, credential, or topology details (verify: review of the published artifacts)

## 5. Separately approved live verification

- [ ] 5.1 With explicit live authorization, deploy the approved image through the companion configuration to the operator-selected interface (verify: the running container reports the expected build identity)
- [ ] 5.2 Verify direct browser access to the overview, build badge, Projects, and Execution History from the selected interface (verify: observed access with no fabricated live-readiness claim)
- [ ] 5.3 Verify data persistence across a real restart using fabricated records only (verify: the same records are present after restart and unexpected external actions are not triggered)

## 6. Spec sync and archive

- [ ] 6.1 After evidence-backed completion, sync the `container-delivery` delta to the main specs and verify `openspec validate --all --strict` passes

## Workflow follow-up

- Open the proposal pull request with `Refs #37` and leave #37 open.
- Archive this change only after the project's review requirements, green CI, and human approval are satisfied.
- Verify the archived result and that issue #37 is closed only by a separate, authorized action.
