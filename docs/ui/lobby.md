# Lobby screens (port only)

The original's network screens (modes 2 to 7: connection type, game list,
host game, modem) are not ported; Jeff chose new screens in the
original's style (decisions.md, "Phase 5 shape"). They take the original
modes 2 to 4, so they count as setup screens: setup music, no top bar.
Each is a form (docs/ui/forms.md) in `src/OpenPax.Ui/Forms/Markup`
(LobbyConnect, LobbyRooms, LobbyRoom) with its view model beside it, and
fills the window at any size.

## Connect (mode 2)

Game Start's Multiplayer Game (or the rooms, if still connected) swaps
the five main buttons for the port's multiplayer choices, as Single
Player Game swaps them for its own: LAN (TCP/IP), Public Lobby, Private
Lobby and Previous Menu, in the first four buttons' places with the port's
captions (Exit Program hides). A choice opens its page of this screen:

- LAN (TCP/IP): lobbies on the local network are listed as they answer a
  broadcast (docs/netcode/lan-discovery.md); pick one.
- PUBLIC LOBBY: the lobby named by `publicLobby` in deploy/config.json,
  shown but not typed (docs/netcode/cloudflare-tunnel.md).
- PRIVATE LOBBY: an address or DNS name typed in, as host, host:port, or
  ws:// / wss:// for a lobby behind a web tunnel. The last one typed is
  kept in openpax.toml.

Each page also takes the name others see (kept in openpax.toml).
CONNECT, or Return in a field, tries for ten seconds; what went wrong
shows below: no answer, a name taken, another version. BACK, and LEAVE
in the rooms, go back to Game Start on its multiplayer choices. A lost lobby reopens the page it was joined from
with the reason.

## Rooms (mode 3)

The rooms, each with its seats taken and whether it has a password or is
playing; the picked one's host, seats and password under the list; the
chat of everyone not in a room on the right. NEW ROOM opens "NAME's
game" with 8 seats and the password typed, if any; JOIN (or a double
click, or Return in the list) joins the picked room with that password.
A line typed under the chat goes on Return. LEAVE leaves the lobby for
Game Start. A refusal shows under the password.

## Room (mode 4)

The members, the host first, each with ready and species shown. Each
member chooses a species (the stock ones), a flag (1 to 30, its picture
beside it) and a fleet color (1 to 16); until a member chooses, the seat
takes the stock species in turn and its own number as flag and color.
The host sets stars, computer opponents and difficulty; members see them.
Members tick "I am ready"; the host's START opens once every member is.
The room's chat starts empty. LEAVE goes back to the rooms; the host
leaving closes the room.

When the game starts the host's game is made by the host program from
the room's galaxy, a clock-picked seed and the members in seat order with
their choices, and the game server runs in the host's game with a relay
connection per member. A member's game arrives through its own relay
connection and is played once the first whole state is in. Both go to the
bridge.

## Designed species

The room's species list holds the stock species, then the player's
designed ones (from Player Design, kept in SpecData in the user folder),
marked "(designed)". A designed species goes to the lobby whole with the
seat choice, in SpecData's own layout (one record, at most 16 KB), and the
host or a dedicated server reads it back as the original reads SpecData
(values out of range take the defaults, a species over the points budget
is reset). One that does not read leaves the seat its stock species.
Everyone in the room sees its name.

## In the game

Return in a game screen (when no field is being typed in) opens the chat
line at the bottom; Return sends it to everyone in the game, Escape
closes it. Lines arrive on the message line and stay in its history,
each under its speaker's name. The server passes lines of up to 200
characters. Ship Design's AUTO and ACCEPT on a member's game finish when
the server's state with their result arrives.
