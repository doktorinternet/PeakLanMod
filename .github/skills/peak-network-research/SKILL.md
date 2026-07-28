---
name: peak-network-research
description: Investigate or modify PEAK multiplayer, Photon PUN, Steam lobby discovery, ConnectionService states, room creation/joining, region selection, or offline LAN transport.
---

# PEAK network research

Use this skill for tasks involving PEAK's connection flow, Steam/Photon separation, direct connect, local Photon Server, room state, callbacks, disconnects, or matchmaking.

## Start with the verified baseline

Read:

1. `.github/copilot-instructions.md`
2. `docs/research/current-network-findings.md`
3. relevant source under `src/`
4. the smallest relevant host/client logs supplied for the task

Do not discard or rewrite the working custom-Photon-Cloud path while investigating local-server support.

## Model of the current flow

Normal host:

1. Steam lobby creation completes.
2. PEAK creates a random Photon room name.
3. `HostState.RoomName` is set.
4. Airport loads.
5. `NetworkConnector` creates the Photon room.

Normal client:

1. Steam lobby data supplies Photon region and current scene.
2. A Steam message supplies the Photon room name.
3. `JoinSpecificRoomState` receives room and region.
4. The selected scene loads.
5. `NetworkConnector` joins the Photon room.

Direct prototype:

- F6/configured host sets `HostState` and loads Airport.
- F7/configured client sets `JoinSpecificRoomState` and loads Airport.
- Custom Photon Cloud App IDs avoid PEAK's official server-side rejection.
- A two-machine match has been proven to load successfully.

## Investigation protocol

1. State one testable hypothesis.
2. Identify the earliest observable point that distinguishes it.
3. Add minimal instrumentation before changing behavior.
4. Build.
5. Ask for or inspect host and client logs from the same attempt.
6. Compare timestamps and find the first divergence.
7. Change one variable only.
8. Preserve a rollback path and the known-good baseline.

Prioritize callbacks over frame-polled state when precise ordering matters:

- `OnConnectedToMaster`
- `OnCreatedRoom`
- `OnJoinedRoom`
- `OnJoinRoomFailed`
- `OnDisconnected`
- player enter/leave callbacks using `Photon.Realtime.Player`

## Local-server work

When implementing local Photon Server support:

- Do not change host/join state semantics unless required.
- First redirect the proven PUN connection settings.
- Keep a config switch between custom Photon Cloud and local server.
- Log server address, protocol and port, but sanitize addresses in committed documentation.
- Disable or isolate Photon Voice initially; voice is not required for an in-person LAN.
- Test in this order:
  1. host against `127.0.0.1`,
  2. second client on the same machine if supported,
  3. second machine over LAN,
  4. WAN physically disconnected,
  5. complete Airport-to-match gameplay.

## Do not

- invent PEAK method signatures,
- assume a Photon Server version is compatible without a connection test,
- expose proprietary assemblies or decompiled source,
- treat App IDs as an offline solution,
- bypass all kick/validation behavior permanently merely to make a test pass.
