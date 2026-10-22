#!/usr/bin/env python3
"""Survey the "II" resource containers in a game folder.

Usage:
  survey_resources.py <game folder>

Prints, per file: resource type, count, id range, size range, and whether
the offsets are increasing and end exactly at the end of the file. Used to
confirm the container layout described in docs/formats/resources.md before
the C# reader is written. Reads only.
"""

import struct
import sys
from pathlib import Path


def parse(data: bytes):
    if data[:2] != b"II":
        return None
    version, = struct.unpack_from("<I", data, 2)
    if version != 1:
        return None
    rtype = data[6:10][::-1].decode("latin-1")
    count, = struct.unpack_from("<I", data, 10)
    entries = [struct.unpack_from("<II", data, 14 + 8 * i) for i in range(count)]
    return rtype, entries


def main(argv: list[str]) -> None:
    if len(argv) != 1:
        raise SystemExit(__doc__)
    root = Path(argv[0])
    for p in sorted([*root.glob("Data/*"), *root.glob("Image/*")]):
        data = p.read_bytes()
        r = parse(data)
        rel = p.relative_to(root).as_posix()
        if r is None:
            print(f"{rel:22} not a resource container ({data[:6].hex()})")
            continue
        rtype, entries = r
        header_end = 14 + 8 * len(entries)
        offs = [o for _, o in entries]
        ends = offs[1:] + [len(data)]
        sizes = [e - o for o, e in zip(offs, ends)]
        ids = [i for i, _ in entries]
        ok = offs == sorted(offs) and (not offs or offs[0] == header_end)
        print(f"{rel:22} {rtype} n={len(entries):5} ids {min(ids)}..{max(ids)} "
              f"sizes {min(sizes)}..{max(sizes)} contiguous={ok}")


if __name__ == "__main__":
    main(sys.argv[1:])
