#!/usr/bin/env bash
# Writes the FFmpeg struct offsets used by OpenPax.Media's bindings, as C#.
#   tools/gen_ffmpeg_abi.sh OUT.cs
# build.sh runs it into build/ and fails if the committed copy
# (src/OpenPax.Media/Video/FfmpegAbi.g.cs) differs, so a new FFmpeg pin
# cannot silently break the bindings.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LIBS="$ROOT/build/native/ffmpeg-libs"
EXE="$ROOT/build/native/ffmpeg_abi"
gcc -std=c11 -O0 -I "$LIBS/include" "$ROOT/tools/ffmpeg_abi.c" -L "$LIBS/lib" -lavutil -o "$EXE"
LD_LIBRARY_PATH="$LIBS/lib" "$EXE" > "$1"
