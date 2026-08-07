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
- Phase 5 status: completed.
- Phase 6 status: completed.
- Phase 7 status: completed.

Current reality summary:
- Plugin root now delegates config binding to LanPluginOptions, workflow mode policy to LanWorkflowPolicyService, discovery runtime coordination to LanDiscoveryRuntimeCoordinator, and structured error/local-server state handling to LanErrorStateService.
- Phase 0 scaffolding remains in `src/PeakLanMod/Lan/Services/PluginCompatibilityScaffolding.cs` with real service wiring and only startup-safe placeholders used before runtime initialization.
- Phase 1 extracted deterministic identity/validation helpers into `src/PeakLanMod/Lan/Services/LanIdentityAndValidation.cs` with wrapper-preserving calls through `Plugin` methods.
- Phase 2 extracted config entry ownership into `src/PeakLanMod/Lan/Services/LanPluginOptions.cs` and workflow preset/auto-lock policy into `src/PeakLanMod/Lan/Services/LanWorkflowPolicyService.cs`.
- Phase 3 extracted listener/broadcaster lifecycle and compatibility evaluation into `src/PeakLanMod/Lan/Services/LanDiscoveryRuntimeCoordinator.cs`, and extracted photon state transition logging plus structured LAN error state handling into `src/PeakLanMod/Lan/Services/LanErrorStateService.cs`.
- Phase 4 extracted local server endpoint override management, host LAN endpoint/Luxon automation, local process ensure/stop, readiness probes (including queued-host readiness window state), and Photon local-server AppSettings application into `src/PeakLanMod/Lan/Services/LanServerRuntimeService.cs`.
- Phase 5 extracted direct host/join queue orchestration, readiness/connect gating, reconnect throttling, and host/join state transitions into `src/PeakLanMod/Lan/Services/DirectConnectCoordinator.cs`.
- Phase 6 extracted LAN overlay rendering/state/style ownership and settings-screen auto-collapse behavior into `src/PeakLanMod/Lan/UI/LanOverlayController.cs` and wired it through `ILanOverlayController` in `PluginCompatibilityServices`.
- Phase 7 migrated remaining external callers away from Plugin static wrappers to service facades through `src/PeakLanMod/Lan/Services/LanRuntimeContext.cs`.
- Post-migration cleanup moved workflow mode typing to `src/PeakLanMod/Lan/Model/LanWorkflowMode.cs`, moved Photon settings diagnostics ownership to `LanServerRuntimeService`, and centralized mode-gate semantics behind `ILanModePolicyService`.
- Plugin is now a thin composition root plus metadata/logging.

Target direction summary:
- Plugin remains composition root with no feature-level static compatibility wrappers.
- Cohesive services own behavior by responsibility domain.

## Responsibility routing table

| Responsibility | Preferred implementation location | Avoid placing in |
|---|---|---|
| Lifecycle composition and service wiring | Plugin composition root | Feature services |
| Config binding and config defaults | LanPluginOptions | UI controllers, patches |
| Workflow presets and auto-lock policy | LanWorkflowPolicyService | UI classes |
| Host/join intent orchestration | DirectConnectCoordinator | Plugin root |
| Local server readiness/process/endpoint settings | LanServerRuntimeService | UI classes |
| Discovery listener/broadcaster lifecycle | LanDiscoveryRuntimeCoordinator | Plugin root |
| LAN overlay rendering and input state | LanOverlayController | Networking services |
| Structured LAN error set/clear | LanErrorStateService | UI view models |
| Validation/sanitization/fingerprinting | LanIdentityAndValidation | Patches |
| Transitional service adapter ownership | PluginCompatibilityServices | Plugin feature methods |
| Runtime service access from patches/callback probes | LanRuntimeContext | Plugin feature wrappers |

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

## Transition compatibility wrappers (Phase 7 completion state)

All tracked Plugin static compatibility wrappers have been removed in Phase 7.

Removed wrapper groups:
- Photon settings and callback helper wrappers now route directly to `LanRuntimeContext.Services` facades.
- Plugin static config-entry accessors are removed; callers now use `LanRuntimeContext.Options`.
- Plugin helper wrappers for fingerprint/identity/validation are removed; callers now use `LanRuntimeContext` or injected service interfaces.
- Plugin direct-connect private wrapper methods were inlined to direct coordinator calls from the Plugin update loop.

Retained intentionally in Plugin:
- Plugin metadata constants (`PluginGuid`, `PluginName`, `PluginVersion`).
- shared logger (`Plugin.Log`).

## Interface ownership ledger

Add new interfaces here as they are introduced.

