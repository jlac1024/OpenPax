# Data packs

A data pack is a mod's `data/` folder. It mirrors the game's own data
folder (`data/` next to the game): a file at the same place replaces the
game's, and a file the game does not have is added.

```
mods/
  night-menu/
    mod.toml
    data/
      art/main/31000.png      the Game Start picture
      art2x/main/31000.png    its 2x copy
```

## Which file wins

When the game looks for a file it tries the packs from the mod lowest in
the Options list up, then its own folder. So the last mod in the list
wins where two replace the same file.

## Pictures

Pictures are PNGs in `art/<set>/<id>.png`, where the set is the image file
they came from (`main`, `universe`, `planets`, `ships`, `combat`, `empire`,
`infra`, `surface`, `tech`, `advisors`) and the id is the picture's number
in it.

- **They are 8-bit indexed.** A picture is drawn with the palette of the
  screen showing it, not its own, so a replacement must use the same
  palette indices as the picture it replaces. The easiest way is to start
  from the game's own picture and change only its indices, as the Night
  Menu sample does.
- **2x copies.** Upscaled art sits in `art2x/` at the same place. The
  game uses a 2x copy only from the same pack as the 1x picture (or a
  later one). A pack that replaces a 1x picture without a 2x copy gets its
  own picture scaled up, never the game's old 2x one.

## Other files

Sounds, music and the game's tables are under `data/original/` as the
game's own files. Fonts are under `data/fonts/`. Replacing them works the
same way, file for file.

## Never ship the game's own files

A pack made by changing the game's art holds the game's art, which you
may not share. The Night Menu sample builds its picture at install time
from the player's own game data instead: `samples/NightMenu` is a small
program that reads the imported picture and writes the pack.
