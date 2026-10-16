# Container development image

This document describes how to build and run the single-origin development
container for Control Plane. The image serves the compiled React UI and the JSON
API from one ASP.NET Core process and one origin, runs as a non-root user, and
persists SQLite data in a named volume.

This is a **development-only** deliverable. It is unauthenticated: anyone who
can reach the bound interface can read data and call the execute endpoint. Do
not put sensitive data, live credentials, or public port forwarding in front of
it. A private-network address is not an authentication boundary.

## Prerequisites

- Docker with the Compose plugin.
- An explicit commit identity for the build (`BUILD_SHA`), exactly 40 lowercase
  hexadecimal characters. The build fails without a valid value so an
  unidentified image can never be mistaken for a verified artifact.

## Build

```bash
docker build \
  --build-arg BUILD_SHA=<40-lowercase-hex-commit> \
  --tag control-plane:<short-or-full-commit> .
```

The validated SHA is recorded in the application as `Build__CommitSha` and in
the image's OCI `revision` label. This is provenance metadata, not a signature or
a deployment attestation. The tag is mutable; it is not an immutable digest.

The multi-stage build compiles the UI with Node 22, publishes the API with the
.NET SDK, and copies only the published application, the compiled UI, and the
required runtime libraries into the final runtime image.

## Run with Compose

`compose.yaml` runs exactly one `control-plane` service with one named SQLite
volume mounted at `/data`. It never builds or pulls an image implicitly: an
explicit `CONTROL_PLANE_IMAGE` reference is required.

```bash
export CONTROL_PLANE_IMAGE=control-plane:<short-or-full-commit>
export CONTROL_PLANE_BIND_ADDRESS=127.0.0.1   # loopback default
export CONTROL_PLANE_PORT=8080               # default
docker compose -p control-plane-dev up -d
```

`deployment/env.example` is a generic interpolation example covering only these
three inputs (image, bind address, port). Application settings are not injected
from a host environment file; `compose.yaml` sets them explicitly:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ContainerHosting__Enabled=true`
- `Database__Provider=Sqlite`
- `ConnectionStrings__ControlPlane=Data Source=/data/control-plane.db`
- `CopilotAgent__Enabled=false`

The published host port binds only the selected interface and defaults to
loopback, so the service is not exposed on all interfaces by default. The
container listens on HTTP port 8080.

Verify the served build identity:

```bash
curl -fsS "http://127.0.0.1:${CONTROL_PLANE_PORT:-8080}/api/version"
# {"commitSha":"<the BUILD_SHA you built with>"}
```

Open `http://127.0.0.1:${CONTROL_PLANE_PORT:-8080}/` for the UI. A production
container omits Swagger/OpenAPI, the developer exception page, and the
`/health` and `/alive` endpoints. An unmapped API path such as `/api/unknown`
returns HTTP 404 and not the UI document.

## Persistence

The named volume holds the SQLite database at `/data/control-plane.db`. The
volume directory is owned by the non-root runtime user, so an empty volume
initializes the database on first start. The existing placeholder project is
auto-created on the first overview or project read; there is no project-creation
API.

Records survive an ordinary restart and container recreation with the same
volume:

```bash
docker compose -p control-plane-dev restart
docker compose -p control-plane-dev up -d --force-recreate
```

`docker compose down -v` deletes the named volume and all data. It is **not** an
upgrade step and must not be used in routine procedures. To stop the service
without deleting data, use `docker compose -p control-plane-dev down`.

## Development-only limits

- Unauthenticated and single-operator: no authentication, authorization,
  confirmation, redaction, or immutable audit is enforced.
- The Copilot agent stays disabled through the existing
  `CopilotAgent__Enabled=false` setting; that setting is not authentication.
- No live credentials are included and the delivered configuration does not
  activate real external operations.
- The image is not a production database, TLS endpoint, reverse proxy, or
  high-availability deployment. Registry publication and auto-promotion are out
  of scope.
