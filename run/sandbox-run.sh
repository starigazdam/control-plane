#!/usr/bin/env bash
# Sync this checkout and run the Aspire AppHost on lxc-sandbox.
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
TARGET="${SANDBOX_SSH_TARGET:-}"
REMOTE_DIR="${SANDBOX_REPO_DIR:-}"

[[ -n "$TARGET" ]] || { printf 'usage: SANDBOX_SSH_TARGET=alias SANDBOX_REPO_DIR=/path %s\n' "$0" >&2; exit 2; }
[[ -n "$REMOTE_DIR" ]] || { printf 'SANDBOX_REPO_DIR must be an absolute path on lxc-sandbox\n' >&2; exit 2; }
[[ "$REMOTE_DIR" == /* ]] || { printf 'SANDBOX_REPO_DIR must be an absolute path on lxc-sandbox\n' >&2; exit 2; }

quote() {
  printf "'%s'" "${1//\'/\'\"\'\"\'}"
}

remote_dir_q="$(quote "$REMOTE_DIR")"

printf 'Syncing source to %s:%s\n' "$TARGET" "$REMOTE_DIR"
tar \
  --exclude='./.git' \
  --exclude='./.aspire' \
  --exclude='*/bin' \
  --exclude='*/obj' \
  --exclude='./ui/node_modules' \
  --exclude='./ui/dist' \
  -C "$ROOT" -cf - . |
  ssh "$TARGET" "mkdir -p -- $remote_dir_q && tar -xf - -C $remote_dir_q"

printf 'Running Aspire on the sandbox native Docker socket\n'
ssh -t "$TARGET" "cd $remote_dir_q && ./run/sandbox-preflight.sh && dotnet run --project src/ControlPlane.AppHost/ControlPlane.AppHost.csproj"
