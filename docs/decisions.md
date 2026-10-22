# Decisions

Deferred decisions from the plan, recorded as they are settled.

## Test framework (Phase 0)

xUnit v3, run on Microsoft.Testing.Platform (opted in through `global.json`).
With MTP the only package needed is `xunit.v3`; the VSTest adapter and
`Microsoft.NET.Test.Sdk` are not used. Test projects are executables, one per
`src/` project. Empty test projects are allowed (`build.sh` ignores MTP exit
code 8, "no tests ran") until their phase starts.

## No Wine (Phase 1, Jeff)

The original game is not run under Wine. Everything about the original
comes from static study of the executable and of its data files.
Consequences for the plan:

- The protocol in `docs/netcode/` is worked out from the code (NetMan and
  the WinSock, Mplayer and COMM-DRV call sites), not from captured traffic.
- Same-seed checks (Phase 3) compare against values derived from the code
  rather than captures from a running original, unless Jeff provides
  captures from a Windows machine.
- A legacy netplay test against the original (Phase 5) needs the original
  running on a Windows machine Jeff provides.

## Video codec (Phase 1, Jeff)

Jeff's call, overriding the project plan's decisions 7 and 11 for video only:

- The original movies are Smacker (`_Movie/*.smk`). The importer converts
  them to MP4 with H.264 video, and the port plays the MP4s, not the `.smk`.
- Import uses ffmpeg (with x264) built from pinned source in `deps/`. It
  runs only in `build.sh`'s import step and is never shipped.
- At runtime the port decodes video with an external library: FFmpeg's
  libavcodec and libavformat, built from the same pinned source as a
  decode-only LGPL build (H.264 decoder, MP4 demuxer, the movie audio
  codec), shipped next to the binary like SDL3. Bindings are our own,
  `LibraryImport` in `Heliograph.Media`, as with SDL.
- Movie sound goes into the same MP4 as AAC at 192 kbit/s (Jeff: lossless
  is not needed). Sound effects (WAV) and music (Ogg Vorbis from GOG)
  still use our own decoders.
- Video is standard 4:2:0 H.264, CRF 12, preset slow. 4:4:4 measured
  better against the Smacker frames (44.5 dB against 41.0 dB PSNR) but
  plays as a black window in common players such as VLC, so 4:2:0 was
  chosen for compatibility (Phase 2).

## No legacy netplay (Phase 1, Jeff)

The port will never play against the original executable, so there is no
legacy network mode. This resolves the "legacy netplay scope" item and
overrides the project plan's decision 12 and the legacy part of the Phase 5 exit.

- `Heliograph.Net.Legacy` and its tests are removed. Multiplayer is the
  modern protocol only.
- The original protocol is not documented byte for byte. `docs/netcode/`
  only records how the original keeps games in step and where it goes
  wrong, as input for the modern design.
- Matching the original's AI move for move is not a goal. The port's own
  AI must still be deterministic for a seed and input stream (decision 9),
  since modern netplay runs it on every client. Where the original AI
  budgets its thinking by wall-clock time, the port uses a fixed amount of
  work per turn instead.
- Trig in the simulation uses our own sin/cos, identical on every
  platform, instead of `Math.Sin`, so lockstep holds across Linux, Windows
  and macOS.

## Single-player fidelity kept (Phase 1, Jeff)

Same seed, same galaxy as the original (decision 8 stands). Dropping
legacy netplay does not relax it. For trig this means the port's own
sin/cos must return what x87 `fsin`/`fcos` return at 53-bit precision for
the arguments galaxy generation uses, not merely accurate values. Since
the original is not run, reference values come from executing those x87
instructions natively on an x86 CPU in a small test harness, with the
control word set as the game had it. Results could in principle differ
between CPU makers and generations, so the harness records which CPU
produced the reference values.

## Art as PNG (Phase 2, Jeff)

Jeff's call, overriding the project plan's decision 7 for art: the importer
converts all art out of the original formats, and the port loads the
converted files at runtime.

- `PPIC` pictures become 8-bit indexed PNG with the picture's palette in
  the PNG's palette chunk. The pixel indices stay exactly as the original
  has them, so palette effects (fades, remapping) still work, and the
  files open in any image viewer.
- `CPLT` palettes are written alongside as small text files, since some
  pictures are drawn with a palette other than their own.
- `pft*` bitmap fonts become a PNG glyph sheet plus a text file of glyph
  metrics.
- Anything a picture carries besides pixels (hotspot, transparency, the
  extra fields in its header) goes into a text sidecar next to it, so
  nothing is lost.
