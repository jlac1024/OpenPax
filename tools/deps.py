#!/usr/bin/env python3
"""Fetch, verify and unpack the pinned dependencies listed in tools/deps.toml.

Usage:
  deps.py get <name> <key>   print one value from deps.toml
  deps.py fetch <name>...    download into deps/, check sha256, unpack
  deps.py path <name>        print the unpacked folder for <name>

Archives are kept in deps/archives/ so a pin change can be checked against
what was downloaded before. Each unpacked folder gets a .pin file holding the
sha256 it came from; a mismatch means the folder is stale and gets replaced.
"""

import hashlib
import shutil
import sys
import tarfile
import tomllib
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DEPS = ROOT / "deps"
ARCHIVES = DEPS / "archives"


def load() -> dict:
    with open(ROOT / "tools" / "deps.toml", "rb") as f:
        return tomllib.load(f)


def sha256_of(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def download(url: str, dest: Path) -> None:
    tmp = dest.with_suffix(dest.suffix + ".part")
    print(f"downloading {url}", flush=True)
    with urllib.request.urlopen(url) as r, open(tmp, "wb") as out:
        shutil.copyfileobj(r, out, 1 << 20)
    tmp.rename(dest)


def unpack(archive: Path, into: Path) -> None:
    # Archives come from pinned hashes, but still refuse members that would
    # land outside deps/.
    if archive.name.endswith(".zip"):
        with zipfile.ZipFile(archive) as z:
            for m in z.namelist():
                if not (into / m).resolve().is_relative_to(into.resolve()):
                    raise SystemExit(f"refusing unsafe path in {archive.name}: {m}")
            z.extractall(into)
            # zipfile drops the executable bit; unpacked tools' scripts need it.
            for info in z.infolist():
                mode = (info.external_attr >> 16) & 0o777
                if mode:
                    (into / info.filename).chmod(mode)
    else:
        with tarfile.open(archive) as t:
            t.extractall(into, filter="data")


def fetch_files(name: str, d: dict) -> None:
    """A dependency of loose files (base url plus file names), each checked by its own sha256."""
    target = DEPS / d["unpack"]
    target.mkdir(parents=True, exist_ok=True)
    for file, sha in d["files"].items():
        dest = target / file
        if dest.exists() and sha256_of(dest) == sha:
            continue
        download(d["base"] + file, dest)
        actual = sha256_of(dest)
        if actual != sha:
            dest.unlink()
            raise SystemExit(f"{name}: {file} sha256 mismatch, expected {sha}, got {actual}")
    print(f"{name} {d['version']}: in deps/{target.relative_to(DEPS)}")


def fetch(name: str, deps: dict) -> None:
    d = deps[name]
    if "files" in d:
        fetch_files(name, d)
        return
    if "url" not in d:
        print(f"{name}: nothing to fetch ({d.get('source', 'no url')})")
        return
    target = DEPS / d["unpack"]
    pin = target / ".pin"
    if pin.exists() and pin.read_text().strip() == d["sha256"]:
        print(f"{name} {d['version']}: up to date")
        return

    ARCHIVES.mkdir(parents=True, exist_ok=True)
    archive = ARCHIVES / d["url"].rsplit("/", 1)[1].replace("%2B", "+")
    if not archive.exists() or sha256_of(archive) != d["sha256"]:
        download(d["url"], archive)
    actual = sha256_of(archive)
    if actual != d["sha256"]:
        archive.unlink()
        raise SystemExit(f"{name}: sha256 mismatch, expected {d['sha256']}, got {actual}")

    if target.exists():
        shutil.rmtree(target)
    unpack(archive, DEPS)
    if not target.is_dir():
        raise SystemExit(f"{name}: archive did not unpack to {target.name}, fix 'unpack' in deps.toml")
    pin.write_text(d["sha256"] + "\n")
    print(f"{name} {d['version']}: unpacked to deps/{target.name}")


def main(argv: list[str]) -> None:
    deps = load()
    if len(argv) >= 3 and argv[0] == "get":
        print(deps[argv[1]][argv[2]])
    elif len(argv) == 2 and argv[0] == "path":
        print(DEPS / deps[argv[1]]["unpack"])
    elif len(argv) >= 2 and argv[0] == "fetch":
        for name in argv[1:]:
            fetch(name, deps)
    else:
        raise SystemExit(__doc__)


if __name__ == "__main__":
    main(sys.argv[1:])
