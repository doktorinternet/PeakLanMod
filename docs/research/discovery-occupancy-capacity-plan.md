# Discovery Occupancy and Capacity Plan

Status: Proposed
Date: 2026-08-09
Scope type: Static analysis and implementation planning only (no code changes in this plan)
Primary objective: Add room player count and room capacity to LAN discovery and server-list UX, while preserving the verified LAN baseline.

## Validation status for this plan

- Method: static analysis only.
- Compiled in this planning task: no.
- One-machine runtime tested in this planning task: no.
- Two-machine runtime tested in this planning task: no.
- Physically offline LAN tested in this planning task: no.

## Inputs and evidence

### Evidence source A: current LAN mod codebase

Observed:
- Discovery announcement schema currently has no occupancy fields.
- Discovered session model currently has no occupancy fields.
- Session row rendering currently has no occupancy display.
- Join eligibility currently checks compatibility and transport, not full-room state.

Key references:
- src/PeakLanMod/Lan/Discovery/UdpLanDiscoveryBroadcaster.cs
- src/PeakLanMod/Lan/Services/LanDiscoveryRuntimeCoordinator.cs
- src/PeakLanMod/Lan/Model/LanSessionInfo.cs
- src/PeakLanMod/Lan/Discovery/UdpLanDiscoveryListener.cs
- src/PeakLanMod/Lan/State/LanConnectionStateStore.cs
- src/PeakLanMod/Lan/UI/LanStatusPresenterBridge.cs
- src/PeakLanMod/Lan/UI/LanOverlayController.cs

### Evidence source B: external mod analysis (PEAKUNLIMITED)

Source analyzed:
- docs/research/mods/PEAKUNLIMITED.blob

Observed:
- The mod sets room capacity at room-options creation using a max-player config value.
- The mod patches NetworkingUtilities max-player path and HostRoomOptions.
- The mod logs occupancy as current player count plus configured max players.
- The mod contains many additional patches for systems that assume vanilla 4-player constraints (UI arrays, end screens, waiting UI, etc.), indicating that capacity above 4 is a cross-system risk.

Minimal interoperability references:
- Config max-player binding: docs/research/mods/PEAKUNLIMITED.blob line 185
- HostRoomOptions patch with MaxPlayers assignment: docs/research/mods/PEAKUNLIMITED.blob lines 1210-1222
- Joined/left log format with current/configured count: docs/research/mods/PEAKUNLIMITED.blob line 1234
- Vanilla 4-player constant and multiple >4 compensating patches: docs/research/mods/PEAKUNLIMITED.blob lines 281, 1083-1087, 1516, 1588

## Conclusions from evidence

1. Occupancy and capacity belong in discovery metadata and should be transmitted.
2. Discovery capacity should come from authoritative room state when available, not from local config mirrors.
3. Capacity above 4 should be surfaced as a compatibility-policy concern, not just cosmetic metadata.
4. Changes should be rolled out in backward-compatible phases so mixed sender versions remain discoverable.

## Data model and protocol proposal

Discovery payload additions:
- current_players: int
- max_players: int

Suggested semantics:
- Known values: current_players >= 0 and max_players >= 1
- Unknown value sentinel: -1
- Invalid packet values: current_players < -1, max_players < -1, or current_players > max_players when max_players > 0

Compatibility policy:
- Parser accepts missing occupancy fields during rollout (legacy sender compatibility).
- UI renders occupancy as unknown when fields are missing/unknown.
- Optional policy gate may classify sessions with max_players > 4 as unsupported for baseline-safe joining.

## Implementation plan (small, reviewable commits)

### Commit 1: Expand discovery schema with backward-compatible parsing

Hypothesis:
- Optional occupancy fields can be added without affecting host/join flow or legacy discovery behavior.

Files:
- src/PeakLanMod/Lan/Discovery/UdpLanDiscoveryBroadcaster.cs

Checklist:
- Add CurrentPlayers and MaxPlayers to LanDiscoveryAnnouncement.
- Serialize current_players and max_players.
- Parse occupancy fields as optional.
- Default missing values to -1.
- Keep existing required fields unchanged.
- Add validation helper for occupancy field ranges.

Acceptance:
- Legacy packets still parse.
- New packets parse with occupancy values.
- No change to create/join state behavior.

Rollback:
- Remove occupancy keys and constructor properties.

