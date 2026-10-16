# Spec Delta

## Purpose

Define how a standing development instance of Control Plane is delivered as a
single-origin, non-root container image with persistent SQLite data, a
build-identity endpoint, and bounded acceptance coverage, while a separate
reviewed configuration handles host deployment.

## ADDED Requirements

### Requirement: Serve the compiled UI and JSON API from one origin

A single application container SHALL serve the compiled React UI and the JSON API
from the same origin. The containerized process SHALL run as a non-root user.
Static assets SHALL be served through the standard ASP.NET Core static-file and
default-document pipeline. A request for an unmapped API path SHALL return HTTP
404 and SHALL NOT return the UI document. Mapped JSON API responses SHALL remain
distinct, and no catch-all fallback or error middleware SHALL be added.

#### Scenario: Compiled UI is served at the origin root

- **WHEN** a browser requests the origin root from the container
- **THEN** the container returns the compiled UI document and its static assets

#### Scenario: Unknown API path returns HTTP 404 and not the UI document

- **WHEN** a client requests an API path that is not mapped
- **THEN** the container returns HTTP 404 and does not return the UI document

#### Scenario: Container process is non-root

- **WHEN** the container is running
- **THEN** the application process reports a non-root runtime user identity

### Requirement: Default production API calls to the same origin

A production UI build SHALL default its API requests to the same origin that
served the UI. The build SHALL honor an explicit `VITE_API_BASE_URL` override,
including an empty relative value, and SHALL preserve the existing development
fallback when no override is set. A production build MUST NOT bake in a loopback
API origin by default.

#### Scenario: No override in a production build

- **WHEN** a production UI is built without a `VITE_API_BASE_URL` override
- **THEN** its API requests target the same origin that served the UI

#### Scenario: Empty relative override

- **WHEN** a production UI is built with an empty relative `VITE_API_BASE_URL` value
- **THEN** its API requests remain relative to the serving origin

#### Scenario: Explicit override is honored

- **WHEN** a production UI is built with an explicit `VITE_API_BASE_URL` value
- **THEN** its API requests target that value

### Requirement: Expose image build identity through a version endpoint

`GET /api/version` SHALL return a JSON object whose only property is a string
`commitSha`. An image build SHALL report the full commit SHA baked at build
time. A normal local non-image development process without build metadata SHALL
report the literal `unknown`. The endpoint MUST NOT disclose filesystem paths,
hostnames, environment variables, or runtime inventory.

#### Scenario: Image reports its full build SHA

- **WHEN** the version endpoint is requested from a built image
- **THEN** the response is a JSON object whose `commitSha` is the full baked commit SHA

#### Scenario: Local development reports unknown

- **WHEN** the version endpoint is requested from a local non-image process without build metadata
- **THEN** the response is a JSON object whose `commitSha` is the literal `unknown`

#### Scenario: Response is limited to the commit field

- **WHEN** any caller requests the version endpoint
- **THEN** the JSON body contains only the `commitSha` property and no host, path, or environment details

### Requirement: Reject image builds without a valid commit identity

An image build SHALL require the `BUILD_SHA` build input to be exactly 40
lowercase hexadecimal characters. The build SHALL fail when the input is
missing, empty, or malformed and SHALL NOT produce an apparently identified
image. The validated SHA SHALL be recorded in application configuration and as
an OCI revision label. This metadata records provenance and is not a signature
or a cryptographic deployment attestation.

#### Scenario: Missing or malformed build identity fails the build

- **WHEN** an image build runs without `BUILD_SHA`, or with an empty or malformed value
- **THEN** the build fails and no apparently identified image is produced

#### Scenario: Valid build identity is recorded

- **WHEN** an image build runs with a `BUILD_SHA` of exactly 40 lowercase hexadecimal characters
- **THEN** the build succeeds and the SHA is recorded in the application configuration and the OCI revision label

### Requirement: Show an accessible build badge without blocking the UI

The UI SHALL display a build badge showing the build commit. A shortened display
MUST expose the full SHA in accessible text or a title. When the version request
fails at the HTTP or network level, or returns missing, null, wrong-type, or
invalid data, the UI SHALL show `Build unknown` and SHALL NOT block the overview
or the tabs.

#### Scenario: Valid version data renders the badge

- **WHEN** the version endpoint returns a valid commit SHA
- **THEN** the badge shows the commit and the full SHA is available in accessible text or a title

#### Scenario: Unavailable or malformed version data degrades gracefully

- **WHEN** the version request fails or returns missing, null, wrong-type, or invalid data
- **THEN** the UI shows `Build unknown` and the overview and tabs remain usable

### Requirement: Persist application data in a named SQLite volume

The Compose definition SHALL run exactly one control-plane service with a named
SQLite volume mounted at `/data`. An empty volume SHALL initialize the database
on first start. Existing project and execution-history records SHALL survive
service restart and container recreation with the same volume. The service SHALL
prepare writable ownership for the non-root runtime user.

#### Scenario: Empty volume initializes the database

- **WHEN** the service starts against an empty named volume
- **THEN** the database is created under `/data` and the service serves data

#### Scenario: Records survive restart

- **WHEN** records are written and the service is restarted with the same volume
- **THEN** the same records are present with the same project identity, correlation id, operation id, and isolated count

