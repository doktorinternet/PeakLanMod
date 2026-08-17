# PEAK LAN Mod Manual Windows Installation

This guide assists in installing a mod which modifies PEAK to not use the default Steam and Photon stack which is used in vanilla online PEAK. This allows for playing PEAK multiplayer on LAN.

## Client setup

This section is required if you intend to play the game using the mod. If you only intend to run a server, go straight to the `Server Setup` section.

0. BepInEx for PEAK is required for the mod to work. If you do not currently use any other mods for PEAK, you likely do not have BepInEx and need to install that first. Find the instructions for that in the `dependencies/BepInEx` folder. Go install that now.

1. Copy the content of `mod/BepInEx/plugins` into your PEAK `BepInEx/plugins` folder.
2. Launch PEAK once to let BepInEx generate `BepInEx/config/BadHorse.PeakLanMod.cfg`, then close PEAK.
3. The generated config should be near plug-and-play for default layout. If you use any other paths than recommended in this guide, update the generated config accordingly. You might also want to set `LanServerAddress` to the IP of your primary network adapter to avoid warnings.
4. Install any packaged dependency mods from `dependencies/`. 
5. Start PEAK and verify that the mod is correctly loaded by seeing that there is now a new main menu server list visible.

## Host setup

This section is optional, but without it you can only join open lobbies. To host your own lobbies, you need to either run or have access to a server instance.
The included server is a Luxon server downloaded from the Luxon github page. You can find checksums for each executable in the file `server/luxon-sha254.txt`. This server software is the only one I have tested and verified to work for PEAK, but there may be other options available. If you want to tinker with it, I'm not going to stop you.

To run the ***Luxon*** server locally on your machine, follow these instructions:

1. Copy the folder `server/` into your PEAK root installation folder, where `PEAK.exe` lives.
2. The default mod configuration setting `WorkflowMode = AutoSetup` will attempt to automatically find your IP and apply it wherever required the first time you host a lobby.
3. Add in- & outgoing firewall exceptions for the Luxon executable when requested, or manually in case there's no request or you fail to accept it.

You should be able to run this in PowerShell (might require elevated permissions, i.e. "Run as Administrator"):

```
New-NetFirewallRule
     -DisplayName "Luxon Photon UDP" `
     -Direction Inbound `
     -Action Allow `
     -Protocol UDP `
     -LocalPort 5055,5056,5058 `
     -Profile Private
```

## Server setup

As with the host setup, this is optional and aimed towards users who want to run a server independently of the PEAK instance, either locally on your machine or some other machine on the local network.

1. Copy the folder `server/` into wherever you intend to run the server from.
2. Adjust each entry of `external_address` in `config.yml` to the host machine IP. 
3. Run the Luxon executable with the config file as the only argument.
4. PEAK instances that want to host on your server should turn off automatic host IP detection and set `Hosting.LanServerAddress` to your server IP.

## Supported mods

For an augmented PEAK experience, I've included some other optional mods in this Internyet-tailored package. They can be found in `supported_mods/`. They're not very thoroughly tested, but I've at least once installed them and successfully hosted and joined lobbies. 

## Required versions

- PEAK: see package changelog for tested version.
- BepInEx Pack for PEAK: `5.4.75301`.

## Package layout

- `mod/BepInEx/plugins/BadHorse.PeakLanMod.dll`
- `server/luxon_server.msvc.release.exe`
- `server/config.yml`
- `dependencies/README.md`
- `dependencies/BepInEx/`

## Troubleshooting

- `OnCreateRoomFailed ... Unsupported operation 227`:
  `LanServerPort` is probably incorrectly defined. Use the default in most, if not all, cases.
- `ExceptionOnConnect` or `ClientTimeout`:
  endpoint/port/protocol mismatch, server not running, or firewall block.
- Host LAN IPv4 detection selected an unexpected adapter:
  set `AutoDetectHostIPv4 = false` and configure `LanServerAddress` manually, or keep auto-detect enabled and set `AllowedHostInterfaces` to filter interfaces.
- Luxon config was not rewritten on host start:
  verify `AutoUpdateLuxonConfigOnHost = true`, ensure `LuxonConfigPath` points to the active `config.yml`, and confirm `LanServerAddress` is a non-loopback IPv4.
- Host key pressed but local server did not auto-start:
  verify `AutoStartLanServerOnHost = true`, confirm `LanServerExecutablePath` points to the deployed executable, and either leave `LanWorkflow.LanServerWorkingDirectory` empty or set it to a valid absolute folder.
- Host/join waits and then reports local server readiness timeout:
  verify `EnableLanServerReadinessCheck = true`, confirm `LanServerAddress`, `LanServerPort`, and `LanServerProtocol` match the active server, and check firewall rules for the selected protocol/port.
- No LAN sessions discovered while host is in-room:
  verify `DiscoveryEnabled = true` on both machines, ensure both use the same `DiscoveryUdpPort`, allow inbound/outbound UDP on that port in host/client firewalls.
- Sessions appear but are marked incompatible:
  compare host/client `ProtocolVersion` and, when `RequireVersionMatch = true`, verify PEAK game version and mod version match exactly.
- Structured error label shows `NameServerUnreachable`, `MasterServerRedirectFailed`, or `GameServerRedirectFailed`:
  set `EnableStructuredErrorMapping = true` to expose deterministic classification, then validate endpoint/protocol config and local server/firewall reachability from the same machine where the error is shown.
- Structured error label shows `LuxonNotRunning`:
  verify local server executable path/working directory and whether `AutoStartLanServerOnHost` is enabled when you expect plugin-managed startup.
- UI server list panel is not visible:
  verify `DiscoveryEnabled = true` in the active `BepInEx/config/BadHorse.PeakLanMod.cfg` file (not only in the template file). Ensure the mod DLL is correctly installed in the proper BepInEx folder, and that BepInEx is actually loaded when launching PEAK.
- Airport loaded but not in room:
  room creation/join did not complete. Check host/client callbacks and disconnect logs.