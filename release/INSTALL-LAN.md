# PEAK LAN Mod Offline Installation

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

## Client setup

This section is required if you intend to play the game using the mod. If you only intend to run a server, go straight to the `Server Setup` section

1. Install BepInEx for PEAK found in the `dependencies/` folder. (TODO ADD bepinex install instructions in dependencies folder).
2. Copy the content of `mod/BepInEx/` into your PEAK `BepInEx/` folder. Make sure the mod dll and config files are present in `BepInEx/plugins/` and `BepInEx/config/`.
3. The config file should be plug and play if all instructions are been followed. If you use any other paths than recommended, you likely have to include those changes in the config file. You might also want to set the `LocalServerAddress` to the IP of your primary network adapter to avoid some warnings.
4. Install any packaged dependency mods from `dependencies/`. 
5. Start PEAK and verify that the mod is loaded by the new main menu server list.

## Host setup 

This section is optional, but without it you can only join open lobbies. To host your own lobbies, you need to run or have access to a server instance.
The server is a Luxon server downloaded from the Luxon github page. You can find checksums for each executable in the file `server/luxon-sha254.txt`.

To run the server locally on your machine, follow these instructions:

1. Copy the folder `server/` into your PEAK root installation folder, where `PEAK.exe` lives.
2. The default mod configuration setting `WorkflowMode = AutoSetup` will attempt to automatically find your IP and apply it wherever required the first time you host a lobby.
3. Add in- & outgoing firewall exceptions for the luxon executable.

## Server setup

As with the host setup, this is optional but aimed towards users who want to run a server independently of the PEAK instance, either locally on your machine or some other machine.

1. Copy the folder `server/` into wherever you intend to run the server from.
2. Adjust each entry of `external_address` in `config.yml` to the host machine IP. 
3. Run the luxon executable with the config file as the only argument.
4. PEAK instances that want to host on your server should turn off automatic host IP detection and set `LocalServerAddress` to your server IP.

## Troubleshooting

- `OnCreateRoomFailed ... Unsupported operation 227`:
  `LocalServerPort` is probably incorrectly defined. Use the default in most, if not all, cases.
- `ExceptionOnConnect` or `ClientTimeout`:
  endpoint/port/protocol mismatch, server not running, or firewall block.
- Host LAN IPv4 detection selected an unexpected adapter:
  set `AutoDetectHostIPv4 = false` and configure `LocalServerAddress` manually, or keep auto-detect enabled and set `AllowedHostInterfaces` to filter interfaces.
- Luxon config was not rewritten on host start:
  verify `AutoUpdateLuxonConfigOnHost = true`, ensure `LuxonConfigPath` points to the active `config.yml`, and confirm `LocalServerAddress` is a non-loopback IPv4.
- Host key pressed but local server did not auto-start:
  verify `AutoStartLocalServerOnHost = true`, confirm `LocalServerExecutablePath` points to the deployed executable, and either leave `LanWorkflow.LocalServerWorkingDirectory` empty or set it to a valid absolute folder.
- Host/join waits and then reports local server readiness timeout:
  verify `EnableLocalServerReadinessCheck = true`, confirm `LocalServerAddress`, `LocalServerPort`, and `LocalServerProtocol` match the active server, and check firewall rules for the selected protocol/port.
- No LAN sessions discovered while host is in-room:
  verify `DiscoveryEnabled = true` on both machines, ensure both use the same `DiscoveryUdpPort`, allow inbound/outbound UDP on that port in host/client firewalls.
- Sessions appear but are marked incompatible:
  compare host/client `ProtocolVersion` and, when `RequireVersionMatch = true`, verify PEAK game version and mod version match exactly.
- UI server list panel is not visible:
  verify `DiscoveryEnabled = true` in the active `BepInEx/config/BadHorse.PeakLanMod.cfg` file (not only in the template file). Ensure the mod DLL is correctly installed in the proper BepInEx folder, and that BepInEx is actually loaded when launching PEAK.
- Airport loaded but not in room:
  room creation/join did not complete. Check host/client callbacks and disconnect logs.