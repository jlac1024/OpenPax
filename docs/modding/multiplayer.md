# Mods in multiplayer

A network game plays the room host's mods. Everyone else gets them
automatically: there is nothing to install by hand.

## In a room

- The room list shows each room's mods.
- Join any room, whatever your own mods. If yours differ from the host's,
  the game fetches the ones you do not have from the host, through the
  lobby, and loads the host's set in place. The room's MODS panel says how
  far the fetch has got, and the players list marks anyone still
  fetching.
- The host's START opens once every player is ready and has the room's
  mods.
- Leave the room and your own mods come back.

The host turns mods on and off for the room in the room's MODS panel
(ON / OFF, UP, DOWN), as in Options. The host's game loads the new set at
once, and every player follows.

A dedicated server's room plays the mods it was started with
(`--mods`), and players fetch them from it the same way.

## What makes two mods the same

A mod set names each mod by its key: id, version and the first 12
digits of a hash of every file in the mod (`id@version#hash`). Two players
hold the same mod only when all three match, so a mod changed on one
machine and not bumped in version is still told apart. The lobby and the
game server compare whole sets, in order.

## What is sent

- A mod travels as a package: a zip of its folder, made the same way
  every time. The lobby keeps a copy while it has room (the oldest unused
  go first), so the host is asked for each mod once.
- A fetched mod is unpacked into `mods-synced/` in your user folder, one
  folder per mod and hash, apart from your own `mods/`. It is used only
  in rooms that play it.
- Before it is used, a package is checked: no file may land outside its
  folder, it may hold no more than 64 MB, and its files must hash to the
  key the room named. A package that fails is thrown away.
- C# mods travel too, and run on your machine like any mod you install.
  Join rooms of hosts you trust.

## Loading in place

Mods load without restarting the game: the old mods are let go, their
data packs, pictures, sounds and tables are read again with the new set,
and the screen is made again. This happens when you leave Options with
the mods changed, and when a room's set comes and goes. It never happens
during a game: a running game keeps the mods it started with.
