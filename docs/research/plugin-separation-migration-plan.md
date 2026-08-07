# Plugin.cs separation of concerns migration plan and extraction map

Status: Draft (implementation-ready)
Date: 2026-08-07
Author: Copilot planning pass
Scope type: Planning only (no runtime behavior change in this document)
Authority: Working basis and migration log for splitting src/PeakLanMod/Plugin.cs

## Validation status for this planning task

- Method: static analysis only.
- Compiled in this planning task: no.
- Runtime tested in this planning task: no.
- Two-machine verified in this planning task: no.
- Physically offline LAN verified in this planning task: no.

## Runtime/context metadata captured for this plan

- PEAK build/version: unknown in this planning task (not probed at runtime).
- Mod version observed in source: 0.5.0.
- Commit hash: not captured in this planning task.
- Host/client role under analysis: both, by static flow review.
- Connection modes under analysis: LocalServer workflow, direct host, direct join, LAN discovery UI.
- Region/server context under analysis: Photon NameServer endpoint configured by LocalServerAddress/LocalServerPort/LocalServerProtocol.
- Test date: 2026-08-07 (planning only).

## Observed evidence (static)

1. src/PeakLanMod/Plugin.cs contains lifecycle orchestration, config binding, workflow policy, direct host/join orchestration, readiness/process control, discovery runtime, GUI rendering, validation/sanitization, and static APIs used by other files.
2. External callers are coupled to Plugin static methods and static config entries, especially:
- src/PeakLanMod/PhotonCallbackProbe.cs
- src/PeakLanMod/Patches/PhotonAppIdPatch.cs
- src/PeakLanMod/Patches/MainMenuPageHandlerUpdateBypassPatch.cs
- src/PeakLanMod/Patches/NetworkConnectorDisconnectBypassPatch.cs
- src/PeakLanMod/Patches/NetworkConnectorPatches.cs
- src/PeakLanMod/Patches/PhotonCallTracePatches.cs
- src/PeakLanMod/Lan/Discovery/UdpLanDiscoveryListener.cs
- src/PeakLanMod/Lan/Services/LuxonProcessController.cs
3. Existing direct host/join flow has queueing and readiness logic that is timing-sensitive and should be migrated with behavior parity first.

## Hypotheses and conclusions

### Hypotheses

1. Extracting behavior into cohesive services while preserving call order and defaults will keep the verified direct-connect baseline intact.
2. Keeping Plugin static wrapper methods during transition will avoid breakage in patches/callback probes.
3. Moving pure helpers first (validation/sanitization/fingerprint) will reduce risk and unblock later service extraction.

### Conclusions

1. Plugin should become a thin composition root plus compatibility facade.
2. Migration should be staged so each phase is reviewable and reversible.
3. A full member-by-member extraction checklist is required to avoid hidden leftovers.

## Target class model

1. PluginCompositionRoot
- Owns Awake/Update/OnDestroy/OnGUI and service wiring only.

2. LanPluginOptions
- Owns all config binding and typed access.

3. LanWorkflowPolicyService
- Owns workflow mode presets and auto-lock policy.

4. DirectConnectCoordinator
- Owns host/join queueing and state-machine transitions.

5. LocalServerRuntimeService
- Owns endpoint selection, readiness checks, Luxon process control, Luxon config automation, and Photon AppSettings application.

6. LanDiscoveryRuntimeCoordinator
- Owns listener/broadcaster lifecycle and session compatibility checks.

7. LanOverlayController
- Owns LAN overlay view state, style cache, and UI intent handling.

8. LanErrorStateService
- Owns structured LAN error set/clear and local-server detected/not-detected transitions.

9. LanIdentityAndValidation
- Owns room-name validation, endpoint sanitization, fingerprinting, and identity helper utilities.

## Constraints to preserve during migration

1. Preserve verified direct custom-Photon baseline behavior while LocalServer LAN behavior is still evolving.
2. Keep all new/experimental behavior behind existing configuration gates.
3. Do not remove diagnostic callbacks/logging until parity is proven.
4. Avoid broad behavior changes to Photon callback ordering or connection timing.
5. Keep Plugin static compatibility methods until all external callers are migrated.

