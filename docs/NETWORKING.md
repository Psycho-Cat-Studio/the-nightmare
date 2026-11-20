# Networking: LAN now, online services later

Decision recorded on 2026-11-18. This is the handover for future multiplayer work; setup commands
are in [BUILDING.md](BUILDING.md#playing-together), and implementation structure is in
[ARCHITECTURE.md](ARCHITECTURE.md#networking-mirror).

## Current decision

**Keep Mirror and the existing direct-connect stack for now.** The immediate goal is two friends
playing on the same Wi-Fi. That does not require a relay, cloud account, internet matchmaking or
a migration to Netcode for GameObjects. Validate a complete LAN match before expanding the stack.

No online provider has been selected or configured. Later, we can add services around Mirror or
separately evaluate a migration to Unity's official networking stack.

## What runs today

| Layer | Implementation |
|---|---|
| Gameplay synchronization | Mirror: Commands, SyncVars, RPCs and network spawning |
| Session topology | Host–client: one player's process runs both server and local client |
| Gameplay transport | KCP over UDP, default port **7777** |
| Room discovery | Local UDP broadcast, port **47777**, through `LanDiscovery` |
| Connection setup | LAN room list or an address entered in Host IP |
| Internet lobby / relay | Not configured |

`CustomNetworkManager` handles hosting, joining, status messages and connection rejection.
The host runs damage, enemy AI and match phases; this does not imply complete anti-cheat validation.
Server-side hit validation remains a separate roadmap item. Joining after a match starts is blocked.
There is no implemented host migration: adding a relay alone will not keep a match running when
the host leaves.

## Playing on one Wi-Fi

1. Use matching builds from the same revision on both devices.
2. Host: Play → enter a room name → Create Room.
3. Friend: select the room in the LAN list. If discovery fails, enter the host's local address,
   for example `192.168.1.25:7777`, in Host IP and press Connect. The host address is available
   through the connection overlay (F1), lobby and pause menu.
4. Join before starting the match.

No router port forwarding is needed for LAN play. Allow gameplay UDP 7777 through the host
firewall and discovery UDP 47777 on listening devices. Avoid guest Wi-Fi/client isolation, which
can prevent devices from communicating even on the same SSID. Different subnets can prevent
broadcast discovery; direct IP can still work if routing and firewall rules allow it.

LAN acceptance: test movement, shooting, damage, traps/occupied cells, candle progress, all three
rooms, victory/loss and leaving the session on two real devices. Same-machine Editor/player tests
are useful but do not establish that Wi-Fi discovery and firewalls work.

## How this differs from old UNet

The old project also used host–client. UNet HLAPI synchronized the game; Unity Matchmaker listed
internet rooms, and Unity Relay forwarded packets through NAT. The relay did not run our game
simulation. Git history contains the old `ListMatches` and `JoinMatch` calls.

Mirror is a community-developed descendant of UNet HLAPI, not a replacement for Unity's cloud
services. The old UNet Relay/Matchmaker services were shut down on **2025-07-07**.
[Unity shutdown notice](https://discussions.unity.com/t/unity-unet-service-final-shutdown-faq/1611981).

Internet play currently needs a reachable host address and UDP 7777 port forwarding, or a shared
VPN network with direct connection to the host's VPN address. CGNAT can prevent ordinary inbound
port forwarding; it does not prevent playing together on the local network. The LAN browser is
not a public internet directory and should not be assumed to discover hosts across a VPN.

## Future options

| Path | What it changes and what needs checking |
|---|---|
| Mirror + Epic Online Services | Third-party `EpicOnlineTransport` offers transport and lobby integration. Prototype compatibility with our Mirror/Unity versions and target platforms first. |
| Mirror + Edgegap | `EdgegapKcpTransport` already exists under the vendored Mirror transports. Configure relay allocation and session management/backend; included code alone does not activate a hosted service. |
| Mirror + Unity services | Keep gameplay synchronization and integrate Unity Lobby/Relay with a compatible UTP transport adapter. The old official Mirror sample is deprecated and not maintained beyond Unity 2020.3; it is a reference, not a ready integration for this project. |
| NGO + Unity services | Migrate synchronization to Netcode for GameObjects and use the official Multiplayer Services integration. This requires porting network components, RPCs, SyncVars, spawning/ownership and prefab configuration. |

Unity does provide a current official stack: **Netcode for GameObjects** for synchronization,
**Unity Transport** for transport, and **Multiplayer Services SDK** (`com.unity.services.multiplayer`)
for Lobby, Relay and Matchmaker. The latter combines services through a sessions API; deprecation
of the older individual SDK packages does not mean those cloud services were shut down.
For named rooms and friend joining, lobby/session discovery plus relay is sufficient; automatic
player matching is optional.

Sources: [Unity Multiplayer Services](https://docs.unity.com/en-us/mps-sdk/index),
[SDK consolidation](https://docs.unity.com/en-us/mps-sdk/subservices-toc),
[deprecated Mirror–Relay sample](https://github.com/Unity-Technologies/unity-relay-mirror-sample),
[EpicOnlineTransport](https://github.com/FakeByte/EpicOnlineTransport),
[Mirror Edgegap transport](https://mirror-networking.gitbook.io/docs/manual/transports/edgegap-transports).

## Before choosing an online integration

- Confirm distribution targets: Steam-only versus PC/mobile cross-play. Do not choose a
  Steam-dependent solution for all platforms without proving the mobile connection path.
- Prototype create/find/join on two different internet connections, including a restrictive NAT,
  with host on SteamOS/Linux and a target phone if mobile is in scope. Test both host directions.
- Check provider pricing/limits, SDK maintenance, authentication requirements and required native
  plugins against current documentation. No cost or compatibility assumptions are locked in here.
- Connect the existing room UI to online sessions and adapt connection setup; keep LAN/direct
  connection available. Treat late join, reconnect and host migration as separate features.
- Keep changes focused and preserve the existing gameplay synchronization unless a measured
  limitation justifies migration. Do not migrate to NGO solely to make same-Wi-Fi play work.
