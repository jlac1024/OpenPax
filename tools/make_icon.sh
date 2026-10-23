#!/usr/bin/env bash
# Renders OpenPax's icon (src/OpenPax.Host/Icon/openpax.svg) to what each
# platform takes, beside it: openpax.ico (Windows: the program, the
# installer, shortcuts), openpax.icns (the Mac app), openpax-128.rgba (the
# window's icon, raw pixels the program carries), openpax-256.png (the
# README). The results are committed, so building needs none of this; run it
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

ls -l "$ICON"