#### Scenario: Records survive container recreation

- **WHEN** the container is recreated with the same named volume
- **THEN** the previously written records remain present with the same project identity, correlation id, and operation id

#### Scenario: Non-root process writes to the volume

- **WHEN** the service runs as its non-root runtime user
- **THEN** it can create and update the database files in the mounted volume

### Requirement: Require explicit image and bind configuration

The Compose definition SHALL require an explicit `CONTROL_PLANE_IMAGE` reference
and SHALL NOT build an image implicitly. `CONTROL_PLANE_BIND_ADDRESS` SHALL
default to loopback and `CONTROL_PLANE_PORT` SHALL default to 8080. The
application SHALL listen on container HTTP port 8080 without a redirect loop. An
exposed host port MUST bind only the explicitly selected interface and MUST NOT
bind all interfaces by default.

#### Scenario: Missing image reference is rejected

- **WHEN** the Compose definition is started without an explicit `CONTROL_PLANE_IMAGE` value
- **THEN** startup fails rather than building or pulling an implicit image

#### Scenario: Default bind stays on loopback

- **WHEN** no bind address is provided
- **THEN** the service binds the loopback address and does not expose all interfaces

#### Scenario: Selected private interface is bound

- **WHEN** an operator supplies a private-network interface as `CONTROL_PLANE_BIND_ADDRESS`
- **THEN** the exposed port is bound to that interface only

#### Scenario: Container HTTP works without a redirect loop

- **WHEN** a client calls the container over HTTP
- **THEN** the request is served without an HTTP-to-HTTPS redirect loop

### Requirement: Scope image build context and final image contents

The image build context SHALL exclude local `.env` variants, local databases,
Git metadata, local build-cache directories (`bin`, `obj`, `dist`,
`node_modules`), and test evidence. The final image SHALL contain the compiled
UI, the published application, and the required runtime libraries, and SHALL
exclude secrets, local runtime data, source dependency caches, and build
toolchains.

#### Scenario: Build context excludes sensitive and local files

- **WHEN** the image is built
- **THEN** local `.env` variants, local databases, Git metadata, local `bin`, `obj`, `dist`, and `node_modules`, and test evidence are excluded from the build context

#### Scenario: Final image keeps required output and drops what it must not ship

- **WHEN** the final image is inspected
- **THEN** it contains the compiled UI, the published application, and the required runtime libraries, and it excludes secrets, local runtime databases and evidence, source dependency caches, build toolchains, and unnecessary development assets

### Requirement: Keep delivered configuration inert

The delivered development configuration SHALL keep the Copilot agent disabled,
SHALL NOT include live credentials, and SHALL NOT activate real external
operations or automated calls to the execute endpoint.

#### Scenario: Delivered configuration stays inert

- **WHEN** the delivered development service starts
- **THEN** the Copilot agent is disabled, no live credentials are present, and no real external operation is activated

### Requirement: Verify the container data path with fabricated records

Container acceptance SHALL run against a real built container and SHALL use only
fabricated records and a rejected unknown operation, with no live cloud, broker,
or agent call. The check SHALL record the seeded row's correlation id, operation
id, and project before restart and SHALL verify the same records after restart.
Success MUST NOT be inferred from a 200 status or a nonempty result alone. Count
preservation is scoped to the isolated fixture with no concurrent writer.

#### Scenario: Seeded records are verified after restart

- **WHEN** a fabricated record and a rejected unknown operation are written, the service is restarted, and history is read
- **THEN** the same correlation id, operation id, and project are present with a preserved count

#### Scenario: No live external action is taken

- **WHEN** container acceptance runs
- **THEN** it makes no live cloud, broker, or agent call

### Requirement: Verify browser acceptance against the exact tested build

Browser acceptance SHALL exercise a real container and SHALL verify the overview
data, the build badge, the Projects and Execution History views, same-origin
default requests, and the absence of application load failures. The container
smoke check SHALL assert the exact build identity that was built. This coverage
is new and is not existing evidence.

#### Scenario: Browser walkthrough succeeds against the tested image

- **WHEN** browser acceptance runs against the built image
- **THEN** overview data, the build badge, Projects, and Execution History render from same-origin requests with no application load failure

#### Scenario: Smoke check asserts the tested build identity

- **WHEN** the container smoke check runs
- **THEN** it confirms the served version matches the exact build identity that was built

### Requirement: Define generic outcomes for a separate host deployment

A standing deployment SHALL be delivered as a separately reviewed configuration
in a separate deployment-configuration repository. Public artifacts SHALL state
only generic outcomes, SHALL NOT disclose the real environment, and SHALL NOT
claim that this repository implements that configuration or that live readiness
was verified.

#### Scenario: Generic deployment outcomes are stated

- **WHEN** the deployment configuration is described in public artifacts
- **THEN** it names interface selection, port reservation, service registration, image selection, startup, restart survival, and direct browser access as outcomes to be provided by the separately reviewed companion configuration
- **AND** it states that the companion configuration is prepared and statically checked separately and that live startup, direct browser access, and restart verification run only under separate live authorization

#### Scenario: No real environment or live claim is disclosed

- **WHEN** the deployment configuration is described in public artifacts
- **THEN** no real host, address, credential, or topology is disclosed and no live readiness is claimed
