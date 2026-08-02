# PEAK LAN Mod Offline LAN Installation

This guide installs a full LAN package without Thunderstore.

## Required versions

- PEAK: see package changelog for tested version.
- BepInEx Pack for PEAK: `5.4.75301`.
- Windows host/client machines on the same LAN.

## Package layout

- `mod/BepInEx/plugins/BadHorse.PeakLanMod.dll`
- `mod/BepInEx/config/BadHorse.PeakLanMod.cfg`
- `server/luxon_server.msvc.release.exe`
- `server/config.example.yml`
- `dependencies/README.md`
- `dependencies/BepInEx/`

## Dependencies

- Place any additional required dependency mods under `dependencies/` before distribution.
- Install dependency mods according to their own instructions.
- Typical destination is PEAK `BepInEx/plugins/` and `BepInEx/config/`.

## Host setup

1. Install BepInEx for PEAK found in the `dependencies/` folder. (TODO ADD bepinex install instructions in dependencies folder)
2. Copy package `mod/BepInEx/` into your PEAK `BepInEx/` folder. Make sure the mod dll and config files are present in `BepInEx/plugins/` and `BepInEx/config/`.
3. Copy package `server/` into your PEAK installation folder.
4. In the mod config file, set:
  - `Mode = LocalServer`
   - `LocalServerAddress` to host LAN IP (or `127.0.0.1` if server and host game are same machine).
   - `LocalServerPort` and `LocalServerProtocol` to match server config.
   - Optional host automation:
     - `LanWorkflow.AutoDetectHostIPv4 = true`
     - `LanWorkflow.PreferredHostIPv4 = <specific host IPv4>` (optional manual override)
     - `LanWorkflow.AllowedHostInterfaces = Ethernet,Wi-Fi` (optional CSV filter)
   - Optional host server config automation:
     - `LanWorkflow.AutoUpdateLuxonConfigOnHost = true`
     - `LanWorkflow.LuxonConfigPath = server/config.yml` (or an absolute path)
   - Optional host server process control:
     - `LanWorkflow.AutoStartLocalServerOnHost = true`
     - `LanWorkflow.LocalServerExecutablePath = server/luxon_server.msvc.release.exe`
    - `LanWorkflow.LocalServerWorkingDirectory =` (leave empty to use executable directory)
     - `LanWorkflow.LocalServerStartArguments = config.yml`
     - `LanWorkflow.AutoStopOwnedLocalServerOnExit = true`
     - `LanWorkflow.ForceKillOwnedLocalServerOnExit = true`
     - `LanWorkflow.OwnedLocalServerStopTimeoutMs = 2000`
5. Install any other packaged dependency mods from `dependencies/`.
6. Start PEAK and verify overlay status indicates that the mod is loaded.

## Client setup

1. Install BepInEx for PEAK.
2. Copy package `mod/bepinex/plugins/*` into your PEAK `BepInEx/plugins/`.
3. Copy package `mod/bepinex/config/*` into your PEAK `BepInEx/config/`.
4. Install any packaged dependency mods from `dependencies/`.
5. In the config file, set same room name and same local server endpoint values as host.
6. Start PEAK and verify overlay status indicates that the mod is loaded.

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
- Host key pressed but local server did not auto-start:
  verify `LanWorkflow.AutoStartLocalServerOnHost = true`, confirm `LanWorkflow.LocalServerExecutablePath` points to the deployed executable, and either leave `LanWorkflow.LocalServerWorkingDirectory` empty or set it to a valid absolute folder.
- Airport loaded but not in room:
  room creation/join did not complete. Check host/client callbacks and disconnect logs.
