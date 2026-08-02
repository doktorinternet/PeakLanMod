# Changelog

TODO: You can follow this format for your changelog: <https://keepachangelog.com/en/1.1.0/>

## 2026-08-02

- Confirmed Milestone 4 runtime validation: physically offline LAN test passed on two separate machines with separate Steam accounts.
- Further reduced host-connect log noise: removed per-frame no-op `OfflineMode is false` entries and added short reconnect-attempt cooldown plus periodic not-ready warning cadence.
- Reduced queued-host log spam while waiting for Photon readiness: host preflight now runs once per queued host request and repeated not-ready state logs are throttled.
- Implemented Milestone 4 local NameServer readiness gating: optional pre-connect probe/wait for direct host/join with timeout, polling controls, queued-host compatibility, and focused readiness diagnostics.
- Implemented Milestone 3 Luxon process lifecycle control: optional host auto-start with owned/unowned process detection, plus stop-only-owned behavior on plugin unload.
- Fixed M3 host auto-start path handling: `LocalServerWorkingDirectory` now defaults to empty (use executable directory), with fallback and explicit working-directory diagnostics when relative paths are invalid.
- Improved M3 executable path resolution for profile-based installs: relative `LocalServerExecutablePath` now searches from current directory plus `BepInEx/config` ancestry before requiring absolute paths.
- Improved M3 host usability: single `HostKey` press now queues host intent and auto-completes once Photon becomes connected and ready (`LanWorkflow.AutoRetryDirectHostUntilReady`, default `true`).
- Renamed mode value `LocalPhotonServer` to `LocalServer` and added startup config migration to rewrite legacy `Mode = LocalPhotonServer` entries.
- Implemented Milestone 2 Luxon config automation: optional host-side rewrite of `external_address` entries for NameServer/MasterServer/GameServer with deterministic port-preserving updates and rollback config guard.
- Implemented Milestone 1 host LAN IPv4 selection: optional host-side auto-detection with interface filtering, manual override, and sanitized endpoint diagnostics.
- Added `LanRelease` MSBuild target for offline LAN distribution staging, checksums, and zip packaging.
- Added configurable `LanReleaseOutputDir` build property for finished package output location.
- Added release assets for packaging: mod config template and LAN installation guide.
- Updated LAN package layout to use `mod/bepinex/...`, plus top-level `dependencies/` placeholder.
- Added optional `LanReleaseCopyOutputDir` for copying finished zip to any filesystem location.
- Added configurable dotnet-safe LAN package ID (`LanReleasePackageId`) with default `BadHorse.PeakLanMod`.
- Updated package root README to end-user installation instructions only (from `release/INSTALL-LAN.md`).
- Removed generated `RELEASE_NOTES_TEMPLATE.md` from LAN package output in favor of install guide and changelog.
- Renamed project and source identifiers from `PeakLanProbe` to `PeakLanMod`.