## Required documentation deliverable per completed phase

Every completed migration phase must also update a living repository-structure guide for future agents.

Document path:
- docs/research/repository-structure-guide.md

Purpose:
- Explain the intended class/service boundaries after each phase.
- Explain where new implementations should be added.
- Explain where updates to existing behavior should be made.
- Record temporary compatibility wrappers and planned removal phase.

Minimum required updates after each completed phase:
1. Update an architecture snapshot section showing current truth (not target-only).
2. Add a routing table: responsibility -> preferred file/class -> anti-pattern location to avoid.
3. Add placement rules for new work:
- New networking workflow logic goes into coordinator/services, not Plugin root.
- New UI rendering/state goes into overlay/controller classes, not networking services.
- New Photon callback classification goes into diagnostics/error classification services.
4. Record newly introduced interfaces and their ownership.
5. Record what remains in transition, with explicit next phase for cleanup.

Review gate:
- A migration phase is not considered complete until both code and docs/research/repository-structure-guide.md are updated.

## Phased migration plan with change hypotheses

### Phase 0: Safety scaffolding and compatibility wrappers

Hypothesis: no runtime behavior change if Plugin remains source of truth and wrappers forward to new classes.

Scope (PR boundary):
- In scope:
- Create scaffolding classes/interfaces and wire them with no behavior move yet.
- Add temporary forwarding wrappers in Plugin for future extracted responsibilities.
- Keep all runtime behavior and execution order unchanged.
- Out of scope:
- Moving business logic out of Plugin.
- Removing existing Plugin static APIs.
- Any discovery, UI, host/join, or readiness behavior changes.

Tasks:
- Introduce interfaces and internal classes with no call-site change.
- Keep Plugin static APIs forwarding to new service instances.
- Add parity-focused logs at boundaries only if needed.

Exit criteria:
- Build success in local environment with PEAK refs.
- No behavior delta in host/join/discovery logs.

### Phase 1: Extract pure helper logic

Hypothesis: moving deterministic helper code is behavior-neutral.

Scope (PR boundary):
- In scope:
- Extract helper/utility methods only: normalization, blocked-term checks, sanitization, fingerprinting, identity helper methods.
- Keep method signatures and caller behavior stable through temporary wrappers.
- Out of scope:
- Config binding changes.
- Workflow mode logic changes.
- Any host/join orchestration flow changes.
- Any discovery runtime lifecycle changes.

Tasks:
- Move normalization, blocked terms, endpoint sanitization, fingerprint, and identity helper methods.
- Keep signatures stable through wrappers during transition.

Exit criteria:
- Same host room validation outcomes.
- Same sanitized logging output patterns.

### Phase 2: Extract config binding and workflow policy

Hypothesis: centralizing ConfigEntry ownership reduces coupling without flow changes.

Scope (PR boundary):
- In scope:
- Move Config.Bind ownership and ConfigEntry storage into LanPluginOptions.
- Move workflow preset and auto-lock policy logic into LanWorkflowPolicyService.
- Keep existing defaults and effective values unchanged.
- Out of scope:
- Discovery, UI, direct-host/direct-join orchestration extraction.
- Runtime endpoint/readiness process-control extraction.
- Removing compatibility wrappers.

Tasks:
- Move ConfigureDirectConnect and static ConfigEntry fields into LanPluginOptions.
- Move workflow preset logic into LanWorkflowPolicyService.

Exit criteria:
- Config defaults unchanged.
- Workflow mode behavior unchanged in logs.

### Phase 3: Extract discovery runtime and error state services

Hypothesis: discovery and structured error management can be isolated without altering connection flow.

Scope (PR boundary):
- In scope:
- Extract discovery listener/broadcaster runtime coordination.
- Extract structured error set/clear and local-server detected/not-detected state handling.
- Preserve existing callback call paths using wrappers/facades.
- Out of scope:
- Host/join queue state machine extraction.
- Local server readiness/process automation extraction.
- UI overlay extraction.

