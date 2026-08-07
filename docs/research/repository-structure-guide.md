# Repository structure guide for migration and future agents

Status: Living document
Last updated: 2026-08-07
Source plan: docs/research/plugin-separation-migration-plan.md

## Purpose

This document captures the intended and current repository structure as Plugin.cs is split by responsibility.
Use it to route new implementations and updates to the most appropriate files and classes.

## Validation status for this document update

- Method: static documentation update only.
- Compiled in this update: no.
- Runtime tested in this update: no.

## Current architecture snapshot (phase tracking)

Phase state:
- Phase 0 status: completed.
- Phase 1 status: completed.
- Phase 2 status: completed.
- Phase 3 status: completed.
- Phase 4 status: completed.
- Phase 5-7 status: not started.

Current reality summary:
- Plugin root now delegates config binding to LanPluginOptions, workflow mode policy to LanWorkflowPolicyService, discovery runtime coordination to LanDiscoveryRuntimeCoordinator, and structured error/local-server state handling to LanErrorStateService.
- Phase 0 scaffolding now exists in `src/PeakLanMod/Lan/Services/PluginCompatibilityScaffolding.cs` with real service wiring for completed phases and placeholders for later extraction.
- Phase 1 extracted deterministic identity/validation helpers into `src/PeakLanMod/Lan/Services/LanIdentityAndValidation.cs` with wrapper-preserving calls through `Plugin` methods.
- Phase 2 extracted config entry ownership into `src/PeakLanMod/Lan/Services/LanPluginOptions.cs` and workflow preset/auto-lock policy into `src/PeakLanMod/Lan/Services/LanWorkflowPolicyService.cs`.
- Phase 3 extracted listener/broadcaster lifecycle and compatibility evaluation into `src/PeakLanMod/Lan/Services/LanDiscoveryRuntimeCoordinator.cs`, and extracted photon state transition logging plus structured LAN error state handling into `src/PeakLanMod/Lan/Services/LanErrorStateService.cs`.
- Phase 4 extracted local server endpoint override management, host LAN endpoint/Luxon automation, local process ensure/stop, readiness probes (including queued-host readiness window state), and Photon local-server AppSettings application into `src/PeakLanMod/Lan/Services/LocalServerRuntimeService.cs`.
- External callers still use Plugin static wrappers; callback call paths remain stable by design.

Target direction summary:
- Plugin becomes composition root plus temporary compatibility facade.
- Cohesive services own behavior by responsibility domain.

## Responsibility routing table

| Responsibility | Preferred implementation location | Avoid placing in |
|---|---|---|
| Lifecycle composition and service wiring | Plugin composition root | Feature services |
| Config binding and config defaults | LanPluginOptions | UI controllers, patches |
| Workflow presets and auto-lock policy | LanWorkflowPolicyService | UI classes |
| Host/join intent orchestration | DirectConnectCoordinator | Plugin root |
| Local server readiness/process/endpoint settings | LocalServerRuntimeService | UI classes |
| Discovery listener/broadcaster lifecycle | LanDiscoveryRuntimeCoordinator | Plugin root |
| LAN overlay rendering and input state | LanOverlayController | Networking services |
| Structured LAN error set/clear | LanErrorStateService | UI view models |
| Validation/sanitization/fingerprinting | LanIdentityAndValidation | Patches |
| Transitional service adapter ownership | PluginCompatibilityServices | Plugin feature methods |
| Helper compatibility wrappers | Plugin static helper methods delegating to services | New helper logic in Plugin root |

## Placement rules for new work

1. New networking workflow behavior
- Place in coordinator/services under src/PeakLanMod/Lan/Services.
- Do not add new workflow behavior to Plugin root except composition wiring.

2. New UI behavior
- Place in src/PeakLanMod/Lan/UI.
- Keep UI focused on rendering/view state and intent dispatch.

3. New discovery behavior
- Place in src/PeakLanMod/Lan/Discovery.
- Keep protocol compatibility checks and session lifecycle in discovery coordinator/services.

4. New diagnostics/error classification
- Place in src/PeakLanMod/Lan/Diagnostics.
- Keep mapping logic deterministic and centralized.

5. Patch behavior
- Keep patches thin.
- Patches should delegate to services/facades rather than embedding business logic.

## Transition compatibility wrappers (to track and remove)

Use this section to track temporary Plugin static wrappers and planned removal phase.

| Wrapper/API | Current consumers | Planned removal phase | Notes |
|---|---|---|---|
| Plugin.ApplyConfiguredPhotonSettings | PhotonAppIdPatch | Phase 7 | Replace with LocalServerRuntimeService facade. |
| Plugin.GetEffectiveLocalEndpointForLogging / StopOwnedLocalServerProcessForLeaveRoom | LanErrorStateService | Phase 7 | Wrappers retained while callback-oriented error service still routes through Plugin static surface. |
| Plugin.WorkflowMode and related config entry accessors | Patches + probes + Plugin runtime paths | Phase 7 | Accessors now delegate to LanPluginOptions-backed entries and remain as compatibility wrappers. |
| Plugin.NotifyLocalServerDetected / NotDetected | PhotonCallbackProbe | Phase 7 | Move to LanErrorStateService facade. |
| Plugin.ReportStructuredLanError / ClearStructuredLanError | PhotonCallbackProbe | Phase 7 | Wrappers now delegate to LanErrorStateService (Phase 3). |
| Plugin.RefreshLanDiscoveryBroadcast / StopLanDiscoveryBroadcast | PhotonCallbackProbe | Phase 7 | Wrappers now delegate to LanDiscoveryRuntimeCoordinator (Phase 3). |
| Plugin.Fingerprint | Discovery + process helpers + probes | Phase 7 | Wrapper now delegates to LanIdentityAndValidation (Phase 1). |
| Plugin.NormalizeRoomName / TryNormalizeRoomName / NormalizeRoomNameInputForUi | Plugin internal host/join + LAN UI | Phase 7 | Wrappers now delegate to LanIdentityAndValidation (Phase 1). |
| Plugin.TryGetValidatedHostRoomName / TryGetValidatedHostRoomNameFromInput | Plugin host + LAN UI input gate | Phase 7 | Wrappers now delegate to LanIdentityAndValidation (Phase 1). |
| Plugin.SanitizeEndpointForLog / PullU | Plugin local-server diagnostics + identity helpers | Phase 7 | Wrappers now delegate to LanIdentityAndValidation (Phase 1). |
| Plugin.Services | none (Phase 0 only) | Phase 7 | Transitional composition access point for plugin-backed adapters. |

