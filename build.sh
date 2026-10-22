#!/usr/bin/env bash
# Checks, tests, builds and assembles deploy/. Safe to run often.
#   ./build.sh            full build
#   ./build.sh --no-test  skip dotnet test (format and build still run)
#   ./build.sh --full     redo every step, ignoring build/stamps/
#   ./build.sh --dist     then also the Windows and macOS builds (tools/dist.sh, docs/platforms.md)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT"

RUN_TESTS=1
FULL=0
DIST=0
for arg in "$@"; do
  case "$arg" in
    --no-test) RUN_TESTS=0 ;;
    --full) FULL=1 ;;
    --dist) DIST=1 ;;
    *) echo "build.sh: unknown argument $arg" >&2; exit 2 ;;
  esac
done

step() { printf '\n== %s\n' "$*"; }

# Steps record what they last ran on under build/stamps/ and are skipped
# when nothing they depend on has changed since, so a build after a small
# change only does the work that change needs. --full ignores the stamps.
STAMPS="$ROOT/build/stamps"
mkdir -p "$STAMPS"
# A fingerprint of a set of files: their names, sizes and modification times.
# A path that is not there (no imported data, as on CI) counts as empty.
fingerprint() { { find "$@" -type f -printf '%p %s %T@\n' 2>/dev/null || true; } | sort | md5sum | cut -d' ' -f1; }
fresh() { [[ $FULL == 0 && -f "$STAMPS/$1" && "$(cat "$STAMPS/$1")" == "$2" ]]; }
deps() { python3 -I "$ROOT/tools/deps.py" "$@"; }

case "$(uname -s)-$(uname -m)" in
  Linux-x86_64) RID=linux-x64; SDL_LIB=libSDL3.so.0 ;;
  Linux-aarch64) RID=linux-arm64; SDL_LIB=libSDL3.so.0 ;;
  Darwin-arm64) RID=osx-arm64; SDL_LIB=libSDL3.0.dylib ;;
  Darwin-x86_64) RID=osx-x64; SDL_LIB=libSDL3.0.dylib ;;
  *) echo "build.sh: unsupported platform $(uname -s)-$(uname -m)" >&2; exit 1 ;;
esac

NATIVE="$ROOT/build/native"
SDL_PREFIX="$NATIVE/sdl3"

step "SDL3"
deps fetch sdl3
SDL_PIN="$(deps get sdl3 sha256)"
if [[ -f "$SDL_PREFIX/.pin" && "$(cat "$SDL_PREFIX/.pin")" == "$SDL_PIN" && -e "$SDL_PREFIX/lib/$SDL_LIB" ]]; then
  echo "SDL3 $(deps get sdl3 version) already built"
else
  rm -rf "$NATIVE/sdl3-build" "$SDL_PREFIX"
  cmake -S "$(deps path sdl3)" -B "$NATIVE/sdl3-build" \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_INSTALL_PREFIX="$SDL_PREFIX" \
    -DCMAKE_INSTALL_LIBDIR=lib \
    -DSDL_SHARED=ON -DSDL_STATIC=OFF \
    -DSDL_TESTS=OFF -DSDL_EXAMPLES=OFF -DSDL_INSTALL_DOCS=OFF
  cmake --build "$NATIVE/sdl3-build" --parallel 4
  cmake --install "$NATIVE/sdl3-build"
  echo "$SDL_PIN" > "$SDL_PREFIX/.pin"
fi

step "FFmpeg"
# Import tool (with x264) and the decode-only libraries the game ships.
"$ROOT/tools/build_ffmpeg.sh"
# The bindings read FFmpeg structs at generated offsets; they must match
# the headers of the libraries being shipped.
"$ROOT/tools/gen_ffmpeg_abi.sh" "$ROOT/build/native/FfmpegAbi.g.cs"
if ! cmp -s "$ROOT/build/native/FfmpegAbi.g.cs" "$ROOT/src/OpenPax.Media/Video/FfmpegAbi.g.cs"; then
  echo "build.sh: FFmpeg offsets changed; copy build/native/FfmpegAbi.g.cs over src/OpenPax.Media/Video/FfmpegAbi.g.cs and check the bindings" >&2
  exit 1
fi

step "restore"
# Once more if the runtime dies at start (the same "Internal CLR error" the
# tests see now and then on this machine).
if ! dotnet restore OpenPax.slnx --nologo; then
  echo "restore failed; trying once more"
  dotnet restore OpenPax.slnx --nologo
fi

step "format"
# Only the C# files changed since the last clean check are checked, unless
# a style setting changed (then all are).
FORMAT_STAMP="$STAMPS/format"
FORMAT_NEXT="$STAMPS/format.next"
touch "$FORMAT_NEXT"
if [[ $FULL == 1 || ! -f "$FORMAT_STAMP" || .editorconfig -nt "$FORMAT_STAMP" || Directory.Build.props -nt "$FORMAT_STAMP" ]]; then
  dotnet format OpenPax.slnx --verify-no-changes --no-restore
