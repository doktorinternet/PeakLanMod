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

- `mod/bepinex/plugins/BadHorse.PeakLanMod.dll`
- `mod/bepinex/config/BadHorse.PeakLanMod.cfg`
- `server/luxon_server.msvc.release.exe`
- `server/config.example.yml`
- `dependencies/README.md`
- `docs/INSTALL-LAN.md`
- `metadata/BUILD_INFO.txt`
- `metadata/CHECKSUMS.sha256`

See `release/INSTALL-LAN.md` for host/client setup instructions.

## LAN host IPv4 auto-detection (M1)

Milestone 1 adds optional host-side LAN IPv4 selection for `LocalPhotonServer` mode.

- `LanWorkflow.AutoDetectHostIPv4 = true` enables host-side endpoint selection when pressing `HostKey`.
- `LanWorkflow.PreferredHostIPv4` manually overrides auto-detection when you need a specific adapter.
- `LanWorkflow.AllowedHostInterfaces` narrows candidate interfaces using CSV contains-matching on interface name/description/id.
- Selected endpoint logging is sanitized (masked IPv4 + fingerprint), so diagnostics stay useful without exposing full identifiers in shared logs.

Rollback path:

- Set `LanWorkflow.AutoDetectHostIPv4 = false` to keep using the configured `Photon.LocalServerAddress` directly.

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