Tasks:
- Move listener/broadcaster lifecycle methods to LanDiscoveryRuntimeCoordinator.
- Move ReportStructuredLanError/ClearStructuredLanError and local-server detected/not-detected to LanErrorStateService.

Exit criteria:
- Discovery snapshot counts and broadcaster start/stop behavior remain equivalent.
- Structured error transitions match current behavior.

### Phase 4: Extract LocalServer runtime service

Hypothesis: consolidating endpoint/readiness/process logic improves maintainability while preserving behavior if sequencing stays identical.

Scope (PR boundary):
- In scope:
- Extract local server endpoint resolution and override management.
- Extract readiness probes and queued host readiness logic.
- Extract Luxon process ensure/stop and Luxon config host automation.
- Extract Photon AppSettings local-server application logic.
- Out of scope:
- Direct host/join queue orchestration extraction.
- Overlay UI extraction.
- Removal of remaining static compatibility wrappers.

Tasks:
- Move readiness checks, process ensure/stop, endpoint auto-detect, Luxon config updates, endpoint overrides, and AppSettings application.

Exit criteria:
- Host preflight and join readiness logs remain equivalent.
- No regression in connect retry timing.

### Phase 5: Extract direct connect coordinator

Hypothesis: isolating host/join orchestration reduces Plugin complexity while preserving scene and state-machine transitions.

Scope (PR boundary):
- In scope:
- Extract host/join intent queue state and processing.
- Extract direct connection readiness gating and reconnect throttling.
- Extract host/join state-machine transitions and Airport load trigger.
- Keep dependency calls to options/runtime/discovery services unchanged in behavior.
- Out of scope:
- UI overlay extraction.
- Compatibility wrapper removals not required for this extraction.
- New feature additions to host/join behavior.

Tasks:
- Move queue state, host/join start methods, and connection readiness gate logic.

Exit criteria:
- HostKey/JoinKey flows and queued host behavior unchanged.
- Host/Join state transitions still reach Airport as before.

### Phase 6: Extract LAN overlay controller

Hypothesis: UI extraction is safe if it calls the same intents and updates same shared state.

Scope (PR boundary):
- In scope:
- Extract OnGUI-driven LAN overlay rendering and style initialization.
- Extract overlay view state and settings-screen collapse behavior.
- Keep existing user interactions mapped to the same intents.
- Out of scope:
- Networking behavior changes.
- Changes to discovery protocol or readiness/process workflows.
- Wrapper removals unrelated to UI ownership.

Tasks:
- Move UI rendering/state/style logic and settings-screen collapse behavior.
- Keep interactions routed to same coordinator actions.

Exit criteria:
- Overlay renders and behaves identically in title scene.
- Join-selected and host actions still invoke same runtime intents.

### Phase 7: Remove compatibility debt

Hypothesis: after caller migration, Plugin can become thin without runtime impact.

Scope (PR boundary):
- In scope:
- Migrate remaining external callers from Plugin static helpers to service facades/interfaces.
- Remove deprecated wrappers and obsolete transitional fields.
- Finalize Plugin as composition root + minimal metadata/log surface.
- Out of scope:
- New feature work.
- Behavior changes beyond wrapper removal and caller rewiring.
- Structural redesign beyond the approved target model.

Tasks:
- Migrate external callers from Plugin static access to service interfaces.
- Remove deprecated static wrappers and redundant state.

Exit criteria:
- No remaining Plugin static coupling except constants/log if intentionally retained.
- Plugin class remains composition root only.

## Concrete extraction map (member-level)

Legend:
- Keep in Plugin means keep in composition root/facade.
- Extract means move implementation to target class and leave temporary wrapper if needed.

