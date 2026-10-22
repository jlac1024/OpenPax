#!/usr/bin/env bash
# Cross-compiles the game's native libraries for Windows x64 on Linux with
# MinGW-w64 (docs/platforms.md), into build/native-win-x64/:
#   SDL3.dll
#   avutil-N.dll, avcodec-N.dll, avformat-N.dll   decode-only LGPL FFmpeg
#   ffmpeg.exe   the movie converter the first run uses: FFmpeg with x264 (GPL), static
# Each is rebuilt only when its source pin changes. The C runtime pieces are
# linked in statically, so the DLLs need nothing but Windows itself.
# Called by build.sh --dist.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/build/native-win-x64"
WORK="$ROOT/build/native-win-x64-build"
CROSS=x86_64-w64-mingw32
# Few compile jobs by default, so a desktop in use stays responsive; CI sets JOBS higher.
JOBS="${JOBS:-2}"
deps() { python3 -I "$ROOT/tools/deps.py" "$@"; }
current() { [[ -f "$1" && "$(cat "$1")" == "$2" ]]; }

# SDL3 has some C++ for Windows, so both compilers are needed.
for compiler in "$CROSS-gcc" "$CROSS-g++"; do
  if ! command -v "$compiler" > /dev/null; then
    echo "build_native_windows.sh: $compiler not found; the MinGW-w64 cross-compilers are needed (Ubuntu: gcc-mingw-w64-x86-64 and g++-mingw-w64-x86-64)" >&2
    exit 1
  fi
done

deps fetch sdl3 ffmpeg x264
mkdir -p "$OUT" "$WORK"

FFMPEG_PIN="$(deps get ffmpeg sha256)"
if current "$OUT/.ffmpeg-pin" "$FFMPEG_PIN" && compgen -G "$OUT/avcodec-*.dll" > /dev/null; then
  echo "FFmpeg libraries for Windows already built"
else
  rm -rf "$WORK/ffmpeg" "$WORK/ffmpeg-install"
  mkdir -p "$WORK/ffmpeg"
  # The same decode-only LGPL set as Linux's (tools/build_ffmpeg.sh); Windows threads, no pthreads DLL.
  (cd "$WORK/ffmpeg" && "$(deps path ffmpeg)/configure" \
    --prefix="$WORK/ffmpeg-install" --enable-cross-compile --cross-prefix="$CROSS-" \
    --arch=x86_64 --target-os=mingw32 --extra-ldflags=-static-libgcc \
    --enable-shared --disable-static --disable-autodetect --enable-w32threads \
    --disable-programs --disable-doc --disable-network --disable-everything \
    --disable-avdevice --disable-avfilter --disable-swscale --disable-swresample \
    --enable-decoder=h264,aac --enable-parser=h264,aac --enable-demuxer=mov \
    --enable-protocol=file \
    && make -j"$JOBS" && make install)
  rm -f "$OUT"/avutil-*.dll "$OUT"/avcodec-*.dll "$OUT"/avformat-*.dll
  cp "$WORK"/ffmpeg-install/bin/{avutil,avcodec,avformat}-*.dll "$OUT/"

  # The bindings read FFmpeg's structs at offsets generated on Linux
  # (FfmpegAbi.g.cs); Windows' must be the same. Worked out by the compiler
  # alone, as constants in an assembly listing, since a Windows program
  # cannot run here.
  "$ROOT/tools/check_ffmpeg_abi.sh" "$CROSS-gcc" "$WORK/ffmpeg-install/include"
  echo "$FFMPEG_PIN" > "$OUT/.ffmpeg-pin"
fi

SDL_PIN="$(deps get sdl3 sha256)"
if current "$OUT/.sdl3-pin" "$SDL_PIN" && [[ -f "$OUT/SDL3.dll" ]]; then
  echo "SDL3 for Windows already built"
else
  rm -rf "$WORK/sdl3"
  cat > "$WORK/mingw.cmake" <<CMAKE
set(CMAKE_SYSTEM_NAME Windows)
set(CMAKE_SYSTEM_PROCESSOR x86_64)
set(CMAKE_C_COMPILER $CROSS-gcc)
set(CMAKE_CXX_COMPILER $CROSS-g++)
set(CMAKE_RC_COMPILER $CROSS-windres)
set(CMAKE_FIND_ROOT_PATH /usr/$CROSS)
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
CMAKE
  cmake -S "$(deps path sdl3)" -B "$WORK/sdl3" \
    -DCMAKE_TOOLCHAIN_FILE="$WORK/mingw.cmake" -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_INSTALL_PREFIX="$WORK/sdl3-install" \
    -DCMAKE_SHARED_LINKER_FLAGS="-static-libgcc" \
    -DSDL_SHARED=ON -DSDL_STATIC=OFF -DSDL_TESTS=OFF -DSDL_EXAMPLES=OFF -DSDL_INSTALL_DOCS=OFF
  cmake --build "$WORK/sdl3" --parallel "$JOBS"
  cmake --install "$WORK/sdl3"
  cp "$WORK/sdl3-install/bin/SDL3.dll" "$OUT/SDL3.dll"
  echo "$SDL_PIN" > "$OUT/.sdl3-pin"
fi

# The movie converter: x264, then FFmpeg's program linked with it, both
# static. Only the import step runs it, on the player's machine.
X264_PIN="$(deps get x264 sha256)"
TOOL_PIN="$FFMPEG_PIN-$X264_PIN"
if current "$OUT/.tool-pin" "$TOOL_PIN" && [[ -f "$OUT/ffmpeg.exe" ]]; then
  echo "ffmpeg.exe already built"
else
  rm -rf "$WORK/x264" "$WORK/x264-install" "$WORK/ffmpeg-tool"
  cp -a "$(deps path x264)" "$WORK/x264"
  (cd "$WORK/x264" && ./configure --host="$CROSS" --cross-prefix="$CROSS-" --prefix="$WORK/x264-install" \
    --enable-static --disable-cli --enable-pic && make -j"$JOBS" && make install)
  mkdir -p "$WORK/ffmpeg-tool"
  (cd "$WORK/ffmpeg-tool" && PKG_CONFIG_PATH="$WORK/x264-install/lib/pkgconfig" "$(deps path ffmpeg)/configure" \
    --enable-cross-compile --cross-prefix="$CROSS-" --arch=x86_64 --target-os=mingw32 --pkg-config=pkg-config \
    --enable-gpl --enable-libx264 --disable-autodetect --enable-w32threads --pkg-config-flags=--static \
    --extra-ldflags="-static -static-libgcc" --disable-shared --enable-static \
    --disable-doc --disable-ffplay --disable-ffprobe --disable-network \
    && make -j"$JOBS")
  cp "$WORK/ffmpeg-tool/ffmpeg.exe" "$OUT/ffmpeg.exe"
  echo "$TOOL_PIN" > "$OUT/.tool-pin"
fi

echo "Windows native libraries in $OUT"
