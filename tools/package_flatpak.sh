#!/usr/bin/env bash
# Makes a Flatpak of the Linux release bundle (docs/platforms.md), as one
# .flatpak file a player installs with "flatpak install". Run after
# tools/package.sh linux-x64 VERSION, with flatpak installed:
#   tools/package_flatpak.sh VERSION
# Writes build/release/OpenPax-VERSION-linux-x64.flatpak.
#
# The bundle is already built and self-contained, so this copies it into the
# app instead of building in flatpak-builder: only the freedesktop runtime is
# needed, not its SDK. FLATPAK_USER_DIR (default deps/flatpak) holds the
# runtime, so nothing goes in the home folder.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="$1"
BUNDLE="$ROOT/build/release/OpenPax-$VERSION-linux-x64"
OUT="$ROOT/build/release/OpenPax-$VERSION-linux-x64.flatpak"
WORK="$ROOT/build/release/flatpak"
APP_ID=io.github.jlac1024.OpenPax
REPOSITORIES=https://jlac1024.github.io/OpenPax
SIGNING_KEY="$ROOT/tools/linux/openpax-signing-key.asc"
RUNTIME=org.freedesktop.Platform
RUNTIME_VERSION=25.08
ICON="$ROOT/src/OpenPax.Host/Icon"
export FLATPAK_USER_DIR="${FLATPAK_USER_DIR:-$ROOT/deps/flatpak}"
# flatpak's own messages go to stderr: stdout is only the .flatpak's path.
exec 3>&1 1>&2

if [[ ! -x "$BUNDLE/openpax" ]]; then
  echo "package_flatpak.sh: no bundle at $BUNDLE (run tools/package.sh linux-x64 $VERSION first)" >&2
  exit 2
fi

flatpak remote-add --user --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo
flatpak install --user --noninteractive --or-update flathub "$RUNTIME//$RUNTIME_VERSION"

rm -rf "$WORK"
mkdir -p "$WORK"
APP="$WORK/app"
# The runtime stands in for the SDK too: nothing is compiled in here.
flatpak build-init "$APP" "$APP_ID" "$RUNTIME" "$RUNTIME" "$RUNTIME_VERSION"

FILES="$APP/files"
mkdir -p "$FILES/lib" "$FILES/bin" "$FILES/share/applications" "$FILES/share/metainfo"
cp -a "$BUNDLE" "$FILES/lib/openpax"
# The app is read-only, so the game points to the new .flatpak instead of updating itself.
echo Flatpak > "$FILES/lib/openpax/.openpax-packaged"
printf '#!/bin/sh\nexec /app/lib/openpax/openpax "$@"\n' > "$FILES/bin/openpax"
printf '#!/bin/sh\nexec /app/lib/openpax/lobby-server/openpax-lobby "$@"\n' > "$FILES/bin/openpax-lobby"
chmod 755 "$FILES/bin/openpax" "$FILES/bin/openpax-lobby"
cp "$ROOT/tools/linux/$APP_ID.desktop" "$FILES/share/applications/"
sed -e "s/@VERSION@/$VERSION/" -e "s/@DATE@/$(date -u +%F)/" \
  "$ROOT/tools/linux/$APP_ID.metainfo.xml" > "$FILES/share/metainfo/$APP_ID.metainfo.xml"
for png in "$ICON"/hicolor/*.png; do
  size="$(basename "$png" .png)"
  mkdir -p "$FILES/share/icons/hicolor/${size}x${size}/apps"
  cp "$png" "$FILES/share/icons/hicolor/${size}x${size}/apps/$APP_ID.png"
done
# PNGs only: flatpak's own icon check refuses the SVG on Ubuntu's runners,
# even with gdk-pixbuf's SVG loader installed.

chmod -R u+rwX,go+rX,go-w "$FILES"

# What the game may reach: its window (Wayland, or X11 where there is no
# Wayland), the GPU, sound, the network (updates, the lobby, games), and, read
# only, the Downloads folder and GOG's usual game folder, where a player's
# installer or game is likely to be. Anywhere else, the setup's file picker
# hands the game the one folder or file chosen, through the desktop portal.
flatpak build-finish "$APP" --command=openpax \
  --socket=wayland --socket=fallback-x11 --share=ipc --device=dri \
  --socket=pulseaudio --share=network \
  --filesystem=xdg-download:ro "--filesystem=~/GOG Games:ro"

flatpak build-export "$WORK/repo" "$APP" stable
rm -f "$OUT"
# With the signing key, the bundle names the Flatpak repository on the
# project's GitHub Pages and trusts its key: installing it once is enough for
# the system's updater (and flatpak update) to bring every later version.
ORIGIN=()
if [[ -f "$SIGNING_KEY" ]]; then
  gpg --dearmor < "$SIGNING_KEY" > "$WORK/signing-key.gpg"
  ORIGIN=(--repo-url="$REPOSITORIES/flatpak/repo" --gpg-keys="$WORK/signing-key.gpg")
fi

flatpak build-bundle --runtime-repo=https://dl.flathub.org/repo/flathub.flatpakrepo "${ORIGIN[@]}" \
  "$WORK/repo" "$OUT" "$APP_ID" stable
rm -rf "$WORK"
echo "$OUT" >&3
