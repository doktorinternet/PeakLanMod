# Changelog

TODO: You can follow this format for your changelog: <https://keepachangelog.com/en/1.1.0/>

## 2026-08-07

- Completed Plugin separation migration Phase 0: added compatibility service scaffolding and plugin-backed adapters for future extraction with no intended runtime behavior change.
- Release tag: `v0.5.0`.
- Milestone 8 scope correction: removed runtime mode-transition handling after confirming this mod does not switch network modes during runtime.
- Removed CustomCloud mode and related config parameters (`Photon.Mode`, `Photon.AppIdRealtime`, `Photon.AppIdVoice`).
- LocalServer is now the only supported endpoint path for both host-create and client-join LAN workflows.
- Consolidated LocalServer config surface: removed `Direct Connect.Enabled`, `Direct Connect.Region`, `LanWorkflow.PreferredHostIPv4`, and `LanWorkflow.LuxonConfigPath`.
- LAN UI overlay actions are now always enabled in LocalServer mode; removed `LanWorkflow.EnableLanUiActions` toggle.
- `LanWorkflow.AutoSkipPhotonFailureDialog` naming is now canonical (legacy `...InLocalServer` key is no longer used).
- Follow-up consolidation adjustment: restored explicit `LanWorkflow.LuxonConfigPath` so Luxon rewrite target remains explicit and decoupled from launch arguments.
- Improved launch-path intuition: relative `LocalServerExecutablePath` now resolves against `LocalServerWorkingDirectory` first when that setting is present.
- LAN release package no longer ships a static `BadHorse.PeakLanMod.cfg.template`; config is generated from plugin defaults on first launch.

## 2026-08-06

- Implemented Milestone 7 structured connection error mapping behind `LanWorkflow.EnableStructuredErrorMapping` (default `false`) to preserve rollback safety.
- Added deterministic LAN error classification for local server auto-start failures, readiness timeouts, Photon join/create/disconnect callback failures, and discovery incompatibility blocks.
- Added LAN state/UI surfacing of the latest structured error code and context.
- Updated release config template and troubleshooting docs for structured error diagnostics.
- Reduced M7 false-positive startup errors: `DisconnectByClientLogic` is now treated as non-actionable and clears stale structured error state instead of surfacing `UnknownPhotonFailure`.
- Reduced additional M7 startup noise: `OnDisconnected` classifications that remain `UnknownPhotonFailure` (for example `Exception` without deterministic routing context) are no longer surfaced as active structured UI errors.

## 2026-08-03

- Release tag: `v0.4.0`.
- Improved network setup and stability
- Improved installation guide

## 2026-08-02

- Release tag: `v0.3.0-m6-server-list` (Milestone 6 Part 2 UX/UI polish).
- Updated M6 server list UX to be scrollable with no fixed session-count cap.
- Added M6 UX Part 2 polish: `Join Selected` is disabled until a compatible session is selected, panel now shows inline join-unavailable reasons, and `Last refresh` timestamp is displayed with lightweight periodic auto-refresh plus manual refresh.
- Removed legacy in-game local-server status text notifications and UI reflection fallback probes; M6 LAN session panel is now the primary in-game status surface.
- Clarified `Photon.LocalServerAddress` as a bootstrap fallback endpoint (auto-managed in typical M6 discovery/join-selected flow).
- Switched M6 LAN UI interactions from shortcut-only to clickable overlay controls (`Host LAN`, `Join Selected`, `Refresh`) with clickable session rows; removed M6-specific shortcut config keys.
- Implemented Milestone 6 UI integration: added config-gated LAN UI panel rendering, join-selected session application, and connection-phase status snapshots from the LAN state store while preserving pre-M6 behavior behind `LanWorkflow.EnableLanUiActions`.
- Further reduced M5 discovery diagnostics noise: snapshot status now logs on change only, and incompatible-session updates log only when payload compatibility state changes.
- Reduced M5 discovery log noise: equivalent repeated announcements now refresh TTL without emitting repeated incompatible-session update lines.
- Implemented Milestone 5 UDP LAN discovery transport: config-gated host announcements, client listener, TTL/dedupe session store, and compatibility tagging (`IncompatibleProtocolVersion`, `IncompatibleGameVersion`, `IncompatibleModVersion`).
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
