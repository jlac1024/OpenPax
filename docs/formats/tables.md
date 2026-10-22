# Game tables

Record layouts of the game's data resources. Every body starts with its
int32 class id (resources.md); the fields below follow it. Little endian.

## HULL (class 7108, Data/HullData)

Read. Six hulls, ids 2000 to 2005. A ship design stores a
hull type 0 to 5 and looks up resource 2000 + type.

| Field | Type | Meaning |
|---|---|---|
| Structure | int32 | Times a per-design factor gives the design's structure points |
| Capacity | int32 | Space for components; a design fits while its used space is at most this |
| Cost | int32 | Times the same factor, plus the components' costs, gives the design's cost |

| Id | Structure | Capacity | Cost |
|---|---|---|---|
| 2000 | 100 | 500 | 750 |
| 2001 | 400 | 1000 | 1500 |
| 2002 | 200 | 2000 | 1500 |
| 2003 | 1000 | 2000 | 3000 |
| 2004 | 2000 | 4000 | 6000 |
| 2005 | 1000 | 4000 | 4000 |

The factor comes from a table of doubles, indexed by a field
of the design, and may be scaled by a technology. That belongs to the
simulation and is ported in Phase 3.

Note on offsets in the original: the resource cache returns the object's
base, where a reference count sits at +4 and the fields start at +0xC, so
code reads these three at +0xC, +0x10 and +0x14.

## RACE (class 7105, Data/GameData) and Data/SpecData

A species template. Read; written. Eight
built-in species, ids 1 to 8. Custom species from the designer are kept
in `Data/SpecData` (loader): an `II` mark, then the same
records back to back without class ids, until the end of the file. The
GOG install ships it empty.

| Field | Type | Allowed | Meaning |
|---|---|---|---|
| Race id | int32 | 0 to 999 | Equals the resource id for built-in species |
| Name | string | | uint16 length, then the bytes |
| Description | string | | Same |
| Picture | int32 | 0 to 17 | Advisor portrait set |
| Preferred temperature | int32 | 160, 130, 100, 70, 40, 10 | Very hot to very cold |
| Temperature tolerance | int32 | 20, 40, 60 | Low, normal, high |
| Atmospheres | 4 x byte | 0 or 1 | Carbon dioxide, methane-ammonia, oxygen-nitrogen, sulfur dioxide |
| Physical traits | 5 x byte | 0 or 1 | Anaerobic, subterranean, psychic, predator, aquatic |
| Cultural traits | 7 x byte | 0 or 1 | Warrior, scientist, politician, spy, builder, nomad, merchant |
| Rate modifiers | 5 x int8 | -5 to 5 | Research, reproduction, construction, espionage, finance |
| Research levels | 5 x int8 | -1 to 1 | Weapons, shielding, ship, colonial, space (low is dearer, high cheaper) |

Each field goes through a setter that ignores values outside the allowed
range, so the field keeps its default.
Defaults: id 0, name "Unnamed" (string list 100, entry 3),
description "No Description" (list 85, entry 0), picture 0, temperature
160, tolerance 60, carbon dioxide only, everything else 0.

After reading, the original adds up the species' points and,
when they exceed 10, resets the whole species to the defaults, name and id
included. Points: when not anaerobic, the number of atmospheres minus one
(so -1 with none); anaerobic 5, subterranean 5, psychic 3, predator 2,
aquatic 2; 2 per cultural trait; plus the sum of all ten rate and
research values. All eight built-in species total exactly 10.

| Id | Name | Temperature | Atmospheres | Notable traits |
|---|---|---|---|---|
| 1 | Humans | 100 | O2/N2 | all rates +2 |
| 2 | Kar'Tsoran | 40 | CO2 | spy, merchant |
| 3 | Gorak | 160 | CH4/NH3, SO2 | predator, aquatic, warrior |
| 4 | Yssla | 100 | O2/N2, SO2 | psychic, politician |
| 5 | Schreki | 160 | none | anaerobic, subterranean |
| 6 | Tekari | 10 | CO2, O2/N2 | scientist |
| 7 | D'naren | 100 | CH4/NH3, O2/N2 | builder |
| 8 | Kybus | 70 | all four | warrior, scientist |

