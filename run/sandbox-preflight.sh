#!/usr/bin/env bash
# Validate the native-Docker host required by Aspire DCP.
set -euo pipefail

fail() {
  printf 'sandbox preflight: ERROR: %s\n' "$1" >&2
  exit 1
}

if [[ -n "${DOCKER_HOST:-}" ]]; then
  case "$DOCKER_HOST" in
    tcp://*|http://*|https://*)
      fail "DOCKER_HOST=$DOCKER_HOST uses a remote TCP endpoint; run Aspire on lxc-sandbox where Docker is native, or unset DOCKER_HOST"
      ;;
    *)
      fail "DOCKER_HOST is set; unset it so Aspire DCP uses the native Docker Unix socket"
      ;;
  esac
fi

command -v docker >/dev/null 2>&1 || fail "docker CLI is not installed"
command -v dotnet >/dev/null 2>&1 || fail "dotnet SDK is not installed"
command -v node >/dev/null 2>&1 || fail "node is not installed (required by the Vite resource)"
command -v npm >/dev/null 2>&1 || fail "npm is not installed (required by the Vite resource)"
[[ -d "${PWD}/ui/node_modules" ]] || fail "ui/node_modules is missing; run npm ci --prefix ui once on the sandbox"

docker info >/dev/null 2>&1 || fail "Docker daemon is unavailable through the native socket"

dotnet_version="$(dotnet --version)"
docker_version="$(docker version --format '{{.Server.Version}}')"
node_version="$(node --version)"
npm_version="$(npm --version)"

printf 'sandbox preflight: OK\n'
printf '  dotnet SDK: %s\n' "$dotnet_version"
printf '  Docker Engine: %s\n' "$docker_version"
printf '  Node.js: %s\n' "$node_version"
printf '  npm: %s\n' "$npm_version"
printf '  Docker endpoint: native Unix socket (DOCKER_HOST unset)\n'