| Plugin.cs member | Destination class | Action | Notes |
|---|---|---|---|
| Awake | PluginCompositionRoot | Keep in Plugin | Construct/wire services, patch Harmony, attach callback probe. |
| Update | PluginCompositionRoot | Keep in Plugin | Route update ticks to workflow/discovery/ui/direct-connect coordinators. |
| OnDestroy | PluginCompositionRoot | Keep in Plugin | Dispose/shutdown services and unpatch Harmony. |
| OnGUI | PluginCompositionRoot | Keep in Plugin | Delegate to LanOverlayController render call. |
| DumpPhotonSettings | LocalServerRuntimeService | Extract | Keep temporary static wrapper for patch diagnostics. |
| ConfigureDirectConnect | LanPluginOptions | Extract | Move all Config.Bind calls. |
| ApplyLanWorkflowMode | LanWorkflowPolicyService | Extract | Called from update/lifecycle. |
| ApplyLanWorkflowPreset | LanWorkflowPolicyService | Extract | Internal helper in policy service. |
| SetConfigEntryValue<T> | LanWorkflowPolicyService | Extract | Generic helper for workflow preset updates. |
| SyncLanDiscoveryRuntime | LanDiscoveryRuntimeCoordinator | Extract | Per-frame discovery runtime reconciliation. |
| RefreshLanDiscoveryBroadcast | LanDiscoveryRuntimeCoordinator | Extract | Keep compatibility wrapper for callbacks. |
| StopLanDiscoveryBroadcast | LanDiscoveryRuntimeCoordinator | Extract | Keep compatibility wrapper for callbacks. |
| ShutdownLanDiscoveryRuntime | LanDiscoveryRuntimeCoordinator | Extract | Called on shutdown. |
| EvaluateLanSessionCompatibility | LanDiscoveryRuntimeCoordinator | Extract | Compatibility evaluator callback. |
| BuildLanDiscoveryAnnouncement | LanDiscoveryRuntimeCoordinator | Extract | Host announce payload builder. |
| IsMainMenuScene | LanOverlayController | Extract | UI visibility helper. |
| UpdateLanPanelCollapseForSettingsScreen | LanOverlayController | Extract | Settings-driven collapse policy. |
| IsSettingsScreenVisible | LanOverlayController | Extract | UI hierarchy probe. |
| IsLikelySettingsPanelName | LanOverlayController | Extract | Helper for settings screen detection. |
| RefreshLanUiSessions | LanOverlayController | Extract | Pulls listener snapshot into view model. |
| EnsureLanUiSessionsRefreshed | LanOverlayController | Extract | Auto refresh cadence. |
| TryCanJoinSelectedSession | LanOverlayController | Extract | UI button enablement logic. |
| TryJoinSelectedLanSession | LanOverlayController | Extract | Convert selected session into join intent. |
| TryResolveDiscoverySessionTransport | LanOverlayController | Extract | Helper for selected session transport parse. |
| RenderLanUiOverlay | LanOverlayController | Extract | Main immediate-mode UI rendering. |
| EnsureLanUiStyles | LanOverlayController | Extract | Style initialization/cache. |
| CreateSolidTexture | LanOverlayController | Extract | UI texture helper. |
| LogPhotonStateChanges | LanErrorStateService | Extract | Or separate PhotonStateTelemetryService if preferred. |
| RequestDirectHostStart | DirectConnectCoordinator | Extract | Host intent entrypoint. |
| QueueDirectHostStart | DirectConnectCoordinator | Extract | Queue state management. |
| TryProcessQueuedDirectHostStart | DirectConnectCoordinator | Extract | Queued host progression. |
| RequestDirectJoinStart | DirectConnectCoordinator | Extract | Join intent entrypoint with endpoint. |
| TryProcessQueuedDirectJoinStart | DirectConnectCoordinator | Extract | Queued join progression. |
| ClearPendingDirectJoinState | DirectConnectCoordinator | Extract | Queue cleanup and override reset call. |
| StartDirectHostOnce | DirectConnectCoordinator | Extract | Core host orchestration. |
| StartDirectJoin | DirectConnectCoordinator | Extract | Entry helper from keybind/UI. |
| StartDirectJoinOnce | DirectConnectCoordinator | Extract | Core join orchestration. |
| CanStartDirectConnection | DirectConnectCoordinator | Extract | Readiness/reconnect gating and throttled logging. |
| EnsureOnlineModeForDirectConnect | DirectConnectCoordinator | Extract | Offline mode forcing prior to connect path. |
| LoadAirport | DirectConnectCoordinator | Extract | Scene transition utility. |
| EnsureLocalServerReadinessBeforeConnect | LocalServerRuntimeService | Extract | Readiness gate for host/join. |
| EnsureQueuedHostReadinessBeforeConnect | LocalServerRuntimeService | Extract | Host queued readiness polling path. |
| ResetQueuedHostReadinessWindow | DirectConnectCoordinator | Extract | Queue timing state reset. |
| EnsureHostLocalServerProcess | LocalServerRuntimeService | Extract | Local server process ensure/autostart. |
| StopOwnedLocalServerProcessOnExit | LocalServerRuntimeService | Extract | Owned process shutdown semantics. |
| ApplyHostLanIpv4Selection | LocalServerRuntimeService | Extract | Endpoint auto-detect and config update. |
| ApplyHostLuxonConfigAutomation | LocalServerRuntimeService | Extract | Luxon external_address automation. |
| ApplyConfiguredPhotonSettings | LocalServerRuntimeService | Extract | Keep static wrapper for patch call path initially. |
| ApplyLocalServerSettings | LocalServerRuntimeService | Extract | AppSettings writer with validation. |
| GetConfiguredLocalEndpoint | LocalServerRuntimeService | Extract | String formatting helper. |
| GetEffectiveLocalEndpoint | LocalServerRuntimeService | Extract | Effective endpoint formatter with override. |
| GetConfiguredLocalServerEndpoint | LocalServerRuntimeService | Extract | Typed endpoint accessor. |
| GetEffectiveLocalServerEndpointForConnection | LocalServerRuntimeService | Extract | Config vs transient override resolver. |
| ApplyTransientJoinEndpointOverride | LocalServerRuntimeService | Extract | Runtime override management. |
| ClearTransientJoinEndpointOverride | LocalServerRuntimeService | Extract | Runtime override cleanup. |
| IsJoinEndpointOverrideActive property | LocalServerRuntimeService | Extract | Override state property. |
| NotifyLocalServerDetected | LanErrorStateService | Extract | Keep static compatibility wrapper for callbacks. |
| NotifyLocalServerNotDetected | LanErrorStateService | Extract | Keep static compatibility wrapper for callbacks. |
| ReportStructuredLanError | LanErrorStateService | Extract | Keep static compatibility wrapper for callbacks. |
| ClearStructuredLanError | LanErrorStateService | Extract | Keep static compatibility wrapper for callbacks. |
| HandleLeftRoom | LanErrorStateService | Extract | Delegates stop-owned-process-on-leave logic. |
| TryAutoLockWorkflowModeAfterSuccessfulHost | LanWorkflowPolicyService | Extract | Keep static compatibility wrapper for callback use. |
| NormalizeRoomName | LanIdentityAndValidation | Extract | Core normalization. |
| TryNormalizeRoomName | LanIdentityAndValidation | Extract | Safe parse wrapper. |
| NormalizeRoomNameInputForUi | LanIdentityAndValidation | Extract | UI-safe normalization. |
| TryContainsBlockedHostRoomNameTerm | LanIdentityAndValidation | Extract | Blocklist validation. |
| TryGetValidatedHostRoomName | LanIdentityAndValidation | Extract | Host-name validator. |
| TryGetValidatedHostRoomNameFromInput | LanIdentityAndValidation | Extract | UI-specific validation wording. |
| TryGetValidatedConfiguredHostRoomName | DirectConnectCoordinator | Extract | Uses options + validation service. |
| TryGetNormalizedConfiguredRoomName | DirectConnectCoordinator | Extract | Uses options + validation service. |
| SanitizeEndpointForLog | LanIdentityAndValidation | Extract | PII-safe endpoint logging helper. |
| Fingerprint | LanIdentityAndValidation | Extract | Keep Plugin wrapper until all call sites migrated. |
| PullU | LanIdentityAndValidation | Extract | User ID resolution helper. |
| Q1 | LanOverlayController | Extract | Admin gating check; may be renamed for clarity. |
| MixSig | LanOverlayController | Extract | Admin identity row helper. |

