# Remote Docker development on `lxc-sandbox`

## Supported topology

Aspire and its Distributed Application Control Plane (DCP) run **on the Docker
host**, using the sandbox's native Docker Unix socket. Hermes and `lxc-aibox`
are remote developer clients that synchronize the checkout and invoke the
AppHost over SSH. Neither client needs Docker, and neither should point Aspire
at a forwarded `DOCKER_HOST=tcp://...` endpoint.

This is intentional: Docker API reachability through a TCP bridge does not
prove that Aspire DCP accepts the endpoint as a healthy runtime. Running DCP
beside the native socket avoids that failure mode and keeps all containers,
volumes, networks, and build caches on the disposable sandbox.

The sandbox is development-only. Do not place production data, credentials,
private registry logins, or persistent production services there.

## One-time sandbox preparation

After the reviewed change is merged, and with explicit approval to modify the
sandbox, install/verify the pinned project prerequisites on `lxc-sandbox`:

```bash
# Run on the sandbox, from a checkout of this repository.
dotnet --version       # must satisfy global.json (10.0.x)
docker version
docker compose version
node --version         # Node.js 22 is used by CI
npm --version
npm ci --prefix ui
sha256sum ui/package-lock.json > ui/node_modules/.sandbox-package-lock.sha256
touch .sandbox-checkout
```

`SANDBOX_REPO_DIR` must already exist and contain `.sandbox-checkout`; this
marker is a deliberate guard against a path typo overwriting another directory.
The runner maintains that directory as a synchronized, non-git checkout.
Run the one-time preparation commands inside the directory that will be used
for `SANDBOX_REPO_DIR`. Do not set `DOCKER_HOST` on the sandbox. If it is present in the environment,
remove it from the shell/session used to start Aspire. Verify the daemon with:

```bash
./run/sandbox-preflight.sh
```

The preflight is deliberately read-only. It checks Docker, .NET, Node, npm, UI
dependencies, and rejects TCP/HTTP Docker endpoints before DCP starts.

## Remote run from Hermes or aibox

Configure an SSH alias in the developer's `~/.ssh/config` (the alias and path
are intentionally not committed):

```text
Host control-plane-sandbox
    HostName <sandbox-management-address>
    User <development-user>
```

Then run from any checkout on Hermes or aibox:

```bash
SANDBOX_SSH_TARGET=control-plane-sandbox \
SANDBOX_REPO_DIR=/home/<development-user>/src/control-plane \
./run/sandbox-run.sh
```

The runner:

1. Requires the configured remote directory to contain `.sandbox-checkout`.
2. Archives **tracked files only** over SSH, so `.env.local`, certificates, and
   other untracked local material never leave the client. The tracked `.env`
   placeholder is transferred; never put a real value in that tracked file.
3. Extracts into a staging directory, preserves only `.aspire` state and
   `ui/node_modules`, then replaces the checkout. Deleted or renamed tracked
   files therefore cannot remain stale on the sandbox.
4. Runs the preflight remotely against the native Docker socket.
5. Starts `dotnet run --project src/ControlPlane.AppHost/ControlPlane.AppHost.csproj`
   remotely, so DCP and Docker are colocated.

The runner holds `${SANDBOX_REPO_DIR}.lock` for the complete sync and AppHost
session. A second Hermes/aibox client is refused until the first session exits;
if a client is known to be gone, inspect the sandbox before removing a stale
lock directory.

The sandbox's `.aspire` state, Docker named volumes, pulled images, and Docker
build cache are not transferred or deleted. Source-only iterations therefore
reuse dependency and container state. The sync does not run Docker prune,
volume removal, or broad filesystem cleanup.

Only one AppHost session should run against the shared sandbox at a time. Check
`docker ps` before starting a second client. If an SSH connection drops, reconnect
or inspect the AppHost containers and stop only the orphaned development process
after confirming its resource names; do not use broad Docker cleanup.

The AppHost's dashboard and web endpoint are printed by the remote process.
Use an SSH local-port forward for temporary access; do not expose the sandbox
service publicly. Because Aspire allocates development ports dynamically, a
follow-up LAN deployment command should use an explicitly chosen, reviewed
port mapping rather than assuming a dashboard port.

## Integration test execution

Distributed AppHost tests must also run on a machine with native Docker access.
Run them on the sandbox (or in CI's native Docker runner), not from Hermes/aibox
through a TCP Docker bridge:

```bash
ssh control-plane-sandbox \
  'cd /home/<development-user>/src/control-plane && ./run/sandbox-preflight.sh && dotnet test tests/ControlPlane.AppHost.Tests/ControlPlane.AppHost.Tests.csproj --configuration Release'
```

CI remains the merge gate. The remote sandbox is an inner-loop development and
reproduction environment, not a replacement for CI.

Changing `ui/package-lock.json` requires repeating `npm ci --prefix ui` and the
`sha256sum` recording command on the sandbox before the next run; preflight
fails closed until the dependency installation matches the lockfile.

## State, reset, rollback, and cleanup

- PostgreSQL uses the Aspire data volume and is persistent across normal AppHost
  restarts. Treat its contents as disposable development state.
- The Service Bus Emulator is recreated/reused by Aspire as dictated by its
  resource lifetime; no production messages belong there.
- Normal source synchronization and AppHost shutdown do not delete volumes,
  images, networks, or build cache.
- Resetting the database, removing volumes/networks, or broad Docker cleanup
  requires explicit confirmation and an inventory first:

  ```bash
  docker ps -a
  docker volume ls
  docker network ls
  docker images
  docker system df
  ```

- Rollback is client-side source rollback: select the prior reviewed commit on
  Hermes/aibox and rerun `sandbox-run.sh`. The controlled replacement removes
  tracked files introduced by the newer source version; it does not delete
  persistent Docker state.
- A later LAN deployment workflow must add explicit health checks, port
  ownership, a versioned image/tag, and a reversible previous-version path.
  It is intentionally not hidden inside `sandbox-run.sh`.

## Timing measurements

Measure source synchronization separately from AppHost startup. The sync-only
mode still performs the safety checks and controlled replacement but does not
leave an interactive server running:

```bash
/usr/bin/time -f 'sync_elapsed=%E' \
  SANDBOX_SYNC_ONLY=1 ./run/sandbox-run.sh
# After a source-only change, repeat the same command and compare the timings.
```

A full AppHost start can then be measured separately with a bounded operator
observation or a future health-gated runner mode. Record timings in the
issue/PR as environment metadata. Do not claim expected latency until the
commands have been run on the current sandbox state.
