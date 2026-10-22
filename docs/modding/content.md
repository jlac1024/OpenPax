# Content

A data pack can add to the game's tables as well as replace its files:
new technologies of any kind, changes to the game's own techs and its six
hulls, premade ship designs, and new planet types. Content is plain TOML,
so it needs no code. The [Arsenal sample](samples.md#arsenal) has one of
each.

Content files go in the pack's `content/` folder, any number, any names
ending in `.toml`:

```
mods/
  arsenal/
    mod.toml
    data/
      content/
        arsenal.toml
```

Files are read in name order, mod by mod from the top of the Options list
down, so a mod can build on what an earlier one added. Content the game
cannot read is reported when the game starts, and that entry is left
out; the rest load.

## Techs

### A new tech

```toml
[[tech]]
key = "storm-lance"          # the tech's name in content files
like = 107                   # copy everything from tech 107, then change:
name = "Storm Lance"
description = "A beam that holds together at long range."
research_cost = 900000
prerequisites = [107]
damage_by_range = [700, 450, 300, 0]
```

- `key` names the new tech. Other entries refer to it by this key where
  a tech id goes (`like`, `prerequisites`, a fighter's weapons, a design's
  parts). New techs are numbered after the game's own, in the order read,
  so the same mods always number them the same.
- `like` copies a tech, the game's (by id) or an earlier new one (by key):
  every number, its picture, its research line, its name and description
  until you give your own. The new tech also takes the looks of the one
  it is like where the game picks art by a tech's place (a fighter's
  sprites).
- Without `like`, give `class` (`star_drive`, `tactical_drive`, `sensor`,
  `ecm`, `weapon`, `shield`, `fighter`, `armor`, `special`,
  `improvement`) and every field yourself; the rest start at zero.
- `name` and `description` are what the screens show.
- Any other field changes the tech: the fields every tech has, and those
  of its class, are in the [content fields](content-fields.md) reference.
  A name that is not a field is reported and ignored.

The `kind` field decides what a tech is for:

| Kind | What | Kind | What |
|---|---|---|---|
| -2 | planetary improvement | 13 | targeting sensor |
| -1 | theory (learned, nothing to build) | 14 | ECM |
| 1 | repair | 15 | energy weapon |
| 2 | colonizer | 16 | projectile weapon |
| 3 | minesweeper | 17 | missile |
| 5 | shipyard | 18 | planet bomb |
| 6 | training base | 19 | area weapon |
| 7 | missile base | 20 | screen |
| 8 | battle station | 21 | shield |
| 9 | fighter base | 22 | field |
| 10 | mine field | 23 | fighter |
| 11 | star drive | 24 | armor |
| 12 | tactical drive | | |

A new tech joins the end of its kind: the game counts it the best of
its kind where it looks for the latest one (the computer players' designs,
a colony's defenses), and a mine field's or minesweeper's level is its
place in its kind.

`research_group` puts a tech on a line of the research tree; a copy keeps
its original's. A tech is researchable once its `prerequisites` (up to
four tech ids or keys) are known.

### Changing one of the game's techs

```toml
[[tech]]
id = 38          # the game's first star drive
rating = 3       # one more jump from supply
```

An entry with `id` and no `key` changes that tech in place. Two mods
changing the same tech both apply, in order.

### Pictures

A tech's `picture` is the first of its 36 animation frames in the tech
pictures, with its 50x50 icon the one before. A copy uses its original's.
For new art, add the pictures to the pack (`data/art/tech/<id>.png`, see
[data packs](data-packs.md)) and point `picture` at them.

## Hulls

The game has six hull types, and that stays: their buttons, art and
battle sizes are fixed. Their numbers and class names can change:

```toml
[[hull]]
type = 3                 # 0 to 5
capacity = 2500
structure = 900
name = "Heavy Cruiser"
```

The fields are in the [content fields](content-fields.md#hulls-hull)
reference.

## Premade designs

```toml
[[design]]
name = "Lancer"
hull = 3                 # 0 to 5
quality = 4              # armor class 0 to 4, as the design screen's buttons
parts = [
  ["storm-lance", 0, 2], # tech, weapon arc, count
  ["aegis-shield", 1],   # tech, count: a weapon goes in the bow
]
```

A part is a tech (id or key), a weapon arc (0 bow, 1 port, 2 starboard,
3 point defense; anything not a weapon takes -1) and a count. Human
players get the design, in a free design slot, as soon as they know every
part: at the start or when the last part is learned. One they delete
comes back with their next tech. Computer players design their own ships
and get none.

## Planet types

```toml
[[planet]]
key = "crystalline"
name = "Crystalline"
like = 2                 # looks like atmosphere class 2 (0 to 4)
weight = 1               # as common as each of the original's five classes
breathable = "none"      # like, all, none or anaerobic
rating_bonus = 2         # added to the planet's rating: more population, more wealth
```

New planet types are drawn alongside the original's atmosphere classes
while a galaxy is generated, so with them a seed gives a different galaxy
from the original's. Without any, generation is the original's, draw for
draw.

- A new type looks like, and is treated in battle and on the screens as,
  the class it is `like`. Its name shows on the Sector Display's planet
  panel.
- `breathable` says who can breathe it: as its look-alike class (`like`),
  every species, none, or only anaerobic species.
- Homeworlds are always one of the original's classes.
- `weight` sets how often it is drawn among rocky planets: 1 is as often
  as each original class, 0.5 half as often. Gas giants are left alone.

## Saves and network games

Content is part of the game's tables, not of a saved game: a save loads
only with the same mods. A network room shares the host's mods, content
included ([multiplayer](multiplayer.md)).
