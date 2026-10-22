# Finding lobbies on the LAN

The MULTIPLAYER menu's LAN page lists the lobby servers on the local
network without anyone typing an address.

## Datagrams

Both go over UDP, on the lobby's port number (10421 by default).

- Question, from a game: the 8 ASCII bytes `HGLOBBY?`.
- Answer, from a lobby, sent back to whoever asked:
  - the 8 ASCII bytes `HGLOBBY!`
  - protocol version, 16 bits big endian
  - the lobby's TCP port, 16 bits big endian
  - players in the lobby now, 16 bits big endian
  - the lobby's name in UTF-8 (the machine name), up to 64 characters, to the end of the datagram

The address to connect to is the answer's source address with the port
from the answer.

## Asking

The game asks on every IPv4 network the machine is on, real LAN or VPN:

- Broadcasts, every ask (about every two seconds while the page is open):
  255.255.255.255 and the broadcast address of each network (some routers
  and Wi-Fi drivers pass one and not the other).
- Every fifth ask, starting with the first, it also asks directly:
  - each address of every tunnel network of a /22 or smaller (WireGuard,
    ZeroTier, OpenVPN and the like, which mostly carry no broadcasts). A
    network counts as a tunnel when its interface has no hardware address
    or is of a tunnel or PPP type. A plain LAN is left to its broadcast.
  - every Tailscale peer, from `tailscale status --json` (Tailscale gives
    each machine a /32, so there is no subnet to sweep). Offline peers are
    asked too, since the flag lags. No Tailscale, no peers.
  - each host in `lanHosts` in deploy/config.json, by address or name.

A lobby heard in neither of the last two of those wider rounds (about
twenty seconds) drops off the list. A lobby of another protocol version is
listed with a note. Asking runs on a worker thread, so a slow name lookup
or Tailscale call never holds up the screen.

Windows reports a datagram that found nobody listening as an error on the
next receive; both ends ignore that error rather than stopping.

## Answering

The lobby server opens the UDP port beside its TCP port. If the UDP port is
taken it says so at startup and goes on without discovery; players can
still type its address on the PRIVATE LOBBY page. A firewall on the lobby's
machine has to let UDP 10421 in as well as TCP 10421, on the VPN's
interface too (Tailscale's ACLs must allow the port between the machines).
