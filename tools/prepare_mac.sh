#!/usr/bin/env bash
# Run once on the Mac after unpacking (docs/platforms.md): the game is not
# from the App Store and not signed by a developer, so macOS would refuse it.
# This clears the download quarantine and signs it for this Mac alone.
#   ./prepare_mac.sh
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
xattr -dr com.apple.quarantine "$HERE" 2> /dev/null || true

# Each library and program, which is what Apple Silicon needs to run them.
# Not the app as a whole: codesign refuses a bundle whose program folder
# also holds .NET's .dll files.
for f in "$HERE"/OpenPax.app/Contents/MacOS/*.dylib "$HERE"/OpenPax.app/Contents/MacOS/openpax \
  "$HERE"/OpenPax.app/Contents/MacOS/tools/ffmpeg \
  "$HERE"/lobby-server/*.dylib "$HERE"/lobby-server/openpax-lobby; do
  if [[ -e "$f" ]]; then
    codesign --force --sign - "$f"
  fi
done

echo "Ready: open OpenPax.app"