## Concrete extraction map (state and fields)

| Current field group in Plugin.cs | Destination class | Action | Notes |
|---|---|---|---|
| _harmony | PluginCompositionRoot | Keep in Plugin | Harmony lifecycle belongs to root. |
| _previousState | LanErrorStateService | Extract | Photon state transition telemetry state. |
| _pendingDirectHostStart / _pendingDirectHostConnectRequested / _queuedHostPreflightCompleted / _queuedHostReadinessStartedAtUtc / _queuedHostReadinessAttempts | DirectConnectCoordinator | Extract | Host queue state machine internals. |
| _pendingDirectJoinStart / _pendingDirectJoinConnectRequested / _pendingDirectJoinRoomName / _pendingDirectJoinSource / _pendingDirectJoinEndpoint | DirectConnectCoordinator | Extract | Join queue state machine internals. |
| _transientJoinEndpointOverride | LocalServerRuntimeService | Extract | Endpoint override state, currently static. |
| _lastAppliedLanWorkflowMode | LanWorkflowPolicyService | Extract | Last-applied workflow mode cache. |
| LanDiscoveryStateStore / LanDiscoveryListener / LanDiscoveryBroadcaster / LanDiscoveredSessionsViewModel / LanStatusPresenterBridge / LanDiscoveryServerInstanceId | LanDiscoveryRuntimeCoordinator (state store shared with UI) | Extract | Discovery runtime single-responsibility grouping. |
| _lastLanDiscoverySnapshotCount / _lastLanDiscoveryListenerRunning / _lastLanDiscoveryBroadcasterRunning | LanDiscoveryRuntimeCoordinator | Extract | Discovery change logging cache. |
| _isLanServerListCollapsed / _lanPanelCollapsedBySettingsAutomation / _allowLanPanelExpandedWhileSettingsVisible / _lastSettingsScreenProbeAt / _lanServerListScroll / _lanPreferredRoomNameInput / _lastLanUiRefreshAtRealtime / _lastLanUiRefreshAtUtc | LanOverlayController | Extract | UI state only. |
| _lastNotReadyLogAt / _lastReconnectAttemptAt | DirectConnectCoordinator | Extract | Connect retry/log throttling state. |
| _lanUiStyleInitialized and all _lanUi* styles | LanOverlayController | Extract | UI style cache. |
| _roomName / _hostKey / _joinKey | LanPluginOptions | Extract | Direct-connect options. |
| All internal static ConfigEntry fields under line ~2393 | LanPluginOptions | Extract | Keep temporary Plugin pass-through properties if needed. |
| PluginGuid / PluginName / PluginVersion | Plugin (or PluginMetadata static class) | Keep in Plugin | Referenced by BepInPlugin attribute and metadata logs. |
| Log | Plugin (or ILoggerAdapter) | Keep in Plugin (initially) | Needed broadly; optional future abstraction. |
| IsLocalServerMode | LanPluginOptions or LanModeService | Extract (later) | Currently always true; keep wrapper initially for compatibility. |
| X7GateSet / BlockedHostRoomNameTerms | LanIdentityAndValidation | Extract | Validation and admin gating data should leave root class. |