| Interface | Owner class | Responsibility |
|---|---|---|
| IPluginCompatibilityServices | PluginCompatibilityServices | Transitional access to extracted-responsibility service contracts. |
| ILanPluginOptions | LanPluginOptions | Config binding ownership and typed ConfigEntry surface for LAN workflow and direct connect keys. |
| ILanModePolicyService | LanModePolicyService | Authoritative LAN mode-gate semantics for runtime checks. |
| ILanWorkflowPolicyService | LanWorkflowPolicyService | Workflow preset application and auto-lock policy behavior. |
| IDirectConnectCoordinator | DirectConnectCoordinator | Host/join queue orchestration, readiness/connect gating, reconnect throttling, and state-machine transitions. |
| ILanOverlayController | LanOverlayController | LAN overlay rendering, UI view state, settings-screen collapse policy, and UI intent dispatch. |
| ILanDiscoveryRuntimeCoordinator | LanDiscoveryRuntimeCoordinator | Discovery runtime lifecycle, host announce broadcast, and compatibility evaluation surface. |
| ILanErrorStateService | LanErrorStateService | Photon state transition tracking and structured LAN error/local-server state surface. |
| ILanServerRuntimeService | LanServerRuntimeService | Local server endpoint overrides, readiness probes, local process lifecycle, host endpoint/Luxon automation, and Photon app-settings application. |
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
- New interfaces introduced: IPluginCompatibilityServices, ILanPluginOptions, ILanWorkflowPolicyService, IDirectConnectCoordinator, ILanOverlayController, ILanDiscoveryRuntimeCoordinator, ILanErrorStateService, ILanServerRuntimeService, ILanIdentityAndValidation
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
- Routing table changes: local server runtime ownership now implemented in LanServerRuntimeService instead of Plugin method bodies.
- New interfaces introduced: none
- Compatibility wrappers added/removed: no removals; Plugin local-server wrappers now delegate to LanServerRuntimeService.
- Notes for future agents: keep Plugin wrapper signatures stable for callback/patch compatibility while Phase 5 extracts direct host/join orchestration. User confirmed Phase 4 two-machine host/join runtime verification passed.

- 2026-08-07
- Phase completed: Phase 5
- Architecture snapshot updated sections: phase state, current reality summary
- Routing table changes: no new responsibility categories; host/join routing now implemented by DirectConnectCoordinator while Plugin keeps thin wrapper entry points.
- New interfaces introduced: none
- Compatibility wrappers added/removed: no removals; Plugin private direct-connect wrappers now delegate to DirectConnectCoordinator.
- Notes for future agents: keep wrapper entry points stable for upcoming Phase 6 LAN overlay extraction and Phase 7 compatibility cleanup. User confirmed Phase 5 two-machine host/join runtime verification passed.

- 2026-08-07
- Phase completed: Phase 6
- Architecture snapshot updated sections: phase state, current reality summary
- Routing table changes: no new responsibility categories; LAN overlay routing is now implemented by LanOverlayController through ILanOverlayController service wiring.
- New interfaces introduced: none
- Compatibility wrappers added/removed: no removals; Plugin now delegates OnGUI/update collapse hooks to ILanOverlayController while preserving DirectConnect and callback wrapper surfaces.
- Notes for future agents: keep networking behavior unchanged and preserve remaining Plugin compatibility wrappers for Phase 7 caller migration and cleanup. User confirmed Phase 6 two-machine host/join runtime verification passed with deployed DLL.

- 2026-08-07
- Phase completed: Phase 7
- Architecture snapshot updated sections: phase state, current reality summary, target direction summary
- Routing table changes: replaced Plugin helper-wrapper routing with LanRuntimeContext runtime service-access routing.
- New interfaces introduced: none
- Compatibility wrappers added/removed: removed remaining Plugin static compatibility wrappers (settings/callback/config/helper/direct-connect wrappers); retained Plugin metadata/log/diagnostics only.
- Notes for future agents: route patch/probe access through LanRuntimeContext and keep new feature logic in domain services, not in Plugin. User confirmed physically offline LAN validation passed after Phase 7.

- 2026-08-07
- Phase completed: Post-migration cleanup - deviations backlog closure
- Architecture snapshot updated sections: current reality summary, transition compatibility wrappers
- Routing table changes: no new responsibility categories; diagnostics ownership normalized to LanServerRuntimeService.
- New interfaces introduced: ILanModePolicyService
- Compatibility wrappers added/removed: removed unused placeholder types in PluginCompatibilityScaffolding; retained startup-safe placeholders required before LanRuntimeContext.Initialize.
- Notes for future agents: workflow mode typing is now service/model-owned and mode-gating has a single authoritative policy source. Runtime behavior for this refactor was validated by static build only.
