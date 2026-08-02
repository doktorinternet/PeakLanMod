# PeakLanMod

Repository for developing and distributing a LAN multiplayer mod for PEAK.

## LAN release packaging (offline distribution)

This repository supports a LAN-focused release package that can be copied over local network shares.

Build a release package:

```sh
dotnet build -c Release -t:LanRelease -p:RunThunderPipePackAfterBuild=false
```

By default, package output is written to:

- `artifacts/release/BadHorse.PeakLanMod-LAN-v<version>/`
- `artifacts/release/BadHorse.PeakLanMod-LAN-v<version>.zip`

You can override package output root in `Config.Build.user.props`:

```xml
<LanReleaseOutputDir>D:\Releases\PEAK_LAN_MOD</LanReleaseOutputDir>
```

You can set an optional second copy destination (outside repo) for the finished zip in `Config.Build.user.props`:

```xml
<LanReleaseCopyOutputDir>Z:\LAN-Drop\PEAK</LanReleaseCopyOutputDir>
```

Optional package name override (dotnet-safe):

```xml
<LanReleasePackageId>BadHorse.PeakLanMod</LanReleasePackageId>
```

The package staging layout is:

- `mod/BepInEx/plugins/BadHorse.PeakLanMod.dll`
- `mod/BepInEx/config/BadHorse.PeakLanMod.cfg`
- `server/luxon_server.msvc.release.exe`
- `server/config.example.yml`
- `dependencies/README.md`
- `docs/INSTALL-LAN.md`
- `metadata/BUILD_INFO.txt`
- `metadata/CHECKSUMS.sha256`

See `release/INSTALL-LAN.md` for host/client setup instructions.

## LAN host IPv4 auto-detection (M1)

Milestone 1 adds optional host-side LAN IPv4 selection for `LocalServer` mode.

- `LanWorkflow.AutoDetectHostIPv4 = true` enables host-side endpoint selection when pressing `HostKey`.
- `LanWorkflow.PreferredHostIPv4` manually overrides auto-detection when you need a specific adapter.
- `LanWorkflow.AllowedHostInterfaces` narrows candidate interfaces using CSV contains-matching on interface name/description/id.
- Selected endpoint logging is sanitized (masked IPv4 + fingerprint), so diagnostics stay useful without exposing full identifiers in shared logs.

Rollback path:

- Set `LanWorkflow.AutoDetectHostIPv4 = false` to keep using the configured `Photon.LocalServerAddress` directly.

## Luxon external_address automation (M2)

Milestone 2 adds optional host-side automation that rewrites Luxon `external_address` values in `config.yml` before direct hosting.

- `LanWorkflow.AutoUpdateLuxonConfigOnHost = true` enables rewriting during host start (`HostKey`) in `LocalServer` mode.
- `LanWorkflow.LuxonConfigPath` points to the Luxon YAML file to update (relative paths resolve from the PEAK process working directory).
- The updater rewrites all matched `external_address` entries under `NameServer`, `MasterServer`, and `GameServer`, preserving existing ports.
- Default remains disabled, so manual Luxon config is unchanged unless explicitly enabled.

Rollback path:

- Set `LanWorkflow.AutoUpdateLuxonConfigOnHost = false` to return to fully manual Luxon config management.

## Local server process lifecycle control (M3)

Milestone 3 adds optional host-side local server process control with explicit ownership tracking.

- `LanWorkflow.AutoStartLocalServerOnHost = true` enables process auto-start during direct host (`HostKey`) in `LocalServer` mode.
- `LanWorkflow.LocalServerExecutablePath` sets the server executable path.
- `LanWorkflow.LocalServerWorkingDirectory` sets process working directory. Leave empty to use the executable directory.
- `LanWorkflow.LocalServerStartArguments` sets startup arguments.
- `LanWorkflow.AutoStopOwnedLocalServerOnExit = true` stops only plugin-owned server process on plugin unload/game exit.
- `LanWorkflow.ForceKillOwnedLocalServerOnExit` and `LanWorkflow.OwnedLocalServerStopTimeoutMs` control timeout and forced termination behavior.
- `LanWorkflow.AutoRetryDirectHostUntilReady = true` keeps a host request queued after one `HostKey` press and completes it automatically once Photon reaches connected+ready.