## External caller migration checklist

1. Photon callback probe
- src/PeakLanMod/PhotonCallbackProbe.cs
- Replace Plugin static method calls with injected/static service facade once available.

2. Photon settings patch
- src/PeakLanMod/Patches/PhotonAppIdPatch.cs
- Redirect from Plugin.ApplyConfiguredPhotonSettings to LocalServerRuntimeService facade.

3. UI bypass patches relying on config
- src/PeakLanMod/Patches/MainMenuPageHandlerUpdateBypassPatch.cs
- src/PeakLanMod/Patches/NetworkConnectorDisconnectBypassPatch.cs
- Migrate to options facade for AutoSkipPhotonFailureDialog and mode state.

4. Discovery/process utilities logging fingerprint
- src/PeakLanMod/Lan/Discovery/UdpLanDiscoveryListener.cs
- src/PeakLanMod/Lan/Services/LuxonProcessController.cs
- Replace Plugin.Fingerprint dependency with LanIdentityAndValidation service/static utility.

## Regression-focused validation matrix for each migration phase

1. Static validation
- dotnet build (only when local PEAK dependencies are available).

2. One-machine runtime smoke
- Host key path from title screen.
- Join key path with configured room.
- Expected callback order unchanged.

3. Two-machine runtime
- Host creates room, client joins same room.
- Airport load success and in-room callbacks.
- Discovery list and join-selected behavior.

