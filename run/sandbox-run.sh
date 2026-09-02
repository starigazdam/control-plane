#!/usr/bin/env bash
# Sync tracked files and run the Aspire AppHost on lxc-sandbox.
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
TARGET="${SANDBOX_SSH_TARGET:-}"
REMOTE_DIR="${SANDBOX_REPO_DIR:-}"
IDENTITY_FILE="${SANDBOX_SSH_IDENTITY_FILE:-}"

[[ -n "$TARGET" ]] || { printf 'SANDBOX_SSH_TARGET must name an SSH alias or host\n' >&2; exit 2; }
[[ "$TARGET" != -* ]] || { printf 'SANDBOX_SSH_TARGET must not begin with -\n' >&2; exit 2; }
[[ -n "$REMOTE_DIR" && "$REMOTE_DIR" == /* ]] || { printf 'SANDBOX_REPO_DIR must be an absolute path on lxc-sandbox\n' >&2; exit 2; }
REMOTE_DIR="${REMOTE_DIR%/}"
[[ "$REMOTE_DIR" != / && "$REMOTE_DIR" != *..* ]] || { printf 'SANDBOX_REPO_DIR must not be root or contain parent-directory components\n' >&2; exit 2; }
if [[ -n "$IDENTITY_FILE" ]]; then
  [[ -f "$IDENTITY_FILE" ]] || { printf 'SANDBOX_SSH_IDENTITY_FILE does not exist: %s\n' "$IDENTITY_FILE" >&2; exit 2; }
fi

SSH_ARGS=(-o BatchMode=yes)
if [[ -n "$IDENTITY_FILE" ]]; then
  SSH_ARGS+=(-i "$IDENTITY_FILE" -o IdentitiesOnly=yes)
fi
ssh_cmd() { ssh "${SSH_ARGS[@]}" "$@"; }

quote() {
  printf "'%s'" "${1//\'/\'\"\'\"\'}"
}

remote_dir_q="$(quote "$REMOTE_DIR")"
sync_id="$(date +%s)-$$"
stage="${REMOTE_DIR}.sandbox-sync-$sync_id"
previous="${REMOTE_DIR}.previous"
lock="${REMOTE_DIR}.lock"
stage_q="$(quote "$stage")"
previous_q="$(quote "$previous")"
lock_q="$(quote "$lock")"

ssh_cmd "$TARGET" "test -f $remote_dir_q/.sandbox-checkout" || {
  printf 'refusing to sync: %s is not an initialized sandbox checkout\n' "$REMOTE_DIR" >&2
  printf 'create it once on the sandbox and touch .sandbox-checkout\n' >&2
  exit 3
}

ssh_cmd "$TARGET" "mkdir -- $lock_q" || {
  printf 'refusing to sync: another sandbox runner holds %s.lock (inspect before removing it)\n' "$REMOTE_DIR" >&2
  exit 4
}

cleanup() {
  ssh_cmd "$TARGET" "rm -rf -- $stage_q $lock_q" >/dev/null 2>&1 || true
}
trap cleanup EXIT INT TERM HUP

printf 'Syncing tracked files only to %s:%s\n' "$TARGET" "$REMOTE_DIR"
ssh_cmd "$TARGET" "mkdir -- $stage_q"
git -C "$ROOT" ls-files -z |
  tar -C "$ROOT" --null --files-from=- -cf - |
  ssh_cmd "$TARGET" "tar --no-same-owner --null --extract --file=- --directory=$stage_q"

ssh_cmd "$TARGET" "set -e
  cp -- $remote_dir_q/.sandbox-checkout $stage_q/.sandbox-checkout
  if [ -d $remote_dir_q/.aspire ]; then
    mkdir -p -- $stage_q/.aspire
    cp -a -- $remote_dir_q/.aspire/. $stage_q/.aspire/
  fi
  if [ -d $remote_dir_q/ui/node_modules ]; then
    mkdir -p -- $stage_q/ui
    cp -a -- $remote_dir_q/ui/node_modules $stage_q/ui/node_modules
  fi
  if [ -f $remote_dir_q/.env.local ]; then
    cp -- $remote_dir_q/.env.local $stage_q/.env.local
  fi
  if [ -e $previous_q ]; then
    test -f $previous_q/.sandbox-checkout || { echo 'unsafe previous checkout; refusing replacement' >&2; exit 5; }
    rm -rf -- $previous_q
  fi
  mv -- $remote_dir_q $previous_q
  if ! mv -- $stage_q $remote_dir_q; then
    mv -- $previous_q $remote_dir_q
    exit 6
  fi
  rm -rf -- $previous_q"

if [[ "${SANDBOX_SYNC_ONLY:-0}" == 1 ]]; then
  printf 'Sandbox source synchronization complete; AppHost was not started\n'
  exit 0
fi

printf 'Running Aspire on the sandbox native Docker socket\n'
ssh_cmd -t "$TARGET" "export PATH=\"\$HOME/.dotnet:\$PATH\"; cd $remote_dir_q && ./run/sandbox-preflight.sh && dotnet run --project src/ControlPlane.AppHost/ControlPlane.AppHost.csproj"
