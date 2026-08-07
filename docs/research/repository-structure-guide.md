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
- Phase 1-7 status: not started.

Current reality summary:
- Plugin root still contains lifecycle, config, workflow, discovery, local server runtime, direct connect orchestration, UI, and shared helpers.
- Phase 0 scaffolding now exists in `src/PeakLanMod/Lan/Services/PluginCompatibilityScaffolding.cs` with plugin-backed adapters and placeholder interfaces for later extraction.
- External callers still use Plugin static wrappers; no behavior-routing migration occurred in this phase.

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
| Plugin.NotifyLocalServerDetected / NotDetected | PhotonCallbackProbe | Phase 7 | Move to LanErrorStateService facade. |
| Plugin.ReportStructuredLanError / ClearStructuredLanError | PhotonCallbackProbe | Phase 7 | Move to LanErrorStateService facade. |
| Plugin.RefreshLanDiscoveryBroadcast / StopLanDiscoveryBroadcast | PhotonCallbackProbe | Phase 7 | Move to LanDiscoveryRuntimeCoordinator facade. |
| Plugin.Fingerprint | Discovery + process helpers + probes | Phase 7 | Move to LanIdentityAndValidation utility. |
| Plugin.Services | none (Phase 0 only) | Phase 7 | Transitional composition access point for plugin-backed adapters. |

## Interface ownership ledger

Add new interfaces here as they are introduced.

| Interface | Owner class | Responsibility |
|---|---|---|
| IPluginCompatibilityServices | PluginCompatibilityServices | Transitional access to extracted-responsibility service contracts. |
| ILanPluginOptions | PluginCompatibilityServices (placeholder) | Future config binding ownership contract. |
| ILanWorkflowPolicyService | PluginCompatibilityServices (placeholder) | Future workflow preset/policy ownership contract. |
| IDirectConnectCoordinator | PluginCompatibilityServices (placeholder) | Future host/join orchestration ownership contract. |
| ILanOverlayController | PluginCompatibilityServices (placeholder) | Future LAN UI ownership contract. |
| ILanDiscoveryRuntimeCoordinator | PluginCompatibilityServices (plugin-backed) | Discovery broadcast compatibility surface. |
| ILanErrorStateService | PluginCompatibilityServices (plugin-backed) | Structured LAN error and local-server state compatibility surface. |
| ILocalServerRuntimeService | PluginCompatibilityServices (plugin-backed) | Photon app-settings compatibility surface. |
| ILanIdentityAndValidation | PluginCompatibilityServices (plugin-backed) | Fingerprint compatibility surface. |

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
