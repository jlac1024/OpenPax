"""The Release file of an apt repository's suite (tools/publish_repos.sh):
what the suite holds and the size and hashes of each index file under it,
as apt checks them before it trusts the indexes.

    python3 -I tools/apt_release.py DISTS/SUITE > DISTS/SUITE/Release
"""

import email.utils
import hashlib
import os
import sys


def main(suite):
    files = []
    for folder, _, names in os.walk(suite):
        for name in sorted(names):
            path = os.path.join(folder, name)
            relative = os.path.relpath(path, suite).replace(os.sep, "/")
            if relative in ("Release", "InRelease", "Release.gpg"):
                continue
            data = open(path, "rb").read()
            files.append((relative, len(data), hashlib.md5(data).hexdigest(), hashlib.sha256(data).hexdigest()))
    files.sort()

    lines = [
        "Origin: OpenPax",
        "Label: OpenPax",
        "Suite: stable",
        "Codename: stable",
        "Architectures: amd64",
        "Components: main",
        "Description: OpenPax, a new engine for Pax Imperia: Eminent Domain",
        "Date: " + email.utils.formatdate(usegmt=True),
        "MD5Sum:",
    ]
    lines += [f" {md5} {size} {name}" for name, size, md5, _ in files]
    lines.append("SHA256:")
    lines += [f" {sha} {size} {name}" for name, size, _, sha in files]
    sys.stdout.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    main(sys.argv[1])
