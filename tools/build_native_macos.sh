#!/usr/bin/env bash
# Builds the game's native libraries for Apple Silicon Macs, on a Mac
# (docs/platforms.md): in the repository on GitHub's Mac runners
# (.github/workflows), or from the macOS native kit that build.sh --dist
# makes (build/dist/macos-native-kit/), which holds this script, the pins
# (tools/deps.toml) and what the FFmpeg check needs:
#   ./tools/build_native_macos.sh
# Needs the Xcode command line tools (xcode-select --install), CMake and
# pkg-config (brew install cmake pkg-config). Writes build/native-osx-arm64/:
#   libSDL3.0.dylib
#   libavutil.N.dylib, libavcodec.N.dylib, libavformat.N.dylib   decode-only LGPL FFmpeg
#   ffmpeg   the movie converter the first run uses: FFmpeg with x264 (GPL)
# and, for the kit, native-osx-arm64.tar.gz of it, to copy back to the Linux
# machine's build/ folder.
set -euo pipefail

KIT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="$KIT/build/native-osx-arm64-build"
OUT="$KIT/build/native-osx-arm64"
JOBS="${JOBS:-4}"
export MACOSX_DEPLOYMENT_TARGET=13.0

if [[ "$(uname -s)-$(uname -m)" != "Darwin-arm64" ]]; then
  echo "build_native_macos.sh: run this on an Apple Silicon Mac" >&2
  exit 1
fi

for tool in clang make cmake curl shasum tar pkg-config; do
  if ! command -v "$tool" > /dev/null; then
    echo "build_native_macos.sh: $tool not found (Xcode command line tools: xcode-select --install; then brew install cmake pkg-config)" >&2
    exit 1
  fi
done

# A pin's field from deps.toml: pin NAME FIELD.
pin() { awk -v s="[$1]" -v k="$2" '$0 == s { on = 1; next } /^\[/ { on = 0 } on && $1 == k { gsub(/"/, "", $3); print $3; exit }' "$KIT/tools/deps.toml"; }

# Downloads and unpacks a pinned source, checking its hash.
fetch() {
  local name="$1" url sha unpack file
  url="$(pin "$name" url)"
  sha="$(pin "$name" sha256)"
  unpack="$(pin "$name" unpack)"
  file="$WORK/$(basename "$url")"
  if [[ ! -f "$file" ]]; then
    curl -fL -o "$file" "$url"
  fi

  if [[ "$(shasum -a 256 "$file" | cut -d' ' -f1)" != "$sha" ]]; then
    echo "build_native_macos.sh: $file does not match its pinned hash" >&2
    exit 1
  fi

  rm -rf "$WORK/$unpack"
  tar -xf "$file" -C "$WORK"
  echo "$WORK/$unpack"
}

# Built again only when a pinned source or this script changed: the stamp
# in $OUT says what it was built from, so a cached build/ (the release
# workflow) is used as it is.
STAMP="$(pin sdl3 sha256)-$(pin ffmpeg sha256)-$(pin x264 sha256)-$(shasum -a 256 "$KIT/tools/build_native_macos.sh" | cut -c1-16)"
if [[ -f "$OUT/.pin" && "$(cat "$OUT/.pin")" == "$STAMP" && -f "$OUT/libSDL3.0.dylib" && -f "$OUT/ffmpeg" ]] && compgen -G "$OUT/libavcodec.*.dylib" > /dev/null; then
  echo "Native libraries for macOS already built"
else
  mkdir -p "$WORK"
  rm -rf "$OUT"
  mkdir -p "$OUT"

  SDL_SRC="$(fetch sdl3)"
  cmake -S "$SDL_SRC" -B "$WORK/sdl3-build" -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_OSX_ARCHITECTURES=arm64 -DCMAKE_INSTALL_PREFIX="$WORK/sdl3-install" \
    -DSDL_SHARED=ON -DSDL_STATIC=OFF -DSDL_TESTS=OFF -DSDL_EXAMPLES=OFF -DSDL_INSTALL_DOCS=OFF
  cmake --build "$WORK/sdl3-build" --parallel "$JOBS"
  cmake --install "$WORK/sdl3-build"
  cp -L "$WORK/sdl3-install/lib/libSDL3.0.dylib" "$OUT/"

  # The same decode-only LGPL set as Linux's (tools/build_ffmpeg.sh). Each
  # library names the others by @loader_path, so they are found beside it.
  FFMPEG_SRC="$(fetch ffmpeg)"
  rm -rf "$WORK/ffmpeg-build" "$WORK/ffmpeg-install"
  mkdir -p "$WORK/ffmpeg-build"
  (cd "$WORK/ffmpeg-build" && "$FFMPEG_SRC/configure" \
    --prefix="$WORK/ffmpeg-install" --arch=arm64 --cc=clang \
    --install-name-dir='@loader_path' \
    --enable-shared --disable-static --disable-autodetect \
    --disable-programs --disable-doc --disable-network --disable-everything \
    --disable-avdevice --disable-avfilter --disable-swscale --disable-swresample \
    --enable-decoder=h264,aac --enable-parser=h264,aac --enable-demuxer=mov \
    --enable-protocol=file \
    && make -j"$JOBS" && make install)
  for lib in avutil avcodec avformat; do
    cp -L "$WORK"/ffmpeg-install/lib/lib$lib.[0-9]*.dylib "$OUT/" 2> /dev/null || true
  done
  rm -f "$OUT"/lib*.*.*.*.dylib

  "$KIT/tools/check_ffmpeg_abi.sh" clang "$WORK/ffmpeg-install/include"

  # The movie converter: x264, then FFmpeg's program linked with it.
  X264_SRC="$(fetch x264)"
  rm -rf "$WORK/x264-build" "$WORK/x264-install" "$WORK/ffmpeg-tool-build"
  cp -a "$X264_SRC" "$WORK/x264-build"
  (cd "$WORK/x264-build" && ./configure --prefix="$WORK/x264-install" --enable-static --disable-cli --enable-pic && make -j"$JOBS" && make install)
  mkdir -p "$WORK/ffmpeg-tool-build"
  (cd "$WORK/ffmpeg-tool-build" && PKG_CONFIG_PATH="$WORK/x264-install/lib/pkgconfig" "$FFMPEG_SRC/configure" \
    --arch=arm64 --cc=clang --enable-gpl --enable-libx264 --disable-autodetect --pkg-config-flags=--static \
    --disable-shared --enable-static --disable-doc --disable-ffplay --disable-ffprobe --disable-network \
    && make -j"$JOBS")
  cp "$WORK/ffmpeg-tool-build/ffmpeg" "$OUT/ffmpeg"
  echo "$STAMP" > "$OUT/.pin"
fi

tar -czf "$KIT/native-osx-arm64.tar.gz" -C "$KIT/build" native-osx-arm64
echo
echo "Done: $OUT, and $KIT/native-osx-arm64.tar.gz of it."
echo "From the kit: copy the .tar.gz to the Linux machine's build/ folder, then run ./build.sh --dist there."
