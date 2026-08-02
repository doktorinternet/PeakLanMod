# Manual two-machine test checklist

## Record before testing

- PEAK version:
- Mod commit:
- Host machine:
- Client machine:
- Connection mode:
- Photon App fingerprint or local server build:
- Region/server:
- Room name fingerprint:
- Internet physically disconnected: yes/no

## Baseline startup

- [ ] Both plugins load.
- [ ] Both machines use the expected configuration.
- [ ] Host reaches the expected master/local server state.
- [ ] Client reaches the expected master/local server state.

## M5 discovery checks (when `LanWorkflow.DiscoveryEnabled = true`)

- [ ] Host and client use the same `LanWorkflow.DiscoveryUdpPort`.
- [ ] Host reaches room-master state and continues broadcasting session announcements.
- [ ] Client receives at least one valid discovery announcement.
- [ ] Duplicate announcements update one session entry (no duplicate rows per `server_instance_id + room_name`).
- [ ] Session expires from client store after `LanWorkflow.DiscoveryEntryTtlMs` when host stops broadcasting.
- [ ] Incompatible protocol version is classified as `IncompatibleProtocolVersion`.
- [ ] Incompatible game version is classified as `IncompatibleGameVersion` when `LanWorkflow.RequireVersionMatch = true`.
- [ ] Incompatible mod version is classified as `IncompatibleModVersion` when `LanWorkflow.RequireVersionMatch = true`.

## Host

- [ ] Trigger host once.
- [ ] `HostState` contains expected room name.
- [ ] `OnCreatedRoom` fires.
- [ ] `OnJoinedRoom` fires as actor 1/master.
- [ ] Airport loads.

## Client

- [ ] Trigger join once after host is ready.
- [ ] `JoinSpecificRoomState` contains expected room and region/server mode.
- [ ] `OnJoinedRoom` fires as actor 2/non-master.
- [ ] No immediate disconnect.
- [ ] Airport loads.
- [ ] Both players see each other.

## Gameplay

- [ ] Start a match.
- [ ] Both load the same scene.
- [ ] Movement synchronizes.
- [ ] Items synchronize.
- [ ] Stamina and damage synchronize.
- [ ] Death/revival synchronize.
- [ ] Match completion works.

## Failure capture

Save host and client logs from the same attempt. Record:

- first divergent timestamp,
- callback/disconnect cause,
- room-operation return code/message,
- relevant exception,
- whether a retry used the same room instance.

## Latest verified run

- Date: 2026-08-02
- Validation type: physically offline LAN two-machine runtime
- Host role: gaming PC
- Client role: office laptop
- Accounts: separate Steam accounts
- Connection mode: LocalServer
- Result: pass
