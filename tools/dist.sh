#!/usr/bin/env bash
# Makes the Windows and macOS builds (docs/platforms.md), after build.sh has
# built and assembled deploy/ for Linux. Called by ./build.sh --dist.
#   build/dist/OpenPax-win-x64/       the game for Windows x64, and a .zip of it
#   build/dist/OpenPax-osx-arm64/     OpenPax.app for Apple Silicon, and a .tar.gz of it,
#                                        once the Mac's native libraries are in build/
#   build/dist/macos-native-kit/         what the Mac needs to build them, until then
# The game's data, the sample mods, the docs and config.json come from deploy/.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEPLOY="$ROOT/deploy"
DIST="$ROOT/build/dist"
mkdir -p "$DIST"

# What every platform's game folder holds besides the program: data, mods, docs, config.
common() {
  local to="$1"
  rsync -a --delete "$DEPLOY/data/" "$to/data/"
  rsync -a --delete "$DEPLOY/mods/" "$to/mods/"
  rsync -a --delete "$DEPLOY/docs/" "$to/docs/"
  cp "$DEPLOY/config.json" "$to/config.json"
}

publish() {
  local rid="$1" out="$2"
  rm -rf "$out"
  dotnet publish "$ROOT/src/OpenPax.Host/OpenPax.Host.csproj" -c Release -r "$rid" --self-contained --nologo -o "$out/game"
  dotnet publish "$ROOT/src/OpenPax.LobbyServer/OpenPax.LobbyServer.csproj" -c Release -r "$rid" --self-contained --nologo -o "$out/lobby-server"
}

# Windows x64: native libraries cross-compiled here.
echo "== Windows x64"
"$ROOT/tools/build_native_windows.sh"
publish win-x64 "$DIST/publish-win-x64"
WIN="$DIST/OpenPax-win-x64"
rsync -a --delete --exclude /data --exclude /mods --exclude /docs --exclude /config.json --exclude /lobby-server "$DIST/publish-win-x64/game/" "$WIN/"
cp "$ROOT"/build/native-win-x64/*.dll "$WIN/"
rsync -a --delete "$DIST/publish-win-x64/lobby-server/" "$WIN/lobby-server/"
common "$WIN"
rm -f "$DIST/OpenPax-win-x64.zip"
(cd "$DIST" && zip -qr -1 OpenPax-win-x64.zip OpenPax-win-x64)
echo "Windows: $WIN (and OpenPax-win-x64.zip)"

# macOS on Apple Silicon: native libraries built on the Mac (tools/build_native_macos.sh).
echo "== macOS arm64"
MAC_NATIVE="$ROOT/build/native-osx-arm64"
if [[ -f "$ROOT/build/native-osx-arm64.tar.gz" ]]; then
  rm -rf "$MAC_NATIVE"
  tar -xzf "$ROOT/build/native-osx-arm64.tar.gz" -C "$ROOT/build"
fi

if [[ ! -d "$MAC_NATIVE" ]]; then
  KIT="$DIST/macos-native-kit"
  rm -rf "$KIT"
  mkdir -p "$KIT/tools" "$KIT/src/OpenPax.Media/Video"
  cp "$ROOT"/tools/{build_native_macos.sh,check_ffmpeg_abi.sh,ffmpeg_abi.c,deps.toml} "$KIT/tools/"
  cp "$ROOT/src/OpenPax.Media/Video/FfmpegAbi.g.cs" "$KIT/src/OpenPax.Media/Video/"
  rm -f "$DIST/macos-native-kit.zip"
  (cd "$DIST" && zip -qr macos-native-kit.zip macos-native-kit)
  echo "macOS: no native libraries yet. Take $DIST/macos-native-kit.zip to the Mac (docs/platforms.md)."
  exit 0
fi

publish osx-arm64 "$DIST/publish-osx-arm64"
MAC="$DIST/OpenPax-osx-arm64"
APP="$MAC/OpenPax.app"
rm -rf "$MAC"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
rsync -a "$DIST/publish-osx-arm64/game/" "$APP/Contents/MacOS/"
cp "$MAC_NATIVE"/*.dylib "$APP/Contents/MacOS/"
common "$APP/Contents/MacOS"
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>OpenPax</string>
  <key>CFBundleDisplayName</key><string>OpenPax</string>
  <key>CFBundleIdentifier</key><string>local.openpax</string>
  <key>CFBundleExecutable</key><string>openpax</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>0.8</string>
  <key>CFBundleVersion</key><string>0.8</string>
  <key>LSMinimumSystemVersion</key><string>13.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST
rsync -a "$DIST/publish-osx-arm64/lobby-server/" "$MAC/lobby-server/"
cp "$ROOT/tools/prepare_mac.sh" "$MAC/prepare_mac.sh"
rm -f "$DIST/OpenPax-osx-arm64.tar.gz"
tar -czf "$DIST/OpenPax-osx-arm64.tar.gz" -C "$DIST" OpenPax-osx-arm64
echo "macOS: $MAC (and OpenPax-osx-arm64.tar.gz)"
