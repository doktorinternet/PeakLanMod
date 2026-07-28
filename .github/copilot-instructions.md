# PEAK LAN Mod repository instructions

## Project purpose

This repository develops a BepInEx mod for PEAK that replaces Steam-lobby discovery and, ultimately, Photon Cloud with offline LAN multiplayer.

The current verified baseline can direct-host and direct-join a shared Photon room through a custom Photon Cloud application. Both players can load into Airport and continue into a match. Preserve this baseline while implementing offline LAN support.

## Technology and runtime

- Language: C#
- Mod loader: BepInEx 5 for Unity Mono
- Patching: Harmony/HarmonyX
- Game networking: Photon PUN / Photon Realtime, with separate Photon Voice integration
- Project target: `netstandard2.1`
- Project template/build SDK: .NET SDK 10 or later
- Runtime platform: Windows
- PEAK and its assemblies are proprietary local dependencies and are not stored in this repository.

## Build and validation

- Build from the repository root with `dotnet build`.
- `Config.Build.user.props` is machine-local and points to the local PEAK/BepInEx installation. Never commit it.
- A cloud agent may not have PEAK's proprietary assemblies and may be unable to compile. Do not fake a successful build, replace game references with invented stubs, or weaken the project merely to make an unavailable environment pass.
- Clearly distinguish:
  - statically reviewed,
  - compiled locally,
  - tested in one PEAK instance,
  - tested with two machines,
  - tested with internet physically disconnected.
- Never claim runtime validation without corresponding logs or explicit user confirmation.

## Safety and repository hygiene

Never commit or reproduce:

- `Assembly-CSharp.dll`, Photon/Unity game DLLs, or other PEAK binaries
- exported/decompiled PEAK source trees
- PEAK assets
- real Photon App IDs in source, examples, logs, or documentation
- personal Steam IDs, usernames, auth tickets, IP addresses, or other identifiers
- generated BepInEx profile contents

Use fingerprints or placeholders for identifiers in logs and documentation.

## Engineering rules

- Make one hypothesis-driven behavioral change at a time.
- Preserve the working direct Photon Cloud path as a diagnostic baseline.
- Prefer small Harmony prefixes/postfixes over transpilers.
- Do not patch Photon internals when PEAK-level state or configuration can be changed instead.
- Guard experimental behavior with explicit configuration flags.
- Patch exact method overloads and exact parameter types.
- PEAK defines its own `Player` type. Alias Photon players explicitly:
  `using PhotonPlayer = Photon.Realtime.Player;`
- Avoid broad exception swallowing. Log enough context to diagnose failures.
- Do not log complete App IDs, Steam IDs, auth values, or tickets.
- Keep normal, unmodded PEAK behavior unchanged when direct/LAN mode is disabled.
- Do not remove diagnostics until the relevant milestone is proven stable.

## Known architecture

- `NetworkingUtilities.ConnectToNetwork()` configures rates, version, nickname and auth, then calls `PhotonNetwork.ConnectUsingSettings()`.
- Authentication uses `AuthType=None`; user ID falls back to a persistent local GUID when Steam is unavailable.
- `HostState.RoomName` leads `NetworkConnector` to call `PhotonNetwork.CreateRoom(...)`.
- `JoinSpecificRoomState.RoomName` and `RegionToJoin` lead it to call `PhotonNetwork.JoinRoom(...)`.
- The normal Steam flow provides discovery, region, current scene, and room-name transfer.
- Steam is not required for the proven direct Photon gameplay path.
- PEAK's official Photon application rejected direct clients through server-side behavior. A custom Photon Cloud application succeeded.
- The next major objective is to point the same working PUN flow at a compatible local Photon server without internet.

Read `docs/research/current-network-findings.md` before changing connection flow.
