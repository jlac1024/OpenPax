# Modern multiplayer

The port's own multiplayer, between port clients only. Shape decided by
Jeff (decisions.md, "Modern netplay is server authoritative" and "Phase 5
shape"). This file is the design; it grows as the pieces land.

## Pieces

1. **Commands.** Every player action the screens take becomes a command:
   a small record naming the player, the action and its arguments, which
   `Game.Apply` carries out between ticks. Single player applies a command
   at once, as now; a client sends it to the server instead. The headless
   runner's replays gain the command stream. Notices and step events
   become per player state the client reads, not a queue it drains.

   Done: `src/OpenPax.Sim/Commands/` (one record per action, objects
   by id, carried in the save format) and `Game.Apply`, which checks that
   what a command names is the player's (a battle order may name only the
   player's own ships and orbitals). The screens send everything through
   `GameSession.Send`. AUTO draws from the game's random numbers, so it is
   a command whose design comes back as the player's `Draft`. Events and
   notices are kept per human player (`Game.TakeEvents`, `TakeNotices`);
   in the original only the machine's own player got them, so here every
   human does. A game can record every command it applies, with how
   many steps had run before it (battle steps do not move the tick), and
   the state hash after each step (`Game.StartRecording`; the recording
   is marked [NotSaved], so it is never saved or sent). A replay file
   holds the setup, the humans, those commands in the save format, and
   the hashes; `--verify-replay` re-applies each command before its step.
   A dedicated server given `--replay-out PATH` writes its game's replay
   when it stops.
2. **Wire format** (`OpenPax.Net`): length-prefixed messages over TCP,
   reliable and in order, so the game layer needs no resends. Hello
   (version, mod set, name), room and chat messages, commands, state
   updates, ready and start.

   Done (`src/OpenPax.Net/Wire`, `Messages`): a frame is a 4 byte
   length, a type byte and the body; numbers little endian, strings UTF-8
   with a length. Hello, Welcome, Refused, Command, State,
   SnapshotRequest and Chat so far; room messages come with the lobby.
   `Connection` reads on a background thread into a queue the game loop
   drains, so a frame never waits on the network.
3. **Game server** (`OpenPax.Net`): owns the one `Game` and its clock,
   queues commands and applies each at the next tick boundary, and sends
   state updates. It runs inside the hosting player's game, or headless
   (`openpax --dedicated`).

   Done (`GameServer`): a client is seated on the first human player not
   taken; another protocol version or mod set, or no free seat, is
   refused. A client may command only its own player and may not cheat.
   The hosting player's commands apply at once on the same game.
4. **State updates.** The server writes the game in the save format after
   a tick and sends each client the bytes XORed with the last state that
   client was sent, compressed with Deflate, plus the tick and the state
   hash. A client rebuilds the bytes, loads the game from them and checks
   the hash; a mismatch or a gap asks for a full snapshot. Updates go out
   at most every 100 ms, so a burst of battle ticks is one update.

   Done (`GameClient`, `StateMerge`): a client keeps one game for the whole
   match. Each state is loaded as a second copy and merged into it
   (`src/OpenPax.Sim/Saving/StateMerge.cs`): objects with an identity
   (players, stars, planets, fleets, ships, battles and the rest) keep
   their instances so the screens' references stay live, and the merged
   game saves to the same bytes as the server's. A hash that does not
   match, or a state that cannot be rebuilt, counts as a desync and asks
   for the whole game; tests spoil an update on purpose and see it caught
   and recovered. In the app a session plays through a link
   (`HostLink`, `ClientLink`); single player has none.
   Measured on a 20 star game: 340 KB whole, about 50 KB compressed, about
   2.6 KB per tick as a compressed diff, 6 to 10 ms to write.
5. **Lobby server** (`OpenPax.LobbyServer`, headless): names, rooms
   with an optional password, chat, ready check, version and mod set
   check, and the relay that carries all game traffic between a room's
   server and its clients, so no player opens a port. A dedicated game
   server connects to the lobby like a player's game does.

   Done (`src/OpenPax.Net/Lobby`, `openpax-lobby`, `run.sh
   --lobby-server [--port N]`, port 10421 by default):
   - The port takes plain TCP and WebSocket arrivals alike, so the lobby
     can sit behind a Cloudflare tunnel (docs/netcode/cloudflare-tunnel.md).
   - A player arrives with a name, 1 to 24 characters, which nobody else
     in the lobby may have (case does not count). Another protocol version
     is refused. Mods do not keep anyone out of the lobby.
   - Everyone outside a room sees the room list as it changes, and chats
     with each other; members of a room chat within it.
   - A room has a name, an optional password and 2 to 8 seats. Joining is
     refused for a wrong password, a full room, a game already started, or
     a mod set other than the host's.
   - Members mark themselves ready; the host starts once every other
     member is. The host leaving closes the room for everyone in it.
   - Starting issues a one-time ticket per member. The host opens one
     relay connection per member and each member one, all to the lobby's
     own port, each beginning with its ticket. The lobby pairs the two
     ends and from then on copies bytes both ways without reading them, so
     the game server and clients run over the relay unchanged. A ticket
     not used within a minute lapses.
   - A dedicated game server joins as a room's host:

     ```
     ./run.sh --dedicated host:port [--name NAME] [--room NAME] [--password P]
              [--stars N] [--opponents N] [--difficulty N] [--min-players N]
     ```

     It opens the room with that galaxy, starts once at least
     min-players members (2 by default) are in and every one is ready,
     makes the game from the members' choices (`DedicatedServer`), serves
     it through the relay with no player of its own, and stops when the
     game is over, when every player has left (after a minute's grace),
     or on Ctrl+C. Clients are seated by their lobby name, so each gets
     the seat made for them whatever order their relay connections
     arrive in.
6. **Lobby client and screens**: new screens in the original's style
   (connect, room list, room with chat and ready check, game settings),
   reached from Game Start's MULTIPLAYER button.

   Done: `docs/ui/lobby.md`.
7. **Multiplayer rules**: what the original does differently with more
   than one human (who sets speed and pause, players who leave, treaty
   offers between humans), studied from the executable
   (docs/netcode/original.md).

   Done, as the original does it:
   - Speed and pause keys do nothing in a network game; nobody pauses it,
     and Game Options does not pause it either. The host sets the speed
     in Game Options; a guest's speed control is shut. The server sends
     the new speed as session news (`SessionNewsMessage`, wire type 8);
     a guest writes it to its GameSpeed preference, whose watcher sets
     the clock and posts the notice, as the original's order 0x14 does.
   - The port's own (decisions.md): with SpeedZeroPauses on, the host's
     speed 0 pauses everyone, so it opens a vote instead (`PauseVoteOpened`
     session news). Each guest gets a yes or no question; the game pauses
     when every connected player says yes, and goes on at its speed on a
     no or after 30 seconds without every answer. The host's own yes is
     taken as given; a host alone pauses at once. Raising the speed again
     resumes with no vote.
   - A player who leaves stays in the game as an empire with nobody
     giving it orders. Everyone still playing, the host too, sees the
     name and list 86's line: left the game when the connection was
     closed, timed out when it broke.
   - Treaty and trade offers to a human already wait for that human's
     answer (the AI answers only for computer players).

## Not covered

Real accounts (later, decisions.md). Hiding what a player should not see
from a modified client: every client receives the whole game.

## Exit (the project plan's Phase 5, legacy dropped)

Three port clients finish a game over the LAN and through the lobby with
no undetected desync, and a forced desync is caught and recovered.
