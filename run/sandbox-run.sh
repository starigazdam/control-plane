#!/usr/bin/env bash
# Sync tracked files and run the Aspire AppHost on lxc-sandbox.
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
TARGET="${SANDBOX_SSH_TARGET:-}"
REMOTE_DIR="${SANDBOX_REPO_DIR:-}"

[[ -n "$TARGET" ]] || { printf 'SANDBOX_SSH_TARGET must name an SSH alias\n' >&2; exit 2; }
[[ "$TARGET" != -* ]] || { printf 'SANDBOX_SSH_TARGET must not begin with -\n' >&2; exit 2; }
[[ -n "$REMOTE_DIR" && "$REMOTE_DIR" == /* ]] || { printf 'SANDBOX_REPO_DIR must be an absolute path on lxc-sandbox\n' >&2; exit 2; }

quote() {
  printf "'%s'" "${1//\'/\'\"\'\"\'}"
}

remote_dir_q="$(quote "$REMOTE_DIR")"
sync_id="$(date +%s)-$$"
stage="$REMOTE_DIR/.sandbox-sync-$sync_id"
stage_q="$(quote "$stage")"

ssh "$TARGET" "test -f $remote_dir_q/.sandbox-checkout" || {
  printf 'refusing to sync: %s is not an initialized sandbox checkout\n' "$REMOTE_DIR" >&2
  printf 'create it once on the sandbox and touch .sandbox-checkout\n' >&2
  exit 3
}

cleanup() {
  ssh "$TARGET" "rm -rf -- $stage_q" >/dev/null 2>&1 || true
}
trap cleanup EXIT

printf 'Syncing tracked files only to %s:%s\n' "$TARGET" "$REMOTE_DIR"
ssh "$TARGET" "mkdir -- $stage_q"
git -C "$ROOT" ls-files -z |
  tar -C "$ROOT" --null --files-from=- -cf - |
  ssh "$TARGET" "tar --null --extract --file=- --directory=$stage_q"

ssh "$TARGET" "set -e
  cp -- $remote_dir_q/.sandbox-checkout $stage_q/.sandbox-checkout
  if [ -d $remote_dir_q/.aspire ]; then cp -a -- $remote_dir_q/.aspire $stage_q/.aspire; fi
  if [ -d $remote_dir_q/ui/node_modules ]; then mkdir -p -- $stage_q/ui; cp -a -- $remote_dir_q/ui/node_modules $stage_q/ui/node_modules; fi
  mv -- $remote_dir_q $remote_dir_q.previous
  mv -- $stage_q $remote_dir_q
  rm -rf -- $remote_dir_q.previous"
trap - EXIT

printf 'Running Aspire on the sandbox native Docker socket\n'
ssh -t "$TARGET" "cd $remote_dir_q && ./run/sandbox-preflight.sh && dotnet run --project src/ControlPlane.AppHost/ControlPlane.AppHost.csproj"