- Our own PNG writer and reader, using the zlib in the .NET base library.
  No NuGet package.
- Resource lookup rules of the original (including the duplicate id 27000
  in `Main.img`, where lookup by id gets the first picture) are kept in
  how the converted files are named and found.

## Phase 2 signed off (Jeff, 2026-10-18)

Data and media signed off. The listening check of sound is deferred to
Phase 4: the development machine has no sound output. Until then sound is
verified by tests (hashes for the WAVs, comparison with ffmpeg for the
music, alignment and length for movie sound).

## UI work starts before the engine sign-off (Jeff, 2026-10-18)

Jeff lifted the hard gate between Phase 3 and Phase 4 for UI: the main menu
is built properly now (renderer, art and font loading, window layouts,
input), interleaved with the remaining Phase 3 simulation work. The engine
sign-off still happens when Phase 3 is complete; it no longer blocks UI.

## Modern netplay is server authoritative (Jeff, 2026-10-18)

Settles the deferred "modern sync model" item early: server authoritative,
not simultaneous simulation (lockstep). Jeff: "switch to a server
authoritative instead of simultaneous simulation to avoid desync issues".

- One simulation runs, on the server. Clients send orders and show the
  state the server sends back; they never run the game rules themselves,
  so their games cannot drift apart.
- Orders still enter the simulation at tick boundaries, as the Phase 3
  rule already requires; the server decides the tick each order lands on.
- The per-tick state hash stays. The server sends it with each update and
  a client checks that what it applied matches. A mismatch means a lost or
  corrupt update and is fixed by a full snapshot from the server, which is
  the "recovery" the project plan asks for.
- The simulation stays deterministic and headless (decision 9). That is
  still needed for single player fidelity, replays and tests, and it lets
  the same Heliograph.Sim code run inside the server.
- Cross-platform identical math matters less now: no two machines have to
  agree tick by tick in multiplayer. It still matters for replays and
  saves moved between platforms.

Left for the start of Phase 5: whether the server is the hosting player's
game, a headless dedicated server, or both; the update form (snapshots,
deltas, how often); and whether clients predict anything.

## Battles nobody can win end with every side withdrawing (Jeff, 2026-10-18)

A battle resolved at once loops in the original until it ends, which can be
never: two sides that cannot hurt each other (unarmed ships, all weapons
damaged, a colony ship with no target, bombers at a colony with nothing
left to bomb). The original hangs there. Jeff chose: both sides withdraw.

The port checks after every round: no explosion still dying down, no ship
about to leave, no ship with a target it can damage (or, for a computer
side still fighting, a target it would pick), and no orbital with damage
and an enemy ship to fire at. Then every ship still on the field leaves it
as a retreating ship does, so each fleet goes home and its owner's caution
rises by 0.2. A last-resort limit of 100000 rounds ends any other endless
case the same way. Battles that end in the original are unchanged.

## Engine signed off without captures from the original (Jeff, 2026-10-18)

Jeff signed off the data layer, decoders and simulation at the Phase 3
gate (`signoff-engine.md`). No same-seed captures from the original are
coming for this gate, so the Phase 3 criterion "the galaxy matches the
original for test seeds" is waived: fidelity rests on the code study and
the tests built from it. Phase 4 goes ahead in full.

Two items from the review are put off, not dropped:

- Loading the original game's saved games moves from Phase 3 to Phase 4,
  next to the port's own save and load.
- The difficulty 1 stalemate in the soak runs (`ai/soak.md`) is noted and
  left until it can be compared with the original.

## The port's own save format, and a loader for the original's (Jeff, 2026-10-19)

The original saves its object graph (`formats/saves.md`) but not the
random number state, and it draws new random numbers while loading, so a
loaded original game never continues the way it would have. Several of its
fields are still undecoded, and it also stores interface state the port's
simulation does not keep.

Jeff chose: the port saves in a format of its own that holds the whole
game, the random number state included, so a loaded game continues tick for
tick as if it had never stopped (replays, state hashes and netplay rely on
this). A separate loader reads the original's single player saves and
applies the same resets the original applies on load. The original cannot
open the port's saves; a writer for the original's format can come later
if wanted.

## A side's retreat threshold never divides by zero (Jeff, 2026-10-19)

In a battle, a side that fights by itself measures how worn its enemies
are as what is left of their first ships' strength, in thousandths. When
that comes to under one thousandth, the original divides by zero and
crashes. The port counts it as one thousandth instead, which makes the
threshold so high that no ship retreats; nothing else changes. Jeff chose
this over skipping that think's retreat rolls.

