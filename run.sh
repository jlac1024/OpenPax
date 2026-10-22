#!/usr/bin/env bash
# Runs the latest build from deploy/. Never builds; run ./build.sh first.
#   ./run.sh [game args]
#   ./run.sh --lobby-server [server args]
#   ./run.sh --vanilla [game args]   the frozen Phase 4 single player build
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY="$ROOT/deploy"

if [[ "${1:-}" == "--lobby-server" ]]; then
  shift
  exe="$DEPLOY/lobby-server/openpax-lobby"
elif [[ "${1:-}" == "--vanilla" ]]; then
  shift
  # The frozen build is from before the rename to OpenPax.
  exe="$DEPLOY/vanilla/heliograph"
else
  exe="$DEPLOY/openpax"
fi

if [[ -e "$DEPLOY/.assembling" && "$exe" != "$DEPLOY/vanilla/"* ]]; then
  echo "run.sh: build.sh is assembling deploy/ (or its last run failed there); wait for it to finish, then run again" >&2
  exit 1
fi

if [[ ! -x "$exe" ]]; then
  echo "run.sh: $exe not found, run ./build.sh first" >&2
  exit 1
fi

# Dev settings live in build/dev/. The game does not read this yet; it will
# once config lands, so dev runs never touch the real per-user directory.
mkdir -p "$ROOT/build/dev"
export OPENPAX_DEV_DIR="$ROOT/build/dev"

exec "$exe" "$@"