### Commit 2: Source occupancy from authoritative Photon room state in host broadcasts

Hypothesis:
- Broadcasting room-derived occupancy provides accurate metadata and does not change connection sequencing.

Files:
- src/PeakLanMod/Lan/Services/LanDiscoveryRuntimeCoordinator.cs

Checklist:
- In BuildLanDiscoveryAnnouncement, populate:
  - current_players from PhotonNetwork.CurrentRoom.PlayerCount when room exists.
  - max_players from PhotonNetwork.CurrentRoom.MaxPlayers when room exists.
- Use -1 when CurrentRoom is unavailable.
- Keep all existing fields and compatibility checks intact.

Acceptance:
- Host log confirms occupancy is included in broadcast context while in-room.
- Broadcaster lifecycle remains unchanged.

Rollback:
- Revert occupancy sourcing logic to unknown sentinel only.

### Commit 3: Propagate occupancy through session model and state store

Hypothesis:
- Carrying occupancy through session snapshots enables reliable UI refresh on join/leave churn.

Files:
- src/PeakLanMod/Lan/Model/LanSessionInfo.cs
- src/PeakLanMod/Lan/Discovery/UdpLanDiscoveryListener.cs
- src/PeakLanMod/Lan/State/LanConnectionStateStore.cs

Checklist:
- Add CurrentPlayers and MaxPlayers properties to LanSessionInfo constructor and class.
- Populate the fields when building LanSessionInfo in listener.
- Include occupancy fields in state equivalence checks so occupancy changes trigger update path.
- Preserve firstSeen/lastSeen/expiry semantics.

Acceptance:
- Session row state updates on occupancy changes without duplicate entries.
- TTL eviction and dedupe behavior unchanged.

Rollback:
- Remove occupancy fields from model and equivalence check.

### Commit 4: Display occupancy and capacity in server-list and admin telemetry

Hypothesis:
- Showing occupancy improves join decisions without changing networking behavior.

Files:
- src/PeakLanMod/Lan/UI/LanStatusPresenterBridge.cs

Checklist:
- Update BuildSessionRowLabel to include occupancy token when known (example: 2/4).
- Render unknown-safe fallback when values are unavailable.
- Add CurrentPlayers and MaxPlayers to BuildAdminTelemetryPanelData rows.
- Keep existing compatibility and scene display tokens.

Acceptance:
- Rows remain readable and stable for 0, 1-6, and 10+ sessions.
- Admin panel remains complete and formatted.

Rollback:
- Remove occupancy from row and telemetry formatting only.

### Commit 5: Block join when discovered session is full

Hypothesis:
- Full-room precheck prevents avoidable join attempts and reduces noisy join failures.

Files:
- src/PeakLanMod/Lan/UI/LanOverlayController.cs

Checklist:
- Extend TryCanJoinSelectedSession with full-room condition:
  - if current_players >= 0 and max_players > 0 and current_players >= max_players, block join.
- Keep compatibility and transport checks unchanged.
- Add clear reason text for disabled join button state.

Acceptance:
- Join Selected disables for full sessions.
- Non-full compatible sessions remain joinable via existing flow.

Rollback:
- Remove full-room branch from join eligibility.

### Commit 6: Add optional policy gate for capacity above validated baseline

Hypothesis:
- Guarding >4 capacity sessions behind policy reduces user exposure to known cross-system risks.

Files:
- src/PeakLanMod/Lan/Services/LanPluginOptions.cs
- src/PeakLanMod/Lan/Services/PluginCompatibilityScaffolding.cs
- src/PeakLanMod/Lan/Services/LanDiscoveryRuntimeCoordinator.cs

Checklist:
- Add config flag, default conservative value chosen by project owner.
  - Suggested key: LanWorkflow.BlockJoinForCapacityAboveValidatedBaseline
- In discovery compatibility evaluation, classify sessions with max_players > 4 when policy enabled.
- Preserve rollback path by config toggle.

Acceptance:
- Policy-off: behavior unchanged for >4 sessions.
- Policy-on: >4 sessions visibly classified with explicit reason.

Rollback:
- Disable config or remove check.

### Commit 7: Update simulated discovery entries for occupancy-aware UX testing

Hypothesis:
- Simulated entries with occupancy improve UI testing fidelity without runtime-network dependencies.

