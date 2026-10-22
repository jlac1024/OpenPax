# Content fields

Written by `tools/ApiDocs` from the game's own records each build; do not edit by hand. How content files work is in [content](content.md). Every tech has the shared fields, then those of its class. Names are in snake case; a list is written `[a, b, c]`.

## Every tech

Fields every technology has (fifteen int32, read). Name and description are not here: they are strings 0 and 1 of string list 100000 + `TechBase.TechId`.

| Field | Type | Meaning |
|---|---|---|
| `picture` | whole number | Picture id in Image/Tech.img; all theory techs use 25000. |
| `research_group` | whole number | Line of the research tree; 0 for theories. |
| `research_cost` | whole number | Scaled 75 to 125 percent by the species' research level for the field. |
| `cost` | whole number | Per-component cost, scaled by tech level and summed into a design's cost, which is also each ship's upkeep. |
| `build_cost` | whole number | Feeds design and construction cost. |
| `kind` | whole number | Slot or role; see `TechKinds`. -1 theory, -2 planetary improvement. |
| `prerequisites` | list of whole numbers | Four tech ids, 0 for none. |
| `hit_points` | whole number | Structure of the built item. |
| `size` | whole number | Space used in a design, scaled by tech level. |

## `class = "star_drive"` (6001)

Class 6001, read.

| Field | Type | Meaning |
|---|---|---|
| `rating` | whole number | 2, 4, 6 or 8: wormhole jumps a ship can make away from supply, not a speed. |
| `max_hull_class` | whole number, 0 to 255 | Largest hull the drive moves: 2, 3 or 5. |

## `class = "tactical_drive"` (6002)

Class 6002, read.

| Field | Type | Meaning |
|---|---|---|
| `combat_speed` | whole number |  |
| `defense_bonus` | whole number, -32768 to 32767 |  |

## `class = "sensor"` (6003)

Class 6003, read.

| Field | Type | Meaning |
|---|---|---|
| `range` | whole number, -32768 to 32767 |  |
| `accuracy_bonus` | whole number, -32768 to 32767 |  |
| `targets_tracked` | whole number, -32768 to 32767 |  |

## `class = "ecm"` (6004)

Class 6004, electronic countermeasures, read.

| Field | Type | Meaning |
|---|---|---|
| `strength` | whole number |  |

## `class = "weapon"` (6005)

Class 6005: beams, projectiles, missiles, planet bombs, area weapons. Read.

| Field | Type | Meaning |
|---|---|---|
| `effect` | whole number | 0 to 44: which picture set and sound its shots have in battle (the table). |
| `secondary` | whole number | Missile hit points or bomb strength; 0 for beams. Meaning uncertain. |
| `damage_by_range` | list of whole number, -32768 to 32767s | Damage in range bands 1 to 4. The last non-zero band sets the maximum range: 300, 500, 750 or 1500. |
| `accuracy` | whole number, 0 to 255 | 0 to 30: the accuracy bonus in percent, shown "+N%". |
| `reload` | whole number | 3 to 30, lower for better weapons. The design builder divides damage by it; almost certainly the delay between shots. |
| `area_radius` | whole number | 50 to 65 for area weapons, else 0. |
| `unknown5_c` | whole number | 5 to 25; meaning uncertain. |
| `unknown60` | whole number, 0 to 255 | Always 0. |
| `unknown61` | whole number, 0 to 255 | Always 0. |
| `tracking` | whole number, 0 to 255 | 255 for weapons that do not track (shown "No"); 0 to 100 for missiles, probably their tracking strength. |
| `weapon_type` | whole number, 0 to 255 | 0 normal, 1 planet bomb, 2 area weapon. |

## `class = "shield"` (6006)

Class 6006: screens, shields and fields, read. In the file `Shield.IsField` comes before `Shield.Regeneration`, although the original stores them the other way round in memory.

| Field | Type | Meaning |
|---|---|---|
| `shield_type` | whole number | 0 screen, 1 shield, 2 field. |
| `capacity` | whole number | Damage stopped before it fails. |
| `percentereduction` | whole number | Damage reduction in percent. |
| `is_field` | whole number, 0 to 255 | 1 for fields. |
| `regeneration` | whole number | Capacity restored per combat round. |

## `class = "fighter"` (6007)

Class 6007: carrier fighters, read. Seven int16.

| Field | Type | Meaning |
|---|---|---|
| `speed` | whole number, -32768 to 32767 | 6 to 10 (shown as Speed). |
| `weapon1` | whole number, -32768 to 32767 | Tech id of the fighter's weapon. |
| `weapon2` | whole number, -32768 to 32767 | Tech id of a second weapon, or 0. |
| `defense` | whole number, -32768 to 32767 | 30 to 75 (shown as Defense). |
| `hit_points` | whole number, -32768 to 32767 | Always equals the base rating (shown as Hit Points). |
| `accuracy` | whole number, -32768 to 32767 | 25 to 70: the accuracy of its shots in battle. |
| `missile_defense` | whole number, -32768 to 32767 | 5 to 50: taken off a missile's tracking when it rolls to hit. |

## `class = "armor"` (6008)

Class 6008, read.

| Field | Type | Meaning |
|---|---|---|
| `max_hull_class` | whole number, 0 to 255 | 2 to 5: the largest hull class the armor fits (shown as Max Hull). |
| `structure_multiplier` | number | 1.0 to 1.8; multiplies a design's structure. |

## `class = "special"` (6009)

Class 6009, read: ship specials and planetary installations (repair bays, colonizers, minesweepers, shipyards, missile bases, battle stations, fighter bases, minefields, academies).

| Field | Type | Meaning |
|---|---|---|
| `level` | whole number | Installation level 1 to 5, or repair amount, or minefield strength. |
| `strength` | whole number | 100 or 500 for installations, else 0. |

## `class = "improvement"` (6010)

Class 6010, read: planetary improvements. Five pairs of doubles, one pair per resource (credits, research, espionage, construction, population), read as per-population then flat. Only one resource per improvement is used (sim/economy.md); for population the flat value raises the maximum and the per-population value the growth.

| Field | Type | Meaning |
|---|---|---|
| `per_population` | list of numbers |  |
| `flat` | list of numbers |  |

## Hulls (`[[hull]]`)

A ship hull (`HULL` resource, class 7108, ids 2000 + hull type). A ship design picks a hull type 0 to 5 and looks up 2000 + type.

| Field | Type | Meaning |
|---|---|---|
| `structure` | whole number | Scaled by a per-design factor to give the design's structure points. |
| `capacity` | whole number | Space for components; a design fits while its used space is at most this. |
| `cost` | whole number | Scaled by the same factor, plus component costs, gives the design's cost. |

