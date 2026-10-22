# Phase 7 (modding): ready for sign-off

The project plan's exit: a sample C# mod changes game rules, a sample data pack
replaces art, modern netplay refuses mismatched mod sets, and no core
code was touched to make either sample work.

The mod format was settled at the start of the phase (decisions.md,
2026-10-22): a mod is a folder with a manifest, holding a data pack, a C#
assembly, or both. Jeff added content packs and multiplayer mod sync
during the phase (decisions.md, same day).

## Done

- **Loading**: mods are found in the user folder's `mods/` and next to the
  game, turned on and ordered in Options, and loaded in place as Options
  closes, with no restart. A running game keeps the mods it started
  with, and saves record them.
- **Data packs**: files over the game's own, the last mod winning, with
  2x art handled (docs/modding/data-packs.md).
- **Content**: new techs of any kind, changes to the game's techs and the
  six hulls, premade ship designs and new planet types, in TOML
  (docs/modding/content.md). Colonies make room for mods' improvements.
- **C# mods**: an entry point, hooks for tables, new games, every step,
  battles, colony ratings and attack odds; events for every player;
  actions on the game; a saved store and a deterministic random stream of
  their own (docs/modding/api.md, generated from the doc comments each
  build).
- **Multiplayer**: a room plays its host's mods. Joiners fetch them
  through the lobby and load them in place; the host changes them in the
  room; the start waits until every player has them; a mod is named by
  id, version and its files' hash (docs/modding/multiplayer.md).
- **Samples**, shipped in `deploy/mods/`:
  - Simple Combat (rules: battles become a live attrition game),
  - Bounty (rules: the getting started guide's mod),
  - Arsenal (content: a weapon, a shield, an improvement, a planet type, a design),
  - Night Menu (a data pack replacing the Game Start picture).
- **Docs**: guides and the API reference as Markdown in docs/modding and
  as a site in `deploy/docs/modding/`.

## The exit, item by item

- *A sample C# mod changes game rules*: Simple Combat replaces battles
  outright; Bounty pays for kills. Both are loaded from a mods folder in
  their tests, as the game loads them.
- *A sample data pack replaces art*: Night Menu replaces the Game Start
  picture; Arsenal adds content.
- *Modern netplay refuses mismatched mod sets*: the game server refuses a
  client whose set differs, and the lobby will not start a room until every
  member's set is the host's. Rather than turning a player away, the room
  now brings their set into line (Jeff's call).
- *No core code touched to make either sample work*: the samples use only
  the public mod API. The core did grow that API during the phase (hooks,
  events, actions, content, fight display), each piece general and
  documented, none naming a sample.

## Checked

- The original game is untouched: the 30,000 tick, six player headless
  game on seed 1234 still ends on state hash 2a406db3c98287eb, and the
  replay recorded on 2026-10-19 matches all 20,000 ticks. Mods' state,
  planet types and fixes enter the hash only when present.
- The game starts with Simple Combat, Arsenal and Bounty on, with no
  errors.
- A room's set loads in place over the player's own with the real
  imported data, its content reaching the tables and the screens' names,
  and a C# mod's assemblies are freed when another set takes their place.
- Two apps in a lobby: the joiner fetches the host's mod, loads it, the
  host's START opens, and the joiner's own mods come back on leaving.

## Not checked yet

- Playing with the mods: Simple Combat's balance, Arsenal's numbers.
- Mod sync between two real machines over the LAN and the public lobby.
- How the mod panels look in a real window.
