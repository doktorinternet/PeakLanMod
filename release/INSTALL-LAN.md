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
- Typical mod destination is PEAK `BepInEx/plugins/` and `BepInEx/config/`.

## Client setup (required)

1. Install BepInEx for PEAK found in the `dependencies/` folder. (TODO ADD bepinex install instructions in dependencies folder).
2. Copy the content of `mod/BepInEx/` into your PEAK `BepInEx/` folder. Make sure the mod dll and config files are present in `BepInEx/plugins/` and `BepInEx/config/`.
3. Install any packaged dependency mods from `dependencies/`. 
4. Start PEAK and verify that the mod is loaded by the new main menu server list .

## Host setup 

This section is optional. It installs a Luxon server downloaded from the luxon github page. The 

1. Copy the folder `server/` into your PEAK root installation folder, where `PEAK.exe` lives.
2. In the mod config file, set:
  - `Mode = LocalServer`
  - `LocalServerAddress` fallback endpoint (typically leave `127.0.0.1`; M6 join-selected can auto-apply discovered endpoint).
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
     - Optional readiness gate before connect:
       - `LanWorkflow.EnableLocalServerReadinessCheck = true`
       - `LanWorkflow.ReadinessTimeoutMs = 5000`
       - `LanWorkflow.ReadinessPollIntervalMs = 250`
     - Optional LAN discovery transport/session store (M5):
       - `LanWorkflow.DiscoveryEnabled = true`
       - `LanWorkflow.DiscoveryUdpPort = 47777`
       - `LanWorkflow.DiscoveryBroadcastIntervalMs = 1000`
       - `LanWorkflow.DiscoveryEntryTtlMs = 5000`
       - `LanWorkflow.ProtocolVersion = 1`
       - `LanWorkflow.RequireVersionMatch = true`
     - Optional LAN UI actions and session/status overlay (M6):
       - `LanWorkflow.EnableLanUiActions = true`
3. Install any other packaged dependency mods from `dependencies/`.
4. Start PEAK and verify overlay status indicates that the mod is loaded.

## Quick runtime test

1. Host presses `F6` (or uses the M6 `Host LAN` button if enabled).
2. Confirm host reaches in-room master state.
3. On client with discovery enabled, verify at least one session appears in the LAN overlay panel.
4. Client clicks a session row to select it, then clicks `Join Selected`.
5. Confirm both reach same room and player count increases to 2.

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
- Host/join waits and then reports local server readiness timeout:
  verify `LanWorkflow.EnableLocalServerReadinessCheck = true`, confirm `Photon.LocalServerAddress`, `Photon.LocalServerPort`, and `Photon.LocalServerProtocol` match the active server, and check firewall rules for the selected protocol/port.
- Expected status-text behavior:
  local-server status text notifications were removed in favor of the M6 clickable LAN session panel and focused logs.
- No LAN sessions discovered while host is in-room:
  verify `LanWorkflow.DiscoveryEnabled = true` on both machines, ensure both use the same `LanWorkflow.DiscoveryUdpPort`, allow inbound/outbound UDP on that port in host/client firewalls, and confirm the host reached room-master state (`OnJoinedRoom` as master).
- Sessions appear but are marked incompatible:
  compare host/client `LanWorkflow.ProtocolVersion` and, when `LanWorkflow.RequireVersionMatch = true`, verify PEAK game version and mod version match exactly.
- M6 panel is not visible:
  verify `LanWorkflow.EnableLanUiActions = true` and `LanWorkflow.DiscoveryEnabled = true` in the active `BepInEx/config/BadHorse.PeakLanMod.cfg` file (not only in the template file).
- M6 join-selected action does nothing:
  ensure at least one compatible discovered session row is selected, then click `Join Selected`.
- Airport loaded but not in room:
  room creation/join did not complete. Check host/client callbacks and disconnect logs.
