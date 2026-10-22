# The port's saved games

Code: `src/OpenPax.Sim/Saving/`. Files live in "Saved Games" in the
user folder (the SDL preference folder, or `build/dev` for dev runs), one
per name with the extension `.hgsave`. Characters a file name cannot hold
become "_". The original's own saves are a different format
(`decisions.md`, 2026-10-19).

## Why it is built this way

A save holds the whole game, the random number state included, so a loaded
game goes on tick for tick as if it had never stopped. The tests check
that a loaded game equals the saved one field by field and that both then
give the same state hash at every tick for 2000 ticks.

Every object is written by its fields' names, found by reflection, rather
than field by field by hand: nothing can be left out by mistake, and a
field added later simply keeps its empty value in an older save.

## Layout

Little endian throughout.

1. Magic "HGSV" (int32 0x56534748), then the version (int32, 1).
2. The root object: the game with the screens' memory around it
   (`SavedSession`: the star in view, the selection, sort columns and the
   other per-screen choices, the design being made). The clock is not saved;
   a loaded game gets a new one at the GameSpeed preference, as the
   original does.

Each value starts with a tag byte (`SaveTag`):

| Tag | Value |
|---|---|
| 0 | null |
| 1 to 13 | bool, byte, sbyte, int16, uint16, int32, uint32, int64, uint64, double, single, char (uint16), string (length-prefixed UTF-8) |
| 14 | enum: type name, then the value as int64 |
| 20 | an object already written: its number (int32) |
| 21 | a new object: type name, field count, then name and value of each field in name order |
| 22 | a value type, in place: type name and fields as for 21 |
| 23 | an array: element type name, length, items |
| 24 | a List, HashSet or SortedSet: type name, count, items |
| 25 | a dictionary: type name, count, key and value pairs |
| 26 | game data: table kind (int32: 0 the tables, 1 galaxy names, 2 species, 3 techs, 4 hulls) and index (int32) |

Objects (tags 21, 23, 24, 25) are numbered from 0 in the order they are
written, so shared objects stay shared. Names (types and fields) are
written as an int32 number, and the first time a number is used the name
follows as a string. Type names leave out assembly versions: generic types
are written as the definition's name with the arguments in angle brackets.
A list the compiler made for a collection expression is written as an
array.

Fields named in the save that the type no longer has are read and
dropped. The computer player module is not saved; the running one is put
back on load. The game data is not saved; a save is read against the
same imported game data.
