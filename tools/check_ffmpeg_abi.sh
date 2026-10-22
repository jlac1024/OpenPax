#!/usr/bin/env bash
# Checks that a compiler sees FFmpeg's structs at the offsets the bindings use
# (src/OpenPax.Media/Video/FfmpegAbi.g.cs, generated on Linux), without
# running anything it builds: every offset becomes a constant in an assembly
# listing, which is read back. For a cross-compiler (MinGW for Windows) and
# for clang on a Mac.
#   tools/check_ffmpeg_abi.sh COMPILER FFMPEG_INCLUDE_DIR
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CC="$1"
INCLUDE="$2"
ABI="$ROOT/src/OpenPax.Media/Video/FfmpegAbi.g.cs"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# The FIELD lines of tools/ffmpeg_abi.c, as "const long long Name = offsetof(...);".
{
  echo '#include <stddef.h>'
  echo '#include <libavcodec/avcodec.h>'
  echo '#include <libavformat/avformat.h>'
  echo '#include <libavutil/avutil.h>'
  sed -n 's/^ *FIELD("\([A-Za-z]*\)", *\([A-Za-z]*\), *\([A-Za-z_.]*\));.*/const long long abi_\1 = offsetof(\2, \3);/p' "$ROOT/tools/ffmpeg_abi.c"
} > "$WORK/abi.c"

"$CC" -std=c11 -O0 -S -I "$INCLUDE" "$WORK/abi.c" -o "$WORK/abi.s"

status=0
while read -r name; do
  # In the listing: "abi_Name:" (with a leading "_" on macOS) then ".quad VALUE",
  # or ".zero 8" (".space 8") for 0.
  got="$(awk -v n="abi_$name:" '$1 == n || $1 == "_" n { getline; print ($1 == ".zero" || $1 == ".space") ? 0 : $2; exit }' "$WORK/abi.s")"
  want="$(sed -n "s/.*public const int $name = \([0-9]*\);.*/\1/p" "$ABI")"
  if [[ "$got" != "$want" ]]; then
    echo "check_ffmpeg_abi.sh: $name is $got for $CC, $want in FfmpegAbi.g.cs" >&2
    status=1
  fi
done < <(sed -n 's/^ *FIELD("\([A-Za-z]*\)".*/\1/p' "$ROOT/tools/ffmpeg_abi.c")

if (( status == 0 )); then
  echo "FFmpeg struct offsets for $CC match FfmpegAbi.g.cs"
fi

exit $status
