#!/usr/bin/env bash
# Renders OpenPax's icon (src/OpenPax.Host/Icon/openpax.svg) to what each
# platform takes, beside it: openpax.ico (Windows: the program, the
# installer, shortcuts), openpax.icns (the Mac app), openpax-128.rgba (the
# window's icon, raw pixels the program carries), openpax-256.png (the
# README), hicolor/SIZE.png (Linux menus), social-preview.png (GitHub's link preview). The results are committed, so building needs none of this; run it
# after changing the SVG. Needs rsvg-convert (librsvg2-bin) and ImageMagick.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ICON="$ROOT/src/OpenPax.Host/Icon"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

for size in 16 24 32 48 64 128 256 512 1024; do
  rsvg-convert -w "$size" -h "$size" "$ICON/openpax.svg" -o "$WORK/$size.png"
done

convert "$WORK"/{16,24,32,48,64,128,256}.png "$ICON/openpax.ico"
convert "$WORK/128.png" -depth 8 "rgba:$ICON/openpax-128.rgba"
cp "$WORK/256.png" "$ICON/openpax-256.png"

# The Linux menus' sizes (hicolor/SIZE.png, for the .deb and the Flatpak):
# a menu takes the size it draws at, and not every one scales another.
mkdir -p "$ICON/hicolor"
for size in 16 24 32 48 64 128 256 512; do
  cp "$WORK/$size.png" "$ICON/hicolor/$size.png"
done

# An .icns is a header and one entry per size, each a PNG.
python3 -I - "$WORK" "$ICON/openpax.icns" <<'PY'
import struct, sys
work, out = sys.argv[1], sys.argv[2]
entries = b""
for kind, size in ((b"icp4", 16), (b"icp5", 32), (b"icp6", 64), (b"ic07", 128), (b"ic08", 256), (b"ic09", 512), (b"ic10", 1024)):
    data = open(f"{work}/{size}.png", "rb").read()
    entries += kind + struct.pack(">I", len(data) + 8) + data
open(out, "wb").write(b"icns" + struct.pack(">I", len(entries) + 8) + entries)
PY

# The installer's own pictures (tools/installer/openpax.iss), as Inno Setup
# takes them: 32-bit BMPs with alpha, in sizes for 100% to 250% display
# scaling. The small one sits in the corner of the inner pages; the large one
# fills the side of the Welcome and Finish pages, the icon above the name.
INSTALLER="$ICON/installer"
mkdir -p "$INSTALLER"
for size in 55 69 83 110 138; do
  rsvg-convert -w "$size" -h "$size" "$ICON/openpax.svg" -o "$WORK/small-$size.png"
  convert "$WORK/small-$size.png" -define bmp:format=bmp4 "BMP:$INSTALLER/small-$size.bmp"
done
if [[ -f "$ROOT/deps/fonts/titillium-web/TitilliumWeb-Bold.ttf" ]]; then
  for scale in 100 150 200; do
    w=$((164 * scale / 100)); h=$((314 * scale / 100)); i=$((140 * scale / 100))
    rsvg-convert -w "$i" -h "$i" "$ICON/openpax.svg" -o "$WORK/large-icon.png"
    convert -size "${w}x${h}" radial-gradient:'#16244a'-'#03060f' \
      "$WORK/large-icon.png" -gravity north -geometry "+0+$((60 * scale / 100))" -composite \
      -font "$ROOT/deps/fonts/titillium-web/TitilliumWeb-Bold.ttf" -fill '#ffd25a' -pointsize "$((34 * scale / 100))" \
      -gravity north -annotate "+0+$((215 * scale / 100))" 'OpenPax' \
      -alpha on -depth 8 -define bmp:format=bmp4 "BMP:$INSTALLER/large-$scale.bmp"
  done
fi

# GitHub's social preview (the repository's Settings, Social preview): 1280x640,
# the icon beside the name, in the modern font the game ships.
FONTS="$ROOT/deps/fonts/titillium-web"
if [[ -f "$FONTS/TitilliumWeb-Bold.ttf" ]]; then
  rsvg-convert -w 420 -h 420 "$ICON/openpax.svg" -o "$WORK/banner-icon.png"
  convert -size 1280x640 radial-gradient:'#16244a'-'#03060f' "$WORK/banner-icon.png" -geometry +90+110 -composite \
    -font "$FONTS/TitilliumWeb-Bold.ttf" -fill '#ffd25a' -pointsize 120 -annotate +560+300 'OpenPax' \
    -font "$FONTS/TitilliumWeb-Regular.ttf" -fill '#e6ecff' -pointsize 34 -annotate +566+370 'An open-source engine for' \
    -annotate +566+415 'Pax Imperia: Eminent Domain' \
    -fill '#9fb2e0' -pointsize 28 -annotate +566+480 'Windows  ·  macOS  ·  Linux' -depth 8 "$ICON/social-preview.png"
fi

ls -l "$ICON"
