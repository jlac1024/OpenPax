# Data formats

Every file type in the GOG install, what it is, and how far it is
understood. Byte order is little endian throughout. "Identified" means we
know what the file is and its outer layout; "documented" means a reader
can be written from the notes alone.

| Files | Format | Status | Notes |
|---|---|---|---|
| `Data/GameData`, `HullData`, `StrData`, `TechData`, `ViewData`, `PalData` | Resource container | documented (container), identified (records) | See [resources.md](resources.md). Types `RACE`, `HULL`, `STR#`, `TECH`, `VIEW`, `CPLT`. Every resource body starts with a class id. |
| `Data/ShipData` | Resource container, type `SHIP` | identified | Never opened by the game; a leftover (resources.md). |
| `Image/*.img` | Resource container of `PPIC` | documented | Run-length coded 8-bit images, see below. Converted to indexed PNG. |
| `Data/PrefData` | Key/value preferences | documented | Typed name/value pairs, see [tables.md](tables.md). Read only to learn the defaults; the port keeps settings in TOML. |
| `Data/SpecData` | Saved species designs | documented | Empty (0 bytes) in a fresh install. `II`, then species records until the end; see [tables.md](tables.md). |
| `Data/pft8` to `pft18` | Bitmap fonts | documented | Regular and bold glyph sets, 1-bit glyphs, Windows-1252 layout. See [fonts.md](fonts.md). Converted to PNG glyph sheets. |
| `Sound/**/*.wav` | RIFF WAVE, PCM | documented | 204 files. 16-bit mono or stereo at 22,050 Hz for most; a few 8-bit, one at 44,100 Hz, three at 22,254 Hz (Mac rate). |
| `MUSIC/Track02..14.ogg` | Ogg Vorbis | documented (standard) | GOG's rip of the CD audio tracks. Decoded by our own Ogg and Vorbis code (floor 1, residues 0 to 2), checked against ffmpeg. |
| `_Movie/*.smk` | Smacker 2 (`SMK2`) | identified | What the game plays. 640x288 except `Mix_LOGOS.smk` at 640x480. Converted to MP4/H.264 on import (see `docs/decisions.md`). |
| `_MOVIES_AVI/*.avi` | AVI, Microsoft Video 1 (`MSVC`) + PCM | identified | Never opened by the game. Kept as a second source to compare quality when choosing which one to convert. |
| `Saved Games/` | Save files | not present | Empty in a fresh install. Format to be taken from the code (deferred to Phase 3). |
| `setup_pax_imperia_2.0.0.12.exe` | GOG offline installer: Inno Setup 5.5.0 (Unicode), LZMA2 | documented | Read directly by the first run; see [inno-setup.md](inno-setup.md) and [lzma.md](lzma.md). |

## Images (`PPIC`, class 1003)

Read, drawn. Little endian int32 fields:

| Offset | Meaning |
|---|---|
| 0 | Class id 1003 |
| 4 | Left |
| 8 | Top |
| 12 | Right |
| 16 | Bottom |
| 20 | Size of the pixel data in bytes |
| 24 | One int32 per row (bottom - top rows): offset of that row in the pixel data |
| 24 + 4 x rows | Pixel data |

Left and top are 0 in every GOG picture, so width = right and
height = bottom.

Each row is run-length coded, PCX style, and runs until the row's width
is filled:

- a byte below 0xC0 is one pixel of that palette index;
- a byte of 0xC0 or more is a run of (byte - 0xC0) pixels of the index in
  the next byte.

Index 1 is transparent: the original's blitters skip it. No GOG picture
has a zero-length run (0xC0) or a run past the end of its row; the
original would mishandle both. Pictures carry no palette; see
[converted.md](converted.md).

## Palettes (`CPLT`, class 1000)

Read. Class id 1000, then 256 colors of three bytes each:
red, green, blue. When building the DirectDraw palette the original sets
the "reserved" flag on entries 1 to 254 so they can be animated.
