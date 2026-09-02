#!/usr/bin/env bash
# Validate the native-Docker host required by Aspire DCP.
set -euo pipefail

fail() {
  printf 'sandbox preflight: ERROR: %s\n' "$1" >&2
  exit 1
}

if [[ -n "${DOCKER_HOST:-}" ]]; then
  fail "DOCKER_HOST is set; unset it so Aspire DCP uses the native Docker Unix socket"
fi

command -v docker >/dev/null 2>&1 || fail "docker CLI is not installed"
command -v dotnet >/dev/null 2>&1 || fail "dotnet SDK is not installed"
command -v node >/dev/null 2>&1 || fail "node is not installed (required by the Vite resource)"
command -v npm >/dev/null 2>&1 || fail "npm is not installed (required by the Vite resource)"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
[[ -d "$repo_root/ui/node_modules" ]] || fail "ui/node_modules is missing; run npm ci --prefix ui once on the sandbox"
lock_hash="$(sha256sum "$repo_root/ui/package-lock.json" | cut -d' ' -f1)"
recorded_hash="$(cut -d' ' -f1 "$repo_root/ui/node_modules/.sandbox-package-lock.sha256" 2>/dev/null || true)"
[[ "$recorded_hash" == "$lock_hash" ]] || fail "UI lockfile changed or has no recorded install; run npm ci --prefix ui && sha256sum ui/package-lock.json > ui/node_modules/.sandbox-package-lock.sha256"

context="$(docker context show 2>/dev/null)" || fail "Docker context cannot be determined"
endpoint="$(docker context inspect "$context" --format '{{.Endpoints.docker.Host}}' 2>/dev/null)" || fail "Docker context endpoint cannot be inspected"
case "$endpoint" in
  unix://*) ;;
  *) fail "Docker context '$context' uses '$endpoint', not a native Unix socket" ;;
esac

docker info >/dev/null 2>&1 || fail "Docker daemon is unavailable through the native socket"

dotnet_version="$(dotnet --version)"
docker_version="$(docker version --format '{{.Server.Version}}')"
node_version="$(node --version)"
npm_version="$(npm --version)"
case "$dotnet_version" in 10.*) ;; *) fail "dotnet SDK '$dotnet_version' is outside the project's .NET 10 requirement" ;; esac
case "$node_version" in v22.*) ;; *) fail "Node.js '$node_version' is outside the project's Node.js 22 requirement" ;; esac

printf 'sandbox preflight: OK\n'
printf '  dotnet SDK: %s\n' "$dotnet_version"
printf '  Docker Engine: %s\n' "$docker_version"
printf '  Docker context: %s\n' "$context"
printf '  Docker endpoint: %s\n' "$endpoint"
printf '  Node.js: %s\n' "$node_version"
printf '  npm: %s\n' "$npm_version"
