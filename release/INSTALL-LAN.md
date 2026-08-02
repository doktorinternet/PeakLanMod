# PEAK LAN Mod Offline LAN Installation

This guide installs a full LAN package without Thunderstore.

## Required versions

- PEAK: see package changelog for tested version.
- BepInEx Pack for PEAK: `5.4.75301`.
- Windows host/client machines on the same LAN.

## Package layout

- `mod/bepinex/plugins/BadHorse.PeakLanMod.dll`
- `mod/bepinex/config/BadHorse.PeakLanMod.cfg`
- `server/luxon_server.msvc.release.exe`
- `server/config.example.yml`
- `dependencies/README.md`

## Dependencies

- Place any additional required dependency mods under `dependencies/` before distribution.
- Install dependency mods according to their own instructions.
- Typical destination is PEAK `BepInEx/plugins/` and `BepInEx/config/`.

## Host setup

1. Install BepInEx for PEAK.
2. Copy package `mod/bepinex/plugins/*` into your PEAK `BepInEx/plugins/`.
3. Copy package `mod/bepinex/config/*` into your PEAK `BepInEx/config/`.
4. In the config file, set:
  - `Mode = LocalServer`
   - `LocalServerAddress` to host LAN IP (or `127.0.0.1` if server and host game are same machine).
   - `LocalServerPort` and `LocalServerProtocol` to match server config.
   - Optional M1 host automation:
     - `LanWorkflow.AutoDetectHostIPv4 = true`
     - `LanWorkflow.PreferredHostIPv4 = <specific host IPv4>` (optional manual override)
     - `LanWorkflow.AllowedHostInterfaces = Ethernet,Wi-Fi` (optional CSV filter)
   - Optional M2 Luxon config automation:
     - `LanWorkflow.AutoUpdateLuxonConfigOnHost = true`
     - `LanWorkflow.LuxonConfigPath = server/config.yml` (or an absolute path)
5. Install any packaged dependency mods from `dependencies/`.
6. Start the LAN server from package `server/`.
7. Start PEAK and verify overlay status indicates server detected.

## Client setup

1. Install BepInEx for PEAK.
2. Copy package `mod/bepinex/plugins/*` into your PEAK `BepInEx/plugins/`.
3. Copy package `mod/bepinex/config/*` into your PEAK `BepInEx/config/`.
4. Install any packaged dependency mods from `dependencies/`.
5. In the config file, set same room name and same local server endpoint values as host.
6. Start PEAK and verify server detected.

## Quick runtime test

1. Host presses `F6`.
2. Client presses `F7`.
3. Confirm both reach same room and player count increases to 2.

## Troubleshooting

- `OnCreateRoomFailed ... Unsupported operation 227`:
  server endpoint is not handling room operations.
- `ExceptionOnConnect` or `ClientTimeout`:
  endpoint/port/protocol mismatch, server not running, or firewall block.
- Host LAN IPv4 detection selected an unexpected adapter:
  set `LanWorkflow.PreferredHostIPv4` for manual override, or set `LanWorkflow.AllowedHostInterfaces` to filter interfaces.
- Luxon config was not rewritten on host start:
  verify `LanWorkflow.AutoUpdateLuxonConfigOnHost = true`, ensure `LanWorkflow.LuxonConfigPath` points to the active `config.yml`, and confirm `Photon.LocalServerAddress` is a non-loopback IPv4.
- Airport loaded but not in room:
  room creation/join did not complete. Check host/client callbacks and disconnect logs.