4. Offline LAN runtime (target milestone)
- Internet physically disconnected.
- Local server endpoint reachable and readiness passes.
- Host/client full flow remains functional.

## Migration log template

Use this section as append-only log during implementation.

### Log entry template

- Date:
- Phase:
- Files changed:
- Behavioral hypothesis for this step:
- Build result:
- Runtime verification level (static/one-machine/two-machine/offline):
- First divergent callback/state observed (if any):
- Rollback applied (yes/no):
- Repository structure guide updated (yes/no):
- Repository structure guide sections changed:
- Notes:

### Log entries

- 2026-08-07
- Phase: Planning baseline only
- Files changed: docs/research/plugin-separation-migration-plan.md
- Behavioral hypothesis for this step: no runtime behavior change (documentation only)
- Build result: not run
- Runtime verification level: static
- First divergent callback/state observed: not applicable
- Rollback applied: no
- Notes: Initial plan and full extraction map created.

- 2026-08-07
- Phase: Planning governance update
- Files changed: docs/research/plugin-separation-migration-plan.md; docs/research/repository-structure-guide.md
- Behavioral hypothesis for this step: no runtime behavior change (documentation only)
- Build result: not run
- Runtime verification level: static
- First divergent callback/state observed: not applicable
- Rollback applied: no
- Repository structure guide updated (yes/no): yes
- Repository structure guide sections changed: initial document creation
- Notes: Added mandatory per-phase architecture documentation gate and created living repository-structure guide for future agents.

- 2026-08-07
- Phase: Planning scope clarification
- Files changed: docs/research/plugin-separation-migration-plan.md
- Behavioral hypothesis for this step: no runtime behavior change (documentation only)
- Build result: not run
- Runtime verification level: static
- First divergent callback/state observed: not applicable
- Rollback applied: no
- Repository structure guide updated (yes/no): no
- Repository structure guide sections changed: not applicable
- Notes: Added explicit PR scope boundaries (in-scope and out-of-scope) under Phase 0 through Phase 7.

- 2026-08-07
- Phase: Phase 0 - Safety scaffolding and compatibility wrappers
- Files changed: src/PeakLanMod/Plugin.cs; src/PeakLanMod/Lan/Services/PluginCompatibilityScaffolding.cs; docs/research/plugin-separation-migration-plan.md; docs/research/repository-structure-guide.md; CHANGELOG.md; README.md
- Behavioral hypothesis for this step: no runtime behavior change if Plugin remains behavior source and new services are plugin-backed compatibility adapters.
- Build result: dotnet build succeeded (netstandard2.1, local environment)
- Runtime verification level: two-machine (user confirmed)
- First divergent callback/state observed: not observed in static analysis
- Rollback applied: no
- Repository structure guide updated (yes/no): yes
- Repository structure guide sections changed: architecture snapshot, routing table, compatibility wrappers, interface ownership ledger, phase update log
- Notes: Added Phase 0 service contracts and plugin-backed adapter wiring only; no networking flow or callback ordering changes. User confirmed post-change two-machine runtime test passed.
