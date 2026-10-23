#!/usr/bin/env bash
# Makes a .deb of the Linux release bundle (docs/platforms.md). Run after
# tools/package.sh linux-x64 VERSION:
#   tools/package_deb.sh VERSION
# Writes build/release/openpax_VERSION_amd64.deb. The bundle goes in
# /opt/openpax as it is, with the openpax and openpax-lobby commands, a menu
# entry and the icon. A mark in the bundle tells the game it came from a
# package, so it points to the new .deb instead of updating itself.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="$1"
BUNDLE="$ROOT/build/release/OpenPax-$VERSION-linux-x64"
# Debian sorts "~" before anything, as a pre-release label should: 0.2.0~beta.1 < 0.2.0.
DEB_VERSION="${VERSION/-/\~}"
OUT="$ROOT/build/release/openpax_${VERSION}_amd64.deb"
TREE="$ROOT/build/release/deb-tree"
APP_ID=io.github.jlac1024.OpenPax
ICON="$ROOT/src/OpenPax.Host/Icon"

if [[ ! -x "$BUNDLE/openpax" ]]; then
  echo "package_deb.sh: no bundle at $BUNDLE (run tools/package.sh linux-x64 $VERSION first)" >&2
  exit 2
fi

rm -rf "$TREE"
mkdir -p "$TREE/opt" "$TREE/usr/bin" "$TREE/usr/share/applications" "$TREE/usr/share/metainfo" \
  "$TREE/usr/share/icons/hicolor/256x256/apps" "$TREE/usr/share/icons/hicolor/scalable/apps" \
  "$TREE/usr/share/doc/openpax" "$TREE/DEBIAN"
cp -a "$BUNDLE" "$TREE/opt/openpax"
echo deb > "$TREE/opt/openpax/.openpax-packaged"

# Commands on the path that run the bundle's programs from their own folder.
for name in openpax openpax-lobby; do
  target=/opt/openpax/openpax
  [[ "$name" == openpax-lobby ]] && target=/opt/openpax/lobby-server/openpax-lobby
  printf '#!/bin/sh\nexec %s "$@"\n' "$target" > "$TREE/usr/bin/$name"
  chmod 755 "$TREE/usr/bin/$name"
done

cp "$ROOT/tools/linux/$APP_ID.desktop" "$TREE/usr/share/applications/"
sed -e "s/@VERSION@/$VERSION/" -e "s/@DATE@/$(date -u +%F)/" \
  "$ROOT/tools/linux/$APP_ID.metainfo.xml" > "$TREE/usr/share/metainfo/$APP_ID.metainfo.xml"
cp "$ICON/openpax-256.png" "$TREE/usr/share/icons/hicolor/256x256/apps/$APP_ID.png"
cp "$ICON/openpax.svg" "$TREE/usr/share/icons/hicolor/scalable/apps/$APP_ID.svg"
cp "$ROOT/LICENSE" "$TREE/usr/share/doc/openpax/copyright"

# .NET (self-contained, invariant globalization) needs the C and C++
# runtimes, and OpenSSL for HTTPS (update checks). SDL loads the display and
# sound libraries itself when it starts, so those are only recommended.
# Packaged files are root's, writable by no one else, whatever the umask was.
chmod -R u+rwX,go+rX,go-w "$TREE"
# Apparent size: a compressing file system would report far less in blocks.
SIZE="$(du -sk --apparent-size --exclude=DEBIAN "$TREE" | cut -f1)"
cat > "$TREE/DEBIAN/control" <<CONTROL
Package: openpax
Version: $DEB_VERSION
Architecture: amd64
Maintainer: jlac1024 <jlac1024@users.noreply.github.com>
Installed-Size: $SIZE
Depends: libc6 (>= 2.35), libgcc-s1, libstdc++6, libssl3 | libssl3t64
Recommends: libx11-6 | libwayland-client0, libgl1 | libvulkan1, libpulse0 | libpipewire-0.3-0 | libasound2 | libasound2t64
Section: games
Priority: optional
Homepage: https://github.com/jlac1024/OpenPax
Description: Pax Imperia: Eminent Domain, rebuilt to run natively
 OpenPax is a new engine for Pax Imperia: Eminent Domain, the 1997 space
 strategy game. It plays the game from your own copy (the GOG release) and
 ships none of its data: on first start it asks for your game folder or
 GOG's offline installer and imports what it needs.
CONTROL

rm -f "$OUT"
dpkg-deb --root-owner-group --build "$TREE" "$OUT" > /dev/null
rm -rf "$TREE"
echo "$OUT"
