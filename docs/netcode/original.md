# The original's multiplayer

How the original keeps a multiplayer game consistent and where it fails.
Static study only, written to inform our own protocol; the port does not
speak this protocol (decisions.md). Addresses are VAs.

## Summary

Deterministic lockstep in a star. Every machine runs the whole
simulation, AI included, from a seed the host hands out. During play only
player orders travel, one packet per player per network frame. The only
consistency check is a single number drawn from the game RNG that every
packet carries. There is no state hash and no recovery: a player who
disagrees is dropped.

## Topology

- A session object (constructor) holds up to 16 connections.
  Clients connect only to the host, and the host relays every game
  packet to everyone except the sender.
- The host decides membership, version and password checks
  , the game setup and seed, the frame length,
  and drops. It has no authority over game state.

## Transports

Chosen by a session field through a factory. The game layer
sees only whole packets through a small interface (send, receive,
status), so transports are separate from game logic.

| Transport | Notes |
|---|---|
| TCP/IP | Stream sockets on port 10212 and up for play; UDP broadcast on 10314 only to find games |
| IPX/SPX through WinSock | SPX stream for play, IPX broadcast to find games |
| Modem and serial | COMM-DRV library; probes COM1 to COM4 with AT commands |

The Mplayer function names in the executable are never used; that
support appears to be dead code. Play relies on stream transports for
reliability; there is no acknowledgement or resend in the game layer.

Each transport runs accept, receive and send threads (created at
) that only touch their own buffers, under critical sections.
All packet handling and simulation happen on the main thread.

## Messages

Every message starts with five 32-bit words: an auxiliary value (the
sync sample or a reason code), sender id (0xFF for the system), frame
number or slot, type, and total length. At most 3,020 bytes. On stream
transports, a receive thread collects bytes into an 8 KB buffer and the
reader splits packets by the length word.

| Type | Purpose |
|---|---|
| 0x3E8, 0x3E9 | Find games; host describes its game |
| 0x3EA to 0x3EC | Join request; accept with slot; reject with reason (full, password, version, timeout) |
| 0x3ED, 0x1F6 | Player count after a join |
| 0x3EE, 0x3EF | Host closes the game; client acknowledges |
| 0x3F0, 0x1FA | Player left (lost, dropped, error) |
| 0x1F5 | Game setup, including the seed (copied in) |
| 0x1F7, 0x1F9 | Join failed; session or host lost |
| 0x1FB | Frame with no order |
| 0x1FC, 0x1FE | System broadcasts |
| 0x1FD | Chat |
| 0x1FF, 0x200 | Raise or lower ticks per network frame |
| 0x00 to 0x14 | Game orders (fleets, planets, espionage, combat, diplomacy; 2 starts the game, 0x12 registers a player and species) |

Orders come from UI windows through one path.
In single player it runs the order at once; in multiplayer it queues the
order for the network.

## Lockstep

1. The host's setup message carries the seed. Game start
   seeds the game RNG from it.
2. The game clock runs simulation ticks paced by real time,
   up to N ticks per network frame, then waits until every active
   player's packet for the frame has arrived.
3. At the frame boundary every machine applies the orders
   from the previous frame in player order 0 to 15, then sends its next
   packet. A packet carries at most one order. A player's
   own orders also wait for the round trip, so input delay is one frame.
4. Every 5 frames the host measures stalls and time per frame and may
   change N (1 to 30). The change travels in the lockstep stream, so it
   affects pacing, not results.
5. Each packet carries `Random(300)` from the shared game RNG. The host
   compares every player's value with slot 0's and drops any player that
   differs. That is the whole desync check.
6. A connection silent for 8 seconds is dropped; joins time
   out after 15 seconds.

## Why it desyncs, and what the port does differently

| Weakness in the original | Port |
|---|---|
| The check samples the RNG only: 300 possible values, so roughly 1 in 300 frames with a mismatch pass unseen, and divergence that never reaches the RNG is invisible | A hash of the full simulation state every tick |
| Only the host checks, only against slot 0, and the response is a drop | Every peer compares; a mismatch is reported and recovered (resync from the authoritative state) |
| UI code is allowed to draw from the simulation RNG (the galaxy preview does). It is reseeded before play, but nothing stops another window from doing it mid-game. | The simulation RNG lives inside the game state and UI has no access to it |
| A second, clock-mixed random source feeds combat effects. It looks cosmetic, but it is called while combat objects are created. | Cosmetic randomness gets its own stream outside the simulation |
| x87 floating point in combat and drawing, never separated from simulation math | Deterministic math helpers (own trig) identical on all platforms |
| Unchecked buffer and length handling in the receive path, and a third queued packet per player is leaked | Length checks and bounded queues |
| One order per player per frame | Any number of orders per frame |

A dropped player's empire stays in the game: the player lost message
(0x1FA, from a leaver's 0x3F0, a silence or a sync failure) clears the
seat's active byte, so the frame boundary stops waiting for
it and its orders, and every machine posts "<name>" plus list 86's line
for the reason (lost sync, timed out, left). Nothing turns
the empire into a computer player (the AI runs only for computer player
objects), so it goes on with nobody giving it orders.

Speed and pause: the speed keys and the pause key do
nothing in a network game. Only the host may set the speed, in Game
Options (its control is shut for the others); the change goes
to everyone as order 0x14, whose execute writes the
GameSpeed preference on each machine, whose watcher sets the clock and
posts the speed notice. Nothing carries a pause.

Treaty and trade offers to a human go to that human to decide: treaty
offer and reply are orders 8 and 9, a trade package (offer, accept,
reject) order 0x11; the AI's answer code runs only for computer players.
