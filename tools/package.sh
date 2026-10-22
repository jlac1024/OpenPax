#!/usr/bin/env bash
# Makes a release bundle of OpenPax for one platform (docs/platforms.md),
# with no game data: the player's own copy is imported on first run
# (docs/first-run.md). Run after the platform's native libraries are built:
#   linux-x64    build.sh (build/native/)
#   win-x64      tools/build_native_windows.sh (build/native-win-x64/)
#   osx-arm64    tools/build_native_macos.sh, on a Mac (build/native-osx-arm64/)
#   tools/package.sh RID VERSION
# Writes build/release/OpenPax-VERSION-RID/ and an archive of it beside:
# .zip for Windows, .tar.gz for Linux and macOS (keeping files executable).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RID="$1"
VERSION="$2"
NAME="OpenPax-$VERSION-$RID"
RELEASE="$ROOT/build/release"
WORK="$RELEASE/publish-$RID"
OUT="$RELEASE/$NAME"
deps() { python3 -I "$ROOT/tools/deps.py" "$@"; }

rm -rf "$WORK" "$OUT"
mkdir -p "$WORK" "$RELEASE"

# The program and the lobby server, self-contained for the platform.
dotnet publish "$ROOT/src/OpenPax.Host/OpenPax.Host.csproj" -c Release -r "$RID" --self-contained --nologo -o "$WORK/game"
dotnet publish "$ROOT/src/OpenPax.LobbyServer/OpenPax.LobbyServer.csproj" -c Release -r "$RID" --self-contained --nologo -o "$WORK/lobby-server"
# The sample mods (C#, any platform) and the docs site.
for sample in SimpleCombat Bounty; do
  dotnet build "$ROOT/samples/$sample/$sample.csproj" -c Release --nologo -v q
done

dotnet build "$ROOT/tools/ApiDocs/ApiDocs.csproj" -c Release --nologo -v q

GAME="$WORK/game"

# Native libraries and the movie converter beside the program.
case "$RID" in
  linux-x64)
    cp -L "$ROOT/build/native/sdl3/lib/libSDL3.so.0" "$GAME/"
    for lib in "$ROOT"/build/native/ffmpeg-libs/lib/lib{avutil,avcodec,avformat}.so.*; do
      [[ "$lib" =~ \.so\.[0-9]+$ ]] && cp -L "$lib" "$GAME/$(basename "$lib")"
    done
    mkdir -p "$GAME/tools"
    cp "$ROOT/build/native/ffmpeg-tool/bin/ffmpeg" "$GAME/tools/ffmpeg"
    ;;
  win-x64)
    cp "$ROOT"/build/native-win-x64/*.dll "$GAME/"
    mkdir -p "$GAME/tools"
    cp "$ROOT/build/native-win-x64/ffmpeg.exe" "$GAME/tools/ffmpeg.exe"
    ;;
  osx-arm64)
    cp "$ROOT"/build/native-osx-arm64/*.dylib "$GAME/"
    mkdir -p "$GAME/tools"
    cp "$ROOT/build/native-osx-arm64/ffmpeg" "$GAME/tools/ffmpeg"
    ;;
  *)
    echo "package.sh: no bundle for $RID" >&2
    exit 2
    ;;
esac

# The modern font, for the first run's text and the game's.
deps fetch titillium-web
mkdir -p "$GAME/fonts/modern"
cp "$ROOT/deps/fonts/titillium-web/"* "$GAME/fonts/modern/"

# Sample mods that need nothing of the game's: Simple Combat, Bounty, Arsenal.
# (Night Menu is made from the player's imported data, so it is not shipped.)
mkdir -p "$GAME/mods/simple-combat" "$GAME/mods/bounty"
SC="$ROOT/build/artifacts/bin/SimpleCombat/release"
cp "$SC/SimpleCombat.dll" "$SC/mod.toml" "$SC/simple-combat.toml" "$GAME/mods/simple-combat/"
BOUNTY="$ROOT/build/artifacts/bin/Bounty/release"
cp "$BOUNTY/Bounty.dll" "$BOUNTY/mod.toml" "$BOUNTY/settings.toml" "$GAME/mods/bounty/"
cp -r "$ROOT/samples/Arsenal" "$GAME/mods/arsenal"
dotnet "$ROOT/build/artifacts/bin/ApiDocs/release/ApiDocs.dll" "$ROOT/docs" "$GAME/docs/modding" > /dev/null
cp "$ROOT/tools/config.json" "$GAME/config.json"
cp "$ROOT/LICENSE" "$ROOT/THIRD-PARTY-NOTICES.md" "$ROOT/README.md" "$GAME/"

# The bundle's shape: a folder on Windows and Linux, an app on macOS.
if [[ "$RID" == osx-* ]]; then
  APP="$OUT/OpenPax.app"
  mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
  rsync -a "$GAME/" "$APP/Contents/MacOS/"
  cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>OpenPax</string>
  <key>CFBundleDisplayName</key><string>OpenPax</string>
  <key>CFBundleIdentifier</key><string>io.github.jlac1024.openpax</string>
  <key>CFBundleExecutable</key><string>openpax</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleVersion</key><string>$VERSION</string>
  <key>LSMinimumSystemVersion</key><string>13.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST
  rsync -a "$WORK/lobby-server/" "$OUT/lobby-server/"
  cp "$ROOT/tools/prepare_mac.sh" "$OUT/prepare_mac.sh"
  cp "$ROOT/LICENSE" "$ROOT/THIRD-PARTY-NOTICES.md" "$ROOT/README.md" "$OUT/"

  # Signed for running here only (not by a known developer): on a Mac, now;
  # a downloaded copy still needs prepare_mac.sh to clear the quarantine.
  if command -v codesign > /dev/null; then
    for f in "$APP"/Contents/MacOS/*.dylib "$APP/Contents/MacOS/tools/ffmpeg" "$APP/Contents/MacOS/openpax" "$OUT"/lobby-server/*.dylib "$OUT/lobby-server/openpax-lobby"; do
      [[ -e "$f" ]] && codesign --force --sign - "$f"
    done
    codesign --force --sign - "$APP"
  fi
else
  rsync -a "$GAME/" "$OUT/"
  rsync -a "$WORK/lobby-server/" "$OUT/lobby-server/"
fi

rm -rf "$WORK"
cd "$RELEASE"
if [[ "$RID" == win-* ]]; then
  rm -f "$NAME.zip"
  zip -qr "$NAME.zip" "$NAME"
  echo "$RELEASE/$NAME.zip"
else
  rm -f "$NAME.tar.gz"
  tar -czf "$NAME.tar.gz" "$NAME"
  echo "$RELEASE/$NAME.tar.gz"
fi