## Music volume applies from the start (Jeff, 2026-10-20)

The original sends the stored music volume to the CD only when the music
slider is moved while music plays, so until then music plays at full
volume whatever the setting. The port applies the setting from the start;
Jeff chose this over the original's behavior.

## Networking starts before the vanilla sign-off (Jeff, 2026-10-20)

Jeff will play the port beside the original later; Phase 5 starts now.
The single player build as it stood is kept three ways: the `vanilla`
branch, `main` (dev merged in at that point, on Jeff's word), and a frozen
runnable copy in `deploy/vanilla/` (`run.sh --vanilla`), which build.sh
leaves alone. Fixes from his play-through go on dev and are merged into
`vanilla` as well.

## Phase 5 shape (Jeff, 2026-10-20)

- **Game server:** the hosting player's game runs the server in-process,
  and the same server code also runs headless on Linux for a dedicated
  game.
- **NAT:** game traffic goes through the lobby server, which Jeff hosts on
  a public machine, so no player has to open a port.
- **Lobby identity:** a name to start with (rooms may have a password);
  real accounts come later.
- **Screens:** new multiplayer screens of our own, built from the game's
  art and widgets in the original's style, rather than ports of the
  original's network screens (modes 2 to 7).

## A modern UI toolkit, drawn in true color (Jeff, 2026-10-20)

Settles the Phase 6 "resolution scaling, widescreen, UI scaling" item
early, and starts the UI side of the mod API.

- **Everything true color.** The renderer moves to a 32-bit picture at
  the window's own resolution. The original screens, still drawn into
  their 640x480 palette picture, are scaled into it by the largest whole
  multiple that fits, nearest-neighbor and letterboxed, so their pixels
  stay sharp. Original screens move onto the new toolkit one at a time;
  the lobby screens go first.
- **New controls** (check boxes, radio buttons, combo boxes, tree views,
  text boxes, sliders, tabs, scroll views and the rest) are skinned from
  the game's own art and fonts, and lay themselves out for any window
  size.
- **Forms in XAML-like XML**: an element per control, attributes for
  properties, Grid, StackPanel and DockPanel layout, named elements,
  event handlers by method name, and simple bindings to a C# view model.
  Read with .NET's own XML reader; no NuGet. Mods will be able to ship
  forms and register controls of their own (the mod API itself stays a
  Phase 7 decision).

## Upscaled art and a modern font (Jeff, 2026-10-20)

Nearest-pixel scaling of the original screens did not look good.

- **Art is remade at 2x** (1280x960 for a full screen) when the game data
  is imported, with an xBRZ-style pixel-art upscaler written in our own
  C#. The original screens then draw in true color at 1280x960 with the
  upscaled pictures, and that picture is fitted to the window. Other
  upscalers (an AI one such as Real-ESRGAN for the pre-rendered pictures,
  or a mix) stay open to try if xBRZ does not look good.
- **Text in a modern font**: Titillium Web (SIL Open Font License),
  rasterized by our own TrueType reader. The game's own bitmap fonts,
  upscaled, remain an option to compare, and so do the originals.
- Font Awesome is an icon font, not a text font; it may supply the
  toolkit's icons later.

## Outlined text (Jeff, 2026-10-20)

Text is drawn with a black outline so it reads over any background,
keeping the game's own colors, which say things (the picked row,
disabled entries, how good a world is). In the system view, over the
starfield and planets, the names are white. White everywhere, and plain
text, remain options.

## config.json for the public lobby (Jeff, 2026-10-20)

The public lobby's address lives in `config.json` beside the game (with
the LAN page's extra hosts), not in TOML: it is the file a copy of the
game folder carries to friends, and PUBLIC LOBBY connects to it without
asking for an address. The player's own settings (name, preferences)
stay in heliograph.toml in the per-user folder, written whenever they
change.

## Widescreen by widening the original screens (Jeff, 2026-10-21)

In a wide window the game screens keep their original art and layout and
are laid out wider at a column of each screen (docs/ui/widescreen.md),
rather than rebuilt as new forms. Screens that cannot widen (the bridge's
room picture, the setup screens) keep 4:3 with starfield bars.

## American English (Jeff, 2026-10-22)

The whole project is in American spelling: code, comments, docs, UI text
and commit messages (color, center, gray, defense, neighbor). Saves name
their types and fields, so the save reader puts names from older saves
into the new spelling (`Saving/Spelling.cs`); replays read the old
"colour" key too, and the console takes "colours" as well as "colors".

