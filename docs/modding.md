# OpenPax modding

A mod changes the game without touching its code. It is a folder with a
`mod.toml` in it, holding a data pack, a C# assembly, or both:

- A **data pack** replaces or adds game files (pictures, sounds), and
  adds content in plain TOML: new techs, planet types, premade ship
  designs, changes to techs and hulls.
- A **C# mod** changes the rules: it hears the game's events, acts after
  every step, takes battles over, and gives the screens numbers to show.
- A **Lua mod** does the same from a script, with nothing to build: a
  `main.lua` in the mod's folder ([Lua scripting](modding/lua.md)).

```
mods/
  my-mod/
    mod.toml        the manifest: id, name, version, description
    data/           optional: files laid out like the game's data folder
    MyMod.dll       optional: the C# assembly mod.toml names
    main.lua        or instead: the Lua script mod.toml names
```

Mods are turned on and ordered in Options, on the MODS tab, and load as
you leave Options. A game remembers the mods it started with. A network
game plays the room host's mods, which every player fetches
automatically.

## The pages

- [Getting started](modding/getting-started.md): a C# mod from an empty
  folder to a running game, step by step.
- [The manifest and load order](modding/manifest.md): mod.toml, where
  mods are found, how they are turned on and ordered.
- [Data packs](modding/data-packs.md): replacing pictures and other
  files.
- [Content](modding/content.md): new techs, planet types and premade
  designs, and changes to techs and hulls, in TOML. The
  [content fields](modding/content-fields.md) reference lists every field.
- [Lua scripting](modding/lua.md): mods as Lua scripts, the API they
  see, and how saves carry across mod changes.
- [Rules for mod code](modding/rules.md): determinism, saves, network
  games, what runs where.
- [Multiplayer](modding/multiplayer.md): how a room's mods reach every
  player, and what is checked.
- [The samples](modding/samples.md): Simple Combat, Bounty, Arsenal and
  Night Menu, the mods that ship with the game.
- [API reference](modding/api.md): every type and member a mod can use,
  made from the game's own doc comments each build.

The same pages are in the game's folder as a small website,
`docs/modding/index.html`, which opens in any browser with nothing to
install.