## Interface ownership ledger

Add new interfaces here as they are introduced.

| Interface | Owner class | Responsibility |
|---|---|---|
| IPluginCompatibilityServices | PluginCompatibilityServices | Transitional access to extracted-responsibility service contracts. |
| ILanPluginOptions | LanPluginOptions | Config binding ownership and typed ConfigEntry surface for LAN workflow and direct connect keys. |
| ILanWorkflowPolicyService | LanWorkflowPolicyService | Workflow preset application and auto-lock policy behavior. |
| IDirectConnectCoordinator | PluginCompatibilityServices (placeholder) | Future host/join orchestration ownership contract. |
| ILanOverlayController | PluginCompatibilityServices (placeholder) | Future LAN UI ownership contract. |
| ILanDiscoveryRuntimeCoordinator | LanDiscoveryRuntimeCoordinator | Discovery runtime lifecycle, host announce broadcast, and compatibility evaluation surface. |
| ILanErrorStateService | LanErrorStateService | Photon state transition tracking and structured LAN error/local-server state surface. |
| ILocalServerRuntimeService | LocalServerRuntimeService | Local server endpoint overrides, readiness probes, local process lifecycle, host endpoint/Luxon automation, and Photon app-settings application. |
| ILanIdentityAndValidation | LanIdentityAndValidation | Normalization, host room-name validation, endpoint sanitization, fingerprint, and identity helper compatibility surface. |

## Phase update log

Append one entry whenever a migration phase is completed.

### Entry template

- Date:
- Phase completed:
- Architecture snapshot updated sections:
- Routing table changes:
- New interfaces introduced:
- Compatibility wrappers added/removed:
- Notes for future agents:

### Entries

- 2026-08-07
- Phase completed: Planning baseline only
- Architecture snapshot updated sections: initial file creation
- Routing table changes: initial routing table added
- New interfaces introduced: none
- Compatibility wrappers added/removed: initial tracking table added
- Notes for future agents: use this file as the first stop before adding new behavior during migration.

- 2026-08-07
- Phase completed: Phase 0
- Architecture snapshot updated sections: phase state and current reality summary
- Routing table changes: added transitional service adapter routing row
- New interfaces introduced: IPluginCompatibilityServices, ILanPluginOptions, ILanWorkflowPolicyService, IDirectConnectCoordinator, ILanOverlayController, ILanDiscoveryRuntimeCoordinator, ILanErrorStateService, ILocalServerRuntimeService, ILanIdentityAndValidation
- Compatibility wrappers added/removed: added Plugin.Services transitional wrapper surface; no removals
- Notes for future agents: keep Plugin static wrapper call sites unchanged until Phase 1+ implementation extraction starts.

- 2026-08-07
- Phase completed: Phase 1
- Architecture snapshot updated sections: phase state, current reality summary
- Routing table changes: added helper compatibility wrapper routing row
- New interfaces introduced: none
- Compatibility wrappers added/removed: no removals; helper wrappers now delegate to LanIdentityAndValidation
- Notes for future agents: keep wrapper signatures stable while moving non-helper responsibilities in later phases.

- 2026-08-07
- Phase completed: Phase 2
- Architecture snapshot updated sections: phase state, current reality summary
- Routing table changes: no new responsibility categories; updated ownership reality for config/workflow implementation.
- New interfaces introduced: none
- Compatibility wrappers added/removed: no removals; Plugin config accessors now delegate to LanPluginOptions, and Plugin workflow policy calls delegate to LanWorkflowPolicyService.
- Notes for future agents: keep existing Plugin static config accessors as compatibility wrappers until external callers are migrated in later phases. User confirmed post-change two-machine runtime verification passed.

- 2026-08-07
- Phase completed: Phase 3
- Architecture snapshot updated sections: phase state, current reality summary
- Routing table changes: no new responsibility categories; updated discovery/error routing ownership to concrete service classes.
- New interfaces introduced: none
- Compatibility wrappers added/removed: no removals; Plugin discovery and structured-error wrappers now delegate to LanDiscoveryRuntimeCoordinator and LanErrorStateService.
- Notes for future agents: keep Plugin static wrapper signatures and call paths for PhotonCallbackProbe and patches until wrapper removal phase. User confirmed Phase 3 two-machine host/join runtime verification passed.

- 2026-08-07
- Phase completed: Phase 4
- Architecture snapshot updated sections: phase state, current reality summary
- Routing table changes: local server runtime ownership now implemented in LocalServerRuntimeService instead of Plugin method bodies.
- New interfaces introduced: none
- Compatibility wrappers added/removed: no removals; Plugin local-server wrappers now delegate to LocalServerRuntimeService.
- Notes for future agents: keep Plugin wrapper signatures stable for callback/patch compatibility while Phase 5 extracts direct host/join orchestration. User confirmed Phase 4 two-machine host/join runtime verification passed.
