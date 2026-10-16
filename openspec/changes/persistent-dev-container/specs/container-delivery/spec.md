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

### Requirement: Run one control-plane service with a named SQLite volume

The Compose definition SHALL run exactly one control-plane service with a named
SQLite volume mounted at `/data`. An empty volume SHALL initialize the database
on first start, using the existing auto-created placeholder project. The service
SHALL prepare writable ownership for the non-root runtime user.

#### Scenario: Empty volume initializes the database

- **WHEN** the service starts against an empty named volume
- **THEN** the database is created under `/data` and the service serves data

#### Scenario: Placeholder project is auto-created on read

- **WHEN** the overview or project endpoint is read against an empty volume
- **THEN** the existing placeholder project is created and returned, and no separate project-creation API is used

#### Scenario: Non-root process writes to the volume

- **WHEN** the service runs as its non-root runtime user
- **THEN** it can create and update the database files in the mounted volume

### Requirement: Preserve records without adding a persistence surface

Existing project and execution-history records SHALL survive service restart and
container recreation with the same volume. This change SHALL NOT add a
create-project endpoint, direct database seeding, new schema or CRUD, or a new
operation.

#### Scenario: Records survive restart

- **WHEN** records are written and the service is restarted with the same volume
- **THEN** the same records are present with the same project identity, correlation id, operation id, and isolated count

#### Scenario: Records survive container recreation

- **WHEN** the container is recreated with the same named volume
- **THEN** the previously written records remain present with the same project identity, correlation id, and operation id

### Requirement: Require explicit image and bind configuration

The Compose definition SHALL require an explicit `CONTROL_PLANE_IMAGE` reference
and SHALL NOT build an image implicitly. `CONTROL_PLANE_BIND_ADDRESS` SHALL
default to loopback and `CONTROL_PLANE_PORT` SHALL default to 8080. The
application SHALL listen on container HTTP port 8080. An exposed host port MUST
bind only the explicitly selected interface and MUST NOT bind all interfaces by
default.

#### Scenario: Missing image reference is rejected

- **WHEN** the Compose definition is started without an explicit `CONTROL_PLANE_IMAGE` value
- **THEN** startup fails rather than building or pulling an implicit image

#### Scenario: Default bind stays on loopback

- **WHEN** no bind address is provided
- **THEN** the service binds the loopback address and does not expose all interfaces

#### Scenario: Selected private interface is bound

- **WHEN** an operator supplies a private-network interface as `CONTROL_PLANE_BIND_ADDRESS`
- **THEN** the exposed port is bound to that interface only

### Requirement: Gate HTTPS redirection on an explicit container hosting switch

The container SHALL start with exactly one explicit hosting switch set to enabled
(`ContainerHosting__Enabled=true`, read from the `ContainerHosting` configuration
section). When the switch is absent the process SHALL default it to false and
SHALL preserve existing behavior, including Aspire development. When the switch
is enabled the process SHALL NOT perform unconditional HTTPS redirection. The
switch SHALL NOT add a framework or readiness endpoint.

#### Scenario: Enabled hosting switch serves container HTTP without HTTPS redirection

- **WHEN** the container starts with `ContainerHosting__Enabled=true` and a client calls the container over HTTP
- **THEN** the request is served without an unconditional HTTPS redirect

#### Scenario: Missing switch preserves existing behavior

- **WHEN** `ContainerHosting__Enabled` is absent
- **THEN** the process treats it as false and existing deployments, including Aspire development, keep their current behavior

### Requirement: Omit Development-only middleware and endpoints in the production container

In the production container (`ASPNETCORE_ENVIRONMENT=Production`), Swagger UI and
the OpenAPI middleware, the developer exception page, and the `/health` and
`/alive` mappings SHALL be absent.

#### Scenario: Production container omits Development-only middleware and endpoints

- **WHEN** the production container runs
- **THEN** Swagger UI and the OpenAPI middleware, the developer exception page, and the `/health` and `/alive` mappings are not served

### Requirement: Scope image build context and final image contents