Files:
- src/PeakLanMod/Lan/Discovery/SimulatedLanDiscoverySnapshotProvider.cs

Checklist:
- Populate CurrentPlayers and MaxPlayers with realistic spread.
- Include at least one full and one near-full simulated session.
- Keep simulated sessions non-joinable by existing preview-only logic unless intentionally changed.

Acceptance:
- UI can visually validate occupancy rendering and full-room states offline.

Rollback:
- Revert simulated occupancy fields.

### Commit 8: Documentation and manual validation updates

Hypothesis:
- Codifying protocol and checks prevents regressions during future refactors.

Files:
- docs/research/lan-implementation-plan.md
- docs/testing/manual-two-machine-checklist.md
- docs/research/current-network-findings.md (optional short evidence note)

Checklist:
- Add occupancy fields and semantics to discovery schema section.
- Add manual checks for occupancy update cadence and full-room join gating.
- Add note that >4 capacity requires explicit compatibility policy decision.

Acceptance:
- Documentation reflects runtime expectations and rollback knobs.

Rollback:
- Remove doc entries only.

## Reason tokens and user-facing copy

Suggested reason identifiers:
- RoomFull
- OccupancyUnknown
- InvalidOccupancyData
- ExtendedCapacityRequiresSupport

Suggested UI copy:
- RoomFull: Selected session is full.
- OccupancyUnknown: Session occupancy is unavailable.
- InvalidOccupancyData: Session reported invalid occupancy metadata.
- ExtendedCapacityRequiresSupport: Session capacity exceeds validated support for this build.

## Detailed file touchpoint map

Commit 1:
- src/PeakLanMod/Lan/Discovery/UdpLanDiscoveryBroadcaster.cs

Commit 2:
- src/PeakLanMod/Lan/Services/LanDiscoveryRuntimeCoordinator.cs

Commit 3:
- src/PeakLanMod/Lan/Model/LanSessionInfo.cs
- src/PeakLanMod/Lan/Discovery/UdpLanDiscoveryListener.cs
- src/PeakLanMod/Lan/State/LanConnectionStateStore.cs

Commit 4:
- src/PeakLanMod/Lan/UI/LanStatusPresenterBridge.cs

Commit 5:
- src/PeakLanMod/Lan/UI/LanOverlayController.cs

Commit 6:
- src/PeakLanMod/Lan/Services/LanPluginOptions.cs
- src/PeakLanMod/Lan/Services/PluginCompatibilityScaffolding.cs
- src/PeakLanMod/Lan/Services/LanDiscoveryRuntimeCoordinator.cs

Commit 7:
- src/PeakLanMod/Lan/Discovery/SimulatedLanDiscoverySnapshotProvider.cs

Commit 8:
- docs/research/lan-implementation-plan.md
- docs/testing/manual-two-machine-checklist.md
- docs/research/current-network-findings.md (optional)

## Test matrix additions for two-machine validation

Record before run:
- PEAK version
- Mod commit hash
- Host/client roles
- Connection mode (LanServer)
- Date/time
- Internet physically disconnected yes/no

New checks:
- Host room at creation: occupancy appears as 1/max.
- Client discovers host session with matching max_players.
- Occupancy increments/decrements on second client join/leave within one broadcast interval.
- Full-room sessions disable Join Selected.
- Mixed-version discovery still lists sessions when occupancy fields are absent.
- If >4 policy gate enabled, discovered >4 sessions are explicitly marked and blocked.

## Risk notes and safeguards

- Preserve direct LAN baseline behavior by introducing occupancy as additive metadata first.
- Do not alter Photon connect/create/join sequencing while introducing schema fields.
- Keep policy gates configuration-driven for rollback.
- Avoid broad patching of gameplay systems based solely on capacity metadata; limit this plan to discovery/UI/join gating.

## Open decisions for project owner

1. Should BlockJoinForCapacityAboveValidatedBaseline default to true or false?
2. Should max_players > 4 be classified as incompatible or only warning-level non-blocking?
3. Should OccupancyUnknown allow join by default?

## Suggested execution order

- Execute commits 1 through 5 first for occupancy metadata and full-room UX safety.
- Run two-machine validation.
- Decide on commit 6 policy default based on validation confidence and support scope.
- Complete commits 7 and 8 for UX parity and durable documentation.
