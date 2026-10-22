# LAN test (Phase 5 exit)

What the exit asks: three port clients finish a game over the LAN and
through the lobby with no undetected desync, and a forced desync is
caught and recovered. The forced desync is covered by the tests
(`LoopbackGameTests`); this is the part that needs people and machines.

Each machine needs a copy of `deploy/` (it is self-contained; copy the
whole folder). Several copies of the game on one machine also work for
a first try.

1. On the machine that runs the lobby: `./run.sh --lobby-server`. It
   listens on port 10421, TCP for players and UDP for LAN discovery;
   other machines must be able to reach both (open them in a firewall if
   one is on).
2. On each player's machine: start the game, Multiplayer Game, then
   LAN (TCP/IP). The lobby shows in the list by its machine name within
   a couple of seconds; pick it, check the name, CONNECT. (PRIVATE LOBBY
   with the lobby machine's address, for example `192.168.1.20`, works
   too, as does PUBLIC LOBBY when `publicLobby` in deploy/config.json
   names it.)
3. One player: NEW ROOM. The others: pick it, JOIN.
4. Everyone chooses a species, flag and color; the host sets the
   galaxy; the others tick "I am ready"; the host presses START.
5. Play to the end. Try the chat (Return in a game screen), orders,
   research, a battle, Ship Design's AUTO and ACCEPT on a guest.
6. Note anything that looks out of step between the players.

The same with a dedicated server instead of a player hosting:

    ./run.sh --dedicated 192.168.1.20 --name Box --min-players 3 --replay-out build/lan-replay.toml

then three players join its room and tick ready; it starts by itself.
The replay it writes at the end can be checked with
`deploy/openpax --verify-replay build/lan-replay.toml`.