## Speed 0 pauses (Jeff, 2026-10-22)

In the original, speed 0 is the slowest speed (600 ms a step). At Jeff's
call it pauses the game instead, as a gameplay toggle (SpeedZeroPauses in
Options) that is on by default, an exception to decision 15's "off by
default". With it off the game runs at the original's slowest speed. In a
network game the host's speed 0 pauses everyone only after a vote every
connected player agrees to (docs/netcode/modern.md).

## Gameplay fixes (Jeff, 2026-10-22)

Of the original's bugs that change play (docs/sim/fix-candidates.md),
twelve are offered as fixes, each its own setting, all off by default as
decision 15 says: with none on, the game plays as the original. A game
keeps the set it started with.

## Phase 6 signed off; Phase 7 mod format (Jeff, 2026-10-22)

Jeff signed off Phase 6 (docs/signoff-phase6.md). For Phase 7:

- A mod is a folder holding a manifest (mod.toml) and either or both of
  a data pack (files laid out as the game's data folder, which replace or
  add to the game's) and a C# assembly (rules, AI, screens) through the
  mod API.
- Mods live in a mods folder in the user folder. Options lists them, each
  on or off, in an order (later ones win where two replace the same
  file). A game remembers its mod set.

## Simple Combat, the first gameplay mod (Jeff, 2026-10-22)

Jeff's first mod simplifies combat: no battle screen, and the game never
stops for a battle. His choices:

- A planet is taken by wearing its defenses down, then its population;
  when the people are nearly gone it falls.
- Any fleet can take a planet; no colony ship is needed.
- A siege takes days of game time, not a moment.
- Fleets fight only when one attacks, not on meeting.

It ships as a sample (samples/SimpleCombat) next to a sample data pack
(samples/NightMenu), both in deploy/mods/ and off until turned on. Mods
are found in the user's mods folder first and then the game's own, so a
player's copy of a shipped mod wins.

## Simple Combat: fleets at war that meet fight (Jeff, 2026-10-22)

Jeff changed "fleets engage only when one attacks": fleets at war now
engage on their own when they come close in a system, or circle the same
uncolonized planet. At a colony, his own or the enemy's, things stay as
they were: an attack starts a siege, and the colony's fleets defend it.

## Mod content and multiplayer mod sync (Jeff, 2026-10-22)

- Mods can add content, not only rules: new technologies of any kind,
  changes to the six hulls (no new hull types), premade ship designs, and
  new planet types.
- New planet types are drawn alongside the original ones while a galaxy
  is generated, so with such a mod a seed gives a different galaxy from
  the original. Without one, generation is the original's, draw for draw.
- In a network room the host's mods are the room's. The lobby keeps
  every player in step with them automatically: a joiner missing a mod
  gets it from the host with no prompt, C# mods included.
- A changed mod set is loaded in place, without restarting the game.

## Phase 7 signed off; Phase 8 begins (Jeff, 2026-10-22)

Jeff signed off Phase 7 (docs/signoff-phase7.md). Phase 8 builds the game
for Windows and macOS; Jeff runs them on his machines one step at a time.
- Targets: Windows x64 (win-x64) and Apple Silicon Macs (osx-arm64),
  besides Linux. Windows' native libraries are cross-compiled on the Linux
  machine with MinGW; the Mac's are built once on the Mac with a script.
- The .NET runtime packs for those platforms (Microsoft.NETCore.App.Runtime
  and its app host, at the SDK's version) may be downloaded from NuGet to
  publish them from Linux (Jeff approved).

## Open source on GitHub (Jeff, 2026-10-22)

Jeff wants the project public on GitHub, so its free Actions runners can
build releases for all three platforms, the Mac's included. This replaces
"private use only" and "local only" in the project plan.

- **Name**: OpenPax (the working name Heliograph goes). The README says it
  is unofficial and unaffiliated with the game's owners.
- **License**: GPL-3.0.
- **No game data ships.** On first run the game asks for the player's own
  copy: an installed GOG folder, or GOG's offline installer (.exe), which
  the game unpacks itself (our own Inno Setup reader). It imports from that
  into the player's user folder.
- **Movies** are still converted to MP4 on import, so releases carry the
  ffmpeg tool (GPL, with x264) for the import step; the game plays them
  with the LGPL decoding libraries as now.
- **Fresh history**: the public repository starts from one commit of the
  cleaned tree; this repository keeps the full history.
- **Author**: commits are made as Jeff <jlac1024@users.noreply.github.com>,
  GitHub's no-reply address, from now on and in the public repository.
