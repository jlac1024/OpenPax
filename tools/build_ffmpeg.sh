#!/usr/bin/env bash
# Builds the pinned FFmpeg and x264 sources from deps/ into build/native/:
#   x264/          static x264, only linked into the tool below
#   ffmpeg-tool/   ffmpeg program with x264 (GPL), used by the import step,
#                  never shipped
#   ffmpeg-libs/   decode-only LGPL shared libraries the game ships:
#                  avcodec (H.264, AAC), avformat (MP4), avutil
# Each part is rebuilt only when its source pin changes. Called by build.sh.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NATIVE="$ROOT/build/native"
# At most 4 compile jobs, so the build leaves memory for everything else.
JOBS="$(nproc 2>/dev/null || echo 4)"
(( JOBS > 4 )) && JOBS=4
deps() { python3 -I "$ROOT/tools/deps.py" "$@"; }

deps fetch ffmpeg x264
FFMPEG_SRC="$(deps path ffmpeg)"
X264_SRC="$(deps path x264)"
FFMPEG_PIN="$(deps get ffmpeg sha256)"
X264_PIN="$(deps get x264 sha256)"

current() { [[ -f "$1/.pin" && "$(cat "$1/.pin")" == "$2" ]]; }

X264_OUT="$NATIVE/x264"
if current "$X264_OUT" "$X264_PIN"; then
  echo "x264 already built"
else
  rm -rf "$NATIVE/x264-build" "$X264_OUT"
  cp -a "$X264_SRC" "$NATIVE/x264-build"
  (cd "$NATIVE/x264-build" && ./configure --prefix="$X264_OUT" --enable-static --enable-pic --disable-cli && make -j"$JOBS" && make install)
  echo "$X264_PIN" > "$X264_OUT/.pin"
fi

TOOL_OUT="$NATIVE/ffmpeg-tool"
TOOL_PIN="$FFMPEG_PIN-$X264_PIN"
if current "$TOOL_OUT" "$TOOL_PIN"; then
  echo "ffmpeg tool already built"
else
  rm -rf "$NATIVE/ffmpeg-tool-build" "$TOOL_OUT"
  mkdir -p "$NATIVE/ffmpeg-tool-build"
  # --disable-autodetect keeps system libraries out, so the tool behaves
  # the same on every machine.
  (cd "$NATIVE/ffmpeg-tool-build" && PKG_CONFIG_PATH="$X264_OUT/lib/pkgconfig" "$FFMPEG_SRC/configure" \
    --prefix="$TOOL_OUT" --enable-gpl --enable-libx264 --disable-autodetect \
    --disable-doc --disable-ffplay --disable-network --pkg-config-flags=--static \
    && make -j"$JOBS" && make install)
  echo "$TOOL_PIN" > "$TOOL_OUT/.pin"
fi

LIBS_OUT="$NATIVE/ffmpeg-libs"
if current "$LIBS_OUT" "$FFMPEG_PIN"; then
  echo "ffmpeg libraries already built"
else
  rm -rf "$NATIVE/ffmpeg-libs-build" "$LIBS_OUT"
  mkdir -p "$NATIVE/ffmpeg-libs-build"
  # LGPL (no --enable-gpl) and nothing but what MP4 playback needs.
  (cd "$NATIVE/ffmpeg-libs-build" && "$FFMPEG_SRC/configure" \
    --prefix="$LIBS_OUT" --enable-shared --disable-static --disable-autodetect \
    --disable-programs --disable-doc --disable-network --disable-everything \
    --disable-avdevice --disable-avfilter --disable-swscale --disable-swresample \
    --enable-decoder=h264,aac --enable-parser=h264,aac --enable-demuxer=mov \
    --enable-protocol=file \
    && make -j"$JOBS" && make install)
  echo "$FFMPEG_PIN" > "$LIBS_OUT/.pin"
fi