The original's species selection window keeps loaded custom species in a
fixed array of 100 with no bounds check, so more than 100 records in
`SpecData` would overrun it. The port should cap the list at 100 when the
species screen is ported, and note it as a kept limit.

## TECH (classes 6001 to 6010, Data/TechData)

239 technologies, ids 1 to 239. No strings in the records: a tech's name
and description are entries 0 and 1 of string list 100000 + id. Every
record has the same base, then fields that depend on the class. All
records of a class have the same size, and every GOG record is consumed
exactly.

Base (fifteen int32): id; picture (Tech.img; 25000 for every
theory); research group; research cost (scaled 75 to 125 percent by the
species' research level); cost (scaled by tech level and summed into a
design's cost, which is also a ship's upkeep; see `sim/ships.md`); build cost; kind (see below); four prerequisite ids (0 for
none); hit points; size (space used in a design, shrinks with tech level);
two fields that are always 0.

Kinds: -2 planetary improvement, -1 theory, 1 repair, 2 colonizer,
3 minesweeper, 5 shipyard, 6 academy, 7 missile base, 8 battle station,
9 fighter base, 10 minefield, 11 star drive, 12 tactical drive, 13 sensor,
14 ECM, 15 energy weapon, 16 projectile, 17 missile, 18 planet bomb,
19 area weapon, 20 screen, 21 shield, 22 field, 23 fighter, 24 armor.

| Class | Count | Size | What | Fields after the base |
|---|---|---|---|---|
| 6001 | 14 | 69 | Star drive | int32 rating (wormhole jumps away from supply), byte largest hull class |
| 6002 | 19 | 70 | Tactical drive | int32 combat speed, int16 defense bonus |
| 6003 | 22 | 70 | Targeting sensor | int16 range, int16 accuracy bonus, int16 targets tracked |
| 6004 | 5 | 68 | ECM | int32 strength |
| 6005 | 45 | 97 | Ship weapon | int32 effect, int32 secondary, 4 x int16 damage by range band, byte accuracy bonus (percent), int32 reload, int32 area radius, int32, 4 x byte (the third is tracking, 255 for none; the last is the weapon type: 0 normal, 1 planet bomb, 2 area) |
| 6006 | 24 | 81 | Screen, shield, field | int32 type, int32 capacity, int32 percent reduction, byte field flag, int32 regeneration |
| 6007 | 18 | 78 | Fighter | seven int16: speed, two weapon tech ids, defense, hit points, two more |
| 6008 | 5 | 73 | Armor | byte largest hull class, double structure multiplier (1.0 to 1.8) |
| 6009 | 37 | 72 | Installation or ship special | int32 level, int32 strength |
| 6010 | 50 | 144 | Planetary improvement | five pairs of doubles (per population, flat) for credits, research, espionage, construction, population |

Weapon range: the last non-zero damage band gives the maximum range of
300, 500, 750 or 1500. The Research window's stats panel
(`ui/research.md`) names most fields; a few weapon and fighter fields are
still marked Unknown in the C# types.

In memory the record sits at the tech object's +8, so a field's object
offset is its body offset plus 8, except where the layouts part: the
weapon's four int32 after its accuracy byte are padded to object +0x5C
(body 0x51), the shield keeps regeneration before the field flag (object
+0x54 and +0x58, body 0x4D and 0x4C), and armor's double is at object
+0x50 (body 0x41).

## PrefData (Data/PrefData)

Not a resource container. `II`, int32 entry count, then per entry: uint16
name length, name, int32 type, value. Types: 2 string (uint16 length +
bytes), 3 int32, 8 one-byte flag. The GOG copy has 17 entries:
`RedbookVolume`, `MidiVolume`, `VoiceVolume`, `SoundFXVolume`, `RBATrack`,
three numbered entries (`10,002` to `10,004`), the player name, `FlagID`,
`FleetID`, `GameSpeed`, `PlaySurfaceAnim`, `PlayMovies`, `AutoSaveGame`,
`CombatKeyScroll`, `CombatScroll`. The port reads it only for defaults.