The image build context SHALL exclude both the committed `.env` and local dotenv
variants, local databases, Git metadata, local build-cache directories (`bin`,
`obj`, `dist`, `node_modules`), and test evidence; no dotenv file SHALL be copied
into the final image. The final image SHALL contain the compiled UI, the
published application, and the required runtime libraries, and SHALL exclude
secrets, local runtime data, source dependency caches, and build toolchains.

#### Scenario: Build context excludes sensitive and local files

- **WHEN** the image is built
- **THEN** both the committed `.env` and local dotenv variants, local databases, Git metadata, local `bin`, `obj`, `dist`, and `node_modules`, and test evidence are excluded from the build context
- **AND** no dotenv file is copied into the final image

#### Scenario: Final image keeps required output and drops what it must not ship

- **WHEN** the final image is inspected
- **THEN** it contains the compiled UI, the published application, and the required runtime libraries, and it excludes secrets, local runtime databases and evidence, source dependency caches, build toolchains, and unnecessary development assets

### Requirement: Keep delivered configuration inert

The delivered development configuration SHALL keep the Copilot agent disabled
through the existing `CopilotAgent__Enabled=false` setting, SHALL NOT include
live credentials, and SHALL NOT activate real external operations or automated
calls to the execute endpoint. The setting is not authentication.

#### Scenario: Delivered configuration stays inert

- **WHEN** the delivered development service starts
- **THEN** the Copilot agent is disabled through the existing `CopilotAgent__Enabled=false` setting, no live credentials are present, and no real external operation is activated

### Requirement: Verify the container data path with fabricated records only

Container acceptance SHALL run against a real built container and SHALL use only
fabricated records and a rejected unknown operation, with no live cloud, broker,
or agent call. It SHALL NOT create a project, seed the database directly, add
schema or CRUD, or add a new operation. Success MUST NOT be inferred from a 200
status or a nonempty result alone.

#### Scenario: No live external action is taken

- **WHEN** container acceptance runs
- **THEN** it makes no live cloud, broker, or agent call

### Requirement: Use the placeholder project identity for the persistence check

The check SHALL read the existing auto-created placeholder project through the
current overview or project endpoint and SHALL use that identity as the request's
project id.

#### Scenario: Placeholder project identity is used as the request project id

- **WHEN** the check reads the overview or project endpoint
- **THEN** it obtains the existing placeholder project identity and uses it as the project id for the rejected unknown operation

### Requirement: Verify the persisted history tuple across restart and recreation

The check SHALL post one rejected unknown operation with a fabricated correlation
id and SHALL record the exact history tuple (project, correlation id, operation
id) and count before restart. It SHALL then verify the same tuple and count after
both a restart and a container recreation with the same named volume. Count
preservation is scoped to the isolated fixture with no concurrent writer.

#### Scenario: Seeded records are verified after restart and recreation

- **WHEN** a rejected unknown operation is written with a fabricated correlation id, the service is restarted, and then the container is recreated with the same volume
- **THEN** the same project, correlation id, operation id, and count are present

### Requirement: Verify browser acceptance against the exact tested build

Browser acceptance SHALL exercise a real container and SHALL verify the overview
data, the build badge, the Projects and Execution History views, same-origin
default requests, and the absence of application load failures. The container
smoke check SHALL assert the exact build identity that was built.

#### Scenario: Browser walkthrough succeeds against the tested image

- **WHEN** browser acceptance runs against the built image
- **THEN** overview data, the build badge, Projects, and Execution History render from same-origin requests with no application load failure

#### Scenario: Smoke check asserts the tested build identity

- **WHEN** the container smoke check runs
- **THEN** it confirms the served version matches the exact build identity that was built

### Requirement: Keep public artifacts free of private deployment details

Public artifacts SHALL state only generic outcomes, SHALL NOT disclose a real
host, address, credential, environment topology, or runtime data, and SHALL NOT
claim that this repository implements the companion host deployment or that live
readiness was verified.

#### Scenario: No real environment or live claim is disclosed

- **WHEN** the development deployment or its companion configuration is described in public artifacts
- **THEN** no real host, address, credential, topology, or runtime data is disclosed and no live readiness is claimed
