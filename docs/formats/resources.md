# Resource container

The game's data and image files share one container, modeled on Mac
resource files: numbered resources grouped by a four-character type.
Heliotrope's earlier games were Mac titles, and the type codes are Mac
style four-character codes.

## Layout

| Offset | Size | Meaning |
|---|---|---|
| 0 | 2 | `II` (Intel byte order, as in TIFF) |
| 2 | 4 | Number of types T (1 in every GOG file) |
| 6 | | T type blocks, then resource data |

Each type block:

| Size | Meaning |
|---|---|
| 4 | Type code: a 32-bit integer from an MSVC multi-char constant (`'RACE'` is 0x52414345), so the bytes on disk read reversed (`ECAR`) |
| 4 | Resource count N |
| 8 x N | Directory: int32 id, int32 file offset |

The original reader is (header) (type table),
(type block) and (directory entry).

- Ids within a type must not decrease. Lookup by id is a binary search
  (a lower bound) that returns the first matching entry.
  `Image/Main.img` has two different pictures with id 27000 (640x480 and
  640x460); lookup by id always gives the first. Resources can also be
  fetched by index.
- There is no stored size. The original never needs one: it seeks to the
  offset and the resource reads itself (see "Resource bodies"). In the
  GOG files resources are packed back to back from the end of the
  directory to the end of the file (checked with
  `tools/survey_resources.py`), so the port derives a size from the next
  offset.
- The original caches loaded resources and reference counts them.

Files that start with `II` but are not containers: `PrefData` and the
`pft*` fonts. Their second field is not a plausible type count.

## Resource bodies

Every resource body starts with an int32 class id. The loader
looks the id up in a class registry (filled),
creates an object of that class and lets it read the rest of the stream.
Class ids found in the data:

| Class | Factory | Used by |
|---|---|---|
| 1000 | | `CPLT` palettes |
| 1003 | | `PPIC` pictures |
| 1005 | | `STR#` string lists |
| 6001 to 6010 | and others | `TECH`, ten kinds of technology |
| 7105 | | `RACE` |
| 7108 | | `HULL` |
| 7110 | not registered | `SHIP` (see below) |
| 8000 | | `VIEW`, serialized window layouts |

The registry holds about 68 classes in all; the rest are game objects and
windows, which save files will use.

`Data/ShipData` is never opened by the executable (no code names it), and
its class 7110 is not registered, so it is a leftover. GOG ships its own
copy anyway.

## Types in the GOG install

| File | Type | Count | Ids | Entry size |
|---|---|---|---|---|
| `Data/GameData` | `RACE` | 8 | 1..8 | 116..182 |
| `Data/HullData` | `HULL` | 6 | 2000..2005 | 16 |
| `Data/PalData` | `CPLT` | 16 | 1000..1015 | 772 |
| `Data/ShipData` | `SHIP` | 3 | 1000..1002 | 150..199 |
| `Data/StrData` | `STR#` | 356 | 70..100239 | 17..5714 |
| `Data/TechData` | `TECH` | 239 | 1..239 | 68..144 |
| `Data/ViewData` | `VIEW` | 32 | 10000..47000 | 218..5384 |
| `Image/Advisors.img` | `PPIC` | 378 | 1000..18109 | |
| `Image/Combat.img` | `PPIC` | 13867 | 1001..119991 | |
| `Image/Empire.img` | `PPIC` | 318 | 1000..11008 | |
| `Image/Infra.img` | `PPIC` | 557 | 10..1109 | |
| `Image/Main.img` | `PPIC` | 2970 | 50..46000 | |
| `Image/Planets.img` | `PPIC` | 5040 | 300001..401720 | |
| `Image/Ships.img` | `PPIC` | 3168 | 10..16532 | |
| `Image/Surface.img` | `PPIC` | 2545 | 950..30379 | |
| `Image/Tech.img` | `PPIC` | 9304 | 999..34785 | |
| `Image/Universe.img` | `PPIC` | 1029 | 1..22124 | |

Record layouts for each class come from that class's read method.
