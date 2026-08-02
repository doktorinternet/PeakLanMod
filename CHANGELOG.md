# Changelog

TODO: You can follow this format for your changelog: <https://keepachangelog.com/en/1.1.0/>

## 2026-08-02

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
