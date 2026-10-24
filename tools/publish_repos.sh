#!/usr/bin/env bash
# Makes the package repositories the project's GitHub Pages serve
# (docs/platforms.md), from a published release's .flatpak and .deb:
#   tools/publish_repos.sh VERSION RELEASE_FOLDER SITE_FOLDER
# SITE_FOLDER gets:
#   flatpak/repo                  a signed Flatpak (OSTree) repository with the release
#   flatpak/openpax.flatpakrepo   the file that adds it as a remote by hand
#   apt/                          a signed apt repository ("stable main") with the release
#   openpax-signing-key.asc       the key both are signed with
#   index.html                    how to use them
# The signing key must be in gpg's keyring (the workflow imports it from the
# OPENPAX_SIGNING_KEY secret). Each run holds only the release given: players
# update to it from whatever they have.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="$1"
RELEASE="$(cd "$2" && pwd)"
SITE="$3"
APP_ID=io.github.jlac1024.OpenPax
URL="${OPENPAX_REPOSITORIES:-https://jlac1024.github.io/OpenPax}"
KEY_FILE="$ROOT/tools/linux/openpax-signing-key.asc"
KEY_ID="$(gpg --with-colons --import-options show-only --import < "$KEY_FILE" | awk -F: '$1 == "fpr" { print $10; exit }')"

rm -rf "$SITE"
mkdir -p "$SITE/flatpak" "$SITE/apt"
cp "$KEY_FILE" "$SITE/openpax-signing-key.asc"

# Flatpak: the release's bundle imported into a new repository (an OSTree
# archive, as Flatpak repositories are served), signed.
ostree init --mode=archive-z2 --repo="$SITE/flatpak/repo"
flatpak build-import-bundle --gpg-sign="$KEY_ID" "$SITE/flatpak/repo" "$RELEASE/OpenPax-$VERSION-linux-x64.flatpak"
flatpak build-update-repo --gpg-sign="$KEY_ID" --title="OpenPax" --default-branch=stable "$SITE/flatpak/repo"
cat > "$SITE/flatpak/openpax.flatpakrepo" <<REPO
[Flatpak Repo]
Title=OpenPax
Url=$URL/flatpak/repo/
Homepage=https://github.com/jlac1024/OpenPax
Comment=OpenPax, a new engine for Pax Imperia: Eminent Domain
GPGKey=$(gpg --dearmor < "$KEY_FILE" | base64 -w0)
REPO

# apt: the .deb in a pool, its index, and a Release signed both ways (InRelease and Release.gpg).
APT="$SITE/apt"
mkdir -p "$APT/pool/main" "$APT/dists/stable/main/binary-amd64"
cp "$RELEASE/openpax_${VERSION}_amd64.deb" "$APT/pool/main/"
(cd "$APT" && dpkg-scanpackages --multiversion pool /dev/null > dists/stable/main/binary-amd64/Packages)
gzip -9 -k "$APT/dists/stable/main/binary-amd64/Packages"
python3 -I "$ROOT/tools/apt_release.py" "$APT/dists/stable" > "$APT/dists/stable/Release"
gpg --batch --yes --local-user "$KEY_ID" --clearsign --output "$APT/dists/stable/InRelease" "$APT/dists/stable/Release"
gpg --batch --yes --local-user "$KEY_ID" --armor --detach-sign --output "$APT/dists/stable/Release.gpg" "$APT/dists/stable/Release"

sed -e "s|@VERSION@|$VERSION|g" -e "s|@URL@|$URL|g" "$ROOT/tools/linux/repositories.html" > "$SITE/index.html"
# GitHub Pages leaves out files and folders starting with _ or . unless told not to.
touch "$SITE/.nojekyll"
echo "$SITE"
