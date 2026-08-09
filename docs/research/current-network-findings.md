# Current PEAK network findings

_Last updated from experiments performed on 2026-07-28. Revalidate after PEAK updates._

## Verified runtime baseline

A custom BepInEx probe/direct-connect mod has successfully:

- connected two separate Steam accounts,
- used a custom Photon Cloud PUN application,
- direct-hosted a configured room without creating a Steam lobby,
- direct-joined the room by configured name and region,
- loaded both players into Airport,
- and continued into a shared match.

The successful test used the Photon-selected `ru` region. The client had to be configured to use the same region as the host.

This is an internet-dependent diagnostic baseline, not the final offline LAN solution.

## Verified PEAK connection architecture

### Photon startup

`NetworkingUtilities.ConnectToNetwork()`:

- sets Photon serialization and send rates,
- enables scene synchronization,
- derives `GameVersion` and matchmaking `AppVersion`,
- sets nickname,
- sets `AuthenticationValues`,
- calls `PhotonNetwork.ConnectUsingSettings()`.

Authentication uses `CustomAuthenticationType.None`.

User ID selection:

1. Steam ID when Steam is running and logged on,
2. an existing PlayerPrefs `UserID`,
3. otherwise a newly generated persistent GUID.

### Host

- Steam's normal flow creates a Steam lobby.
- `OnLobbyCreated` generates a random Photon room name.
- `HostState.RoomName` receives the room name.
- Airport loads.
- `NetworkConnector` sees `HostState` and calls `PhotonNetwork.CreateRoom(...)`.

Room options observed:

- `IsVisible = false`
- game-defined maximum players
- `PublishUserId = true`

### Join

The normal Steam flow supplies:

- `PhotonRegion` through Steam lobby data,
- `CurrentScene` through Steam lobby data,
- Photon room name through a separate Steam message (`RequestRoomID` / `RoomID`).

PEAK creates `JoinSpecificRoomState`, sets room and region, loads the selected scene, and `NetworkConnector` calls `PhotonNetwork.JoinRoom(...)`.

### In-room scene transition

A later `NetworkConnector.Start()` runs with `InRoomState`; it does not create another room.

## Official Photon application behavior

Direct joining through PEAK's official Photon application was rejected:

- first direct attempt briefly joined and received `DisconnectByDisconnectMessage`,
- a subsequent attempt returned room failure code `32752` with message `KICKED`.

Using a custom Photon Cloud application removed that rejection and allowed gameplay.

Inference: PEAK's official Photon application has server-side validation or plugin behavior tied to the normal official flow. The client-side direct PUN path itself is functional.

## Current goal

Replace custom Photon Cloud with a compatible Photon server reachable on the LAN:

```text
PEAK host/client
  -> local Photon server address
  -> existing HostState / JoinSpecificRoomState
  -> existing CreateRoom / JoinRoom
  -> no internet
```

Keep custom Photon Cloud available as a known-good comparison until offline LAN has passed a physically disconnected two-machine test.

## Discovery occupancy/capacity status (static)

Static update date: 2026-08-09
Validation type for this section: static analysis and compile validation only.

Observed implementation status:

- LAN discovery announcement schema now carries `current_players` and `max_players` fields.
- Host announcement values are sourced from authoritative Photon room state (`CurrentRoom.PlayerCount` and `CurrentRoom.MaxPlayers`) with unknown sentinel fallback.
- Discovered session model/state now propagates occupancy fields end-to-end.
- Server-list row and admin telemetry now render occupancy values using unknown-safe fallback display.
- Join Selected now blocks known-full sessions (`current >= max`) before attempting join.
- Simulated discovery entries now include realistic occupancy/capacity values, including near-full and full rows.

Policy decision recorded:

- No hard incompatibility gate is applied solely because discovered `max_players > 4`.
- Capacity above 4 is informational for user choice in the current rollout.

## Open questions

- Exact PEAK Photon Realtime/PUN client versions.
- Compatible self-hosted Photon Server version and licensing/distribution constraints.
- Required local server applications and ports.
- Whether Photon Voice must be disabled or can be self-hosted separately.
- Whether host migration, reconnect and join-in-progress work without Steam.
- Whether all gameplay systems remain stable through a full run.
