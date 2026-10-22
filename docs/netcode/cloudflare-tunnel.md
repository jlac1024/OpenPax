# A public lobby behind a Cloudflare tunnel

A Cloudflare tunnel carries web traffic (HTTP and WebSockets) from a public
hostname to a machine that has no open ports. Plain TCP does not reach
players through it unless every player runs cloudflared too, so the lobby
also speaks WebSocket.

## How it works

- The lobby server listens on one port (10421 by default) for both kinds of
  arrival. It looks at the first four bytes: `GET ` is an HTTP request, which
  it answers as a WebSocket opening (RFC 6455); anything else is a plain TCP
  player or relay end, as on a LAN.
- Over a WebSocket the game sends exactly the same frames as over TCP, cut
  into binary WebSocket messages. Message boundaries carry no meaning; the
  reader takes the bytes as a stream.
- Relay ends (the game traffic once a room starts) open their own WebSocket
  to the same address, so the whole game goes through the tunnel.
- A plain web request (a browser, a health check) gets a short text answer
  and is closed.
- Both ends ping every 20 seconds, so Cloudflare's idle timeout (about 100
  seconds) never closes a quiet lobby connection.
- WebSocket and TCP players can share a room; the relay joins any two ends.

## Addresses

The PRIVATE LOBBY page, `publicLobby` in `deploy/config.json` and `--dedicated` all take:

| Address | Means |
|---|---|
| `192.168.1.48` | TCP, port 10421 |
| `192.168.1.48:5000` | TCP, another port |
| `ws://192.168.1.48:10421` | WebSocket without TLS (testing) |
| `wss://lobby.example.com` | WebSocket with TLS, port 443: the tunnel |

`deploy/config.json` holds the public lobby's address (`publicLobby`), which
PUBLIC LOBBY connects to without asking. It is a text file to edit by hand
(comments allowed). build.sh writes it from `tools/config.json` only when it
is missing and keeps it across builds. The player's name is
remembered in the `[player]` section of openpax.toml.

## Setting up the tunnel

On the machine that runs the lobby server, with cloudflared installed and
the domain on Cloudflare:

1. `cloudflared tunnel login`
2. `cloudflared tunnel create openpax`
3. `cloudflared tunnel route dns openpax lobby.example.com`
4. A config file (`~/.cloudflared/config.yml`):

   ```yaml
   tunnel: openpax
   credentials-file: /home/USER/.cloudflared/<tunnel id>.json
   ingress:
     - hostname: lobby.example.com
       service: http://localhost:10421
     - service: http_status:404
   ```

5. `./run.sh --lobby-server` and `cloudflared tunnel run openpax`.
6. In `deploy/config.json` on each player's copy: `"publicLobby": "wss://lobby.example.com"`.

For a quick test without a domain, `cloudflared tunnel --url http://localhost:10421`
prints a `https://<words>.trycloudflare.com` address; use it as
`wss://<words>.trycloudflare.com`.

WebSockets are on by default for tunnels. Nothing in the lobby needs TLS
itself: Cloudflare ends TLS at its edge and the tunnel delivers plain HTTP.
