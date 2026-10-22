#!/usr/bin/env bash
# Run once on the Mac after unpacking (docs/platforms.md): the game is not
# from the App Store and not signed by a developer, so macOS would refuse it.
# This clears the download quarantine, so macOS lets it run.
#   ./prepare_mac.sh
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
xattr -dr com.apple.quarantine "$HERE" 2> /dev/null || true

# The programs and libraries came signed (ad hoc) from the build; with the
# quarantine gone, macOS runs them.
echo "Ready: open OpenPax.app"