else
  mapfile -t CHANGED < <(find src tests -name '*.cs' -newer "$FORMAT_STAMP" -not -path '*/obj/*' -not -path '*/bin/*')
  if [[ ${#CHANGED[@]} -eq 0 ]]; then
    echo "no C# files changed"
  else
    echo "${#CHANGED[@]} changed file(s)"
    dotnet format OpenPax.slnx --verify-no-changes --no-restore --include "${CHANGED[@]}"
  fi
fi
mv "$FORMAT_NEXT" "$FORMAT_STAMP"

step "build"
# Parallelism is capped throughout: the development machine has 8 GB of
# memory, and uncapped builds, tests and encodes starved it.
dotnet build OpenPax.slnx -c Release --nologo --no-restore -maxcpucount:4
# The compiler server and build nodes stay resident (about 600 MB) after a
# build; the tests and the import need that memory more than the next
# build needs a warm compiler.
dotnet build-server shutdown > /dev/null

step "import"
# Before the tests, which read the imported data and are fingerprinted by it.
# Re-import when there is no import yet or the data code is newer than it.
# The game files themselves are checked against the GOG hashes on every import.
DATA_DLL="$ROOT/build/artifacts/bin/OpenPax.Data/release/OpenPax.Data.dll"
STAMP="$ROOT/build/data/import.toml"
if [[ ! -d "$ROOT/references/gog" ]]; then
  echo "references/gog/ not found, skipping import"
elif [[ -f "$STAMP" && "$STAMP" -nt "$DATA_DLL" ]]; then
  echo "build/data/ is up to date"
else
  dotnet "$ROOT/build/artifacts/bin/OpenPax.Host/release/openpax.dll" --import "$ROOT/references/gog" "$ROOT/build/data" \
    --ffmpeg "$ROOT/build/native/ffmpeg-tool/bin/ffmpeg"
fi

# The modern text font goes beside the game's own fonts, with its license.
if [[ -d "$ROOT/build/data/fonts" ]]; then
  deps fetch titillium-web
  mkdir -p "$ROOT/build/data/fonts/modern"
  cp -p "$ROOT/deps/fonts/titillium-web/"* "$ROOT/build/data/fonts/modern/"
fi

if [[ $RUN_TESTS == 1 ]]; then
  step "test"
  # Exit code 8 means a test project has no tests yet, which is expected
  # for projects whose phase has not started.
  # One test project at a time: two at once ran this 8 GB machine short and
  # crashed the runtime ("Internal CLR error"). A project runs only when its
  # assemblies (its own and every one it was built against) or the imported
  # data changed since it last passed.
  for proj in tests/*/*.csproj; do
    name="$(basename "$proj" .csproj)"
    bin="$ROOT/build/artifacts/bin/$name/release"
    key="$(fingerprint "$bin" -name '*.dll' ; fingerprint "$ROOT/build/data/import.toml")"
    # The mod tests load the sample mods from their own folders, not their bin.
    if [[ $name == OpenPax.Modding.Tests ]]; then
      key="$key $(fingerprint "$ROOT/build/artifacts/bin/SimpleCombat/release" "$ROOT/build/artifacts/bin/Bounty/release" -name '*.dll')"
    fi
    if fresh "test-$name" "$key"; then
      echo "$name: unchanged since it passed"
      continue
    fi
    # The .NET runtime on this machine now and then dies at start with
    # "Internal CLR error" (dotnet restore has too, with memory to spare),
    # so a project whose run crashed that way gets one more go. A test that
    # fails still fails the build.
    out="$STAMPS/test-$name.log"
    if ! dotnet test --project "$proj" -c Release --no-build --ignore-exit-code 8 2>&1 | tee "$out"; then
      if grep -q "Internal CLR error" "$out"; then
        echo "$name: the runtime crashed; running it again"
        dotnet test --project "$proj" -c Release --no-build --ignore-exit-code 8
      else
        exit 1
      fi
    fi
    rm -f "$out"
    echo "$key" > "$STAMPS/test-$name"
  done
fi

step "publish"
# Published again only when the game's or the lobby server's assemblies changed.
PUBLISH="$ROOT/build/publish"
PUBLISH_KEY="$RID $(fingerprint "$ROOT/build/artifacts/bin/OpenPax.Host/release" "$ROOT/build/artifacts/bin/OpenPax.LobbyServer/release" -name '*.dll')"
if fresh publish "$PUBLISH_KEY" && [[ -d "$PUBLISH/game" && -d "$PUBLISH/lobby-server" ]]; then
  echo "game and lobby server unchanged"
else
  rm -rf "$PUBLISH"
  dotnet publish src/OpenPax.Host/OpenPax.Host.csproj -c Release -r "$RID" --self-contained --nologo -o "$PUBLISH/game"
  dotnet publish src/OpenPax.LobbyServer/OpenPax.LobbyServer.csproj -c Release -r "$RID" --self-contained --nologo -o "$PUBLISH/lobby-server"
  echo "$PUBLISH_KEY" > "$STAMPS/publish"
fi

step "deploy"
DEPLOY="$ROOT/deploy"
# deploy/vanilla/ is a frozen copy of the Phase 4 single player build, made
# once by hand; it is kept across builds.
mkdir -p "$DEPLOY"
# run.sh refuses to start while this marker is there, so a game is never
# started from a half-assembled folder. A failed build leaves it behind on
# purpose: the folder is not whole until the next good build.
MARKER="$DEPLOY/.assembling"
touch "$MARKER"
# The native libraries join the published game first, so deploy/ is one
# mirror of it. -L: ship the real file under the SONAME, not a symlink chain.
cp -Lp "$SDL_PREFIX/lib/$SDL_LIB" "$PUBLISH/game/$SDL_LIB"
# The decode-only FFmpeg libraries, as real files under their sonames.
for lib in "$ROOT"/build/native/ffmpeg-libs/lib/lib{avutil,avcodec,avformat}.so.*; do
  [[ "$lib" =~ \.so\.[0-9]+$ ]] && cp -Lp "$lib" "$PUBLISH/game/$(basename "$lib")"
done
# The sample mods ship in deploy/mods/ (docs/modding.md), off until turned
# on in Options. Night Menu's picture is made from the imported data here,
# so no game art is in the repository.
# What the first run needs beside the program (docs/first-run.md): the
# modern font for its text, and the ffmpeg tool to convert the movies.
deps fetch titillium-web
mkdir -p "$PUBLISH/game/fonts/modern" "$PUBLISH/game/tools"
cp -p "$ROOT/deps/fonts/titillium-web/"* "$PUBLISH/game/fonts/modern/"
cp -p "$ROOT/build/native/ffmpeg-tool/bin/ffmpeg" "$PUBLISH/game/tools/ffmpeg"
SAMPLES="$PUBLISH/game/mods"
rm -rf "$SAMPLES"
mkdir -p "$SAMPLES/simple-combat"
SC_BIN="$ROOT/build/artifacts/bin/SimpleCombat/release"
cp -p "$SC_BIN/SimpleCombat.dll" "$SC_BIN/mod.toml" "$SC_BIN/simple-combat.toml" "$SAMPLES/simple-combat/"
# Arsenal is content only: its folder as it is.
cp -rp "$ROOT/samples/Arsenal" "$SAMPLES/arsenal"
mkdir -p "$SAMPLES/bounty"
BOUNTY_BIN="$ROOT/build/artifacts/bin/Bounty/release"
cp -p "$BOUNTY_BIN/Bounty.dll" "$BOUNTY_BIN/mod.toml" "$BOUNTY_BIN/settings.toml" "$SAMPLES/bounty/"
# The modding docs (docs/modding.md): the API reference is written again
# from the assemblies (docs/modding/api.md, only when it changed), and the
# pages become a small website in deploy/docs/modding/.
rm -rf "$PUBLISH/game/docs"
dotnet "$ROOT/build/artifacts/bin/ApiDocs/release/ApiDocs.dll" "$ROOT/docs" "$PUBLISH/game/docs/modding"
if [[ -d "$ROOT/build/data" ]]; then
  dotnet "$ROOT/build/artifacts/bin/NightMenu/release/NightMenu.dll" "$ROOT/build/data" "$SAMPLES/night-menu"
fi
# rsync copies only what differs (size or time), so the 800 MB of imported
# data is not copied again when it has not changed; --delete drops what the
# build no longer makes. The hand-kept parts of deploy/ are left alone.
rsync -a --delete --exclude /vanilla --exclude /.assembling --exclude /config.json --exclude /lobby-server --exclude /data "$PUBLISH/game/" "$DEPLOY/"
rsync -a --delete "$PUBLISH/lobby-server/" "$DEPLOY/lobby-server/"
if [[ -d "$ROOT/build/data" ]]; then
  rsync -a --delete "$ROOT/build/data/" "$DEPLOY/data/"
fi
# config.json is edited by hand (the public lobby's address), so it is
# written only when missing and kept across builds.
if [[ ! -f "$DEPLOY/config.json" ]]; then
  cp "$ROOT/tools/config.json" "$DEPLOY/config.json"
fi
rm -f "$MARKER"
echo "deploy/ ready ($RID)"

if [[ $DIST == 1 ]]; then
  step "dist"
  "$ROOT/tools/dist.sh"
fi