If `LocalServerWorkingDirectory` is set to a relative path and that folder does not exist under PEAK's process working directory, the launcher falls back to the executable directory and logs that fallback.

Relative `LocalServerExecutablePath` values are resolved by checking:

- current process working directory,
- `BepInEx/config` directory and its parent directories.

This supports profile-based installs where the `server/` folder is located near the profile root instead of under the PEAK game directory.

If you prefer the older behavior (manual repeated key presses while waiting for connect), set `LanWorkflow.AutoRetryDirectHostUntilReady = false`.

Ownership behavior:

- If the plugin starts the process, it is owned and eligible for stop-on-exit.
- If the process was already running externally, it is treated as unowned and never stopped by the plugin.

Rollback path:

- Set `LanWorkflow.AutoStartLocalServerOnHost = false` to return to fully manual server startup.
- Optionally set `LanWorkflow.AutoStopOwnedLocalServerOnExit = false` to disable plugin-driven shutdown.

## Local NameServer readiness gate (M4)

Milestone 4 adds an optional readiness gate that checks local NameServer reachability before direct host/join connect attempts.

- `LanWorkflow.EnableLocalServerReadinessCheck = true` enables readiness gating in `LocalServer` mode.
- `LanWorkflow.ReadinessTimeoutMs` controls maximum wait time before reporting readiness timeout.
- `LanWorkflow.ReadinessPollIntervalMs` controls probe cadence.
- In host auto-retry flow (`AutoRetryDirectHostUntilReady = true`), the host intent remains queued until readiness succeeds or timeout is reached.

Focused diagnostics:

- Logs include readiness endpoint (sanitized), protocol, elapsed milliseconds, attempts, and last probe failure reason.
- On timeout, an in-game local-server status notification is emitted to show that readiness checks failed before connect.

Rollback path:

- Set `LanWorkflow.EnableLocalServerReadinessCheck = false` to restore pre-M4 behavior.

## Release branch guidance

Using a dedicated `release` branch is optional and depends on your team workflow.

Recommendation for this project:

- Keep release branching manual and documented.
- Do not automate branch creation/switching in build targets.

Reason: build automation should be deterministic and non-destructive; automating git branch operations can surprise local development state and create hard-to-debug release mistakes.

## Template Instructions

You can remove this section after you've set up your project.

Next steps:

- Create a copy of the `Config.Build.user.props.template` file and name it `Config.Build.user.props`
  - This will automate copying your plugin assembly to `BepInEx/plugins/`
  - Configure the paths to point to your game path and your `BepInEx/plugins/`
  - Game assembly references should work if the path to the game is valid
- Search `TODO` in the whole project to see what you should configure or modify

### Thunderstore Packaging & Publishing

This template comes with Thunderstore packaging built-in, using [ThunderPipe](<https://github.com/WarperSan/ThunderPipe>).

You can build Thunderstore packages by building with release configuration:

```sh
dotnet build -c Release -v d
```

> [!NOTE]  
> You can learn about different build options with `dotnet build --help`.  
> `-c` is short for `--configuration` and `-v d` is `--verbosity detailed`.

The built package will be found at `./artifacts/thunderstore/`.

You can directly publish to Thunderstore by including `-p:PublishTS=true` in the command. See the `Config.Build.user.props.template` file for configuration instructions.

> [!TIP]  
> Make sure the local package looks fine in `./artifacts/thunderstore/` first, then publish with `dotnet build -c Release -p:PublishTS=true -v d` to avoid potential mistakes.
