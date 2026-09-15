# PEAK LAN Mod: LAN Overlay UI Refactor Plan

Status: Proposed
Date: 2026-09-15
Author: Copilot (peak-lan-engineer)
Scope type: Research and design only (no implementation in this document)

## Validation record for this plan

- Validation method: static analysis only.
- PEAK build/version: unknown in this task (not captured from runtime logs).
- Mod commit: not captured in this task.
- Compiled in this task: no.
- One-machine runtime tested in this task: no.
- Two-machine runtime tested in this task: no.
- Physically offline LAN tested in this task: no.

## Problem statement

`src/PeakLanMod/Lan/UI/LanOverlayController.cs` has grown into a ~1,950-line single class that owns widget construction, layout math (hand-computed pixel rects via constants), styling, dirty-state tracking, and Photon/discovery-state-to-UI binding, all recomputed in a single `RenderLanUiOverlay` call every frame. This makes the file expensive to extend safely (e.g. for the password-protected-rooms feature) and hard to reason about in isolation.

## Constraints (explicitly scoped by the user)

- No separate Unity Editor project / AssetBundle pipeline. All UI continues to be constructed at runtime in C#, in this repository, using `UnityEngine.UI` components already referenced (`Button`, `InputField`, `Text`, `TMP_Text`, `ScrollRect`, `Image`).
- Performance is not a driving concern: the overlay is only active on the main menu scene (`ShouldRenderLanUiOverlay` gates on `IsMainMenuScene()`), so per-frame rebuild cost during actual gameplay is a non-issue. The refactor is about maintainability, not runtime cost.
- Preferred decomposition style: composition into small, separately testable/reviewable classes, consistent with how `Plugin.cs`-adjacent responsibilities were previously split out and how `Lan/Services/*` already decomposes runtime concerns (`DirectConnectCoordinator`, `LanServerRuntimeService`, `LanErrorStateService`, etc.), rather than partial classes.

## Observed evidence (from repository code)

1. `LanOverlayController` constructs every widget by hand (`EnsureOverlayUi`, `EnsureTemplateText`) and repositions everything every call to `RenderLanUiOverlay` via `SetLocalTopLeftRect`/`SetAbsoluteTopLeftRect` with dozens of named pixel constants (e.g. `SessionRowSecondaryTop = 31f`, `AdminBodyFontSize = 14f`).
2. The same method also owns: host panel, room-name input syncing, join/host/refresh button wiring, session list rendering and row pooling (`EnsureSessionRow`, `HideUnusedRows`), a client state log panel (`RenderClientStatePanel`), and an admin telemetry panel — five distinct UI surfaces in one method.
3. Dirty-state tracking is manual and scattered (`_isSyncingRoomInput`, `_lastLoggedConnectionPhase`, `_lastLoggedEndpoint`, `_lastLoggedErrorSignature`, `_lastRenderedStateLogText`), which is the kind of bookkeeping a change-driven update model would eliminate.
4. Button click handlers are re-registered every frame (`_hostButton.onClick.RemoveAllListeners(); _hostButton.onClick.AddListener(...)`), which is only safe because this is main-menu-only and low frequency; it is still unnecessary churn.
5. Existing repo convention favors small, single-responsibility classes wired together through an explicit composition root (`LanRuntimeContext.Services`), which this refactor should follow rather than introducing a new pattern.

## Goals

1. Split `LanOverlayController` into small classes with one clear responsibility each.
2. Replace hand-computed pixel rects with Unity's built-in layout components (`VerticalLayoutGroup`, `HorizontalLayoutGroup`, `LayoutElement`, `ContentSizeFitter`) wherever practical, without requiring prefabs/AssetBundles.
3. Move from "recompute everything every frame" to "rebuild/update only what changed," using explicit change notifications instead of scattered `_last*` comparison fields.
4. Make the structure obviously extensible so the password-protected-rooms UI (toggle, conditional password field, lock badge, join modal) is additive rather than more of the same monolith.

## Proposed structure

New folder: `src/PeakLanMod/Lan/UI/Overlay/` (or similar), containing focused classes:

- `LanOverlayWidgetFactory.cs` — the only place that knows how to construct a themed `Button`, `InputField`/`TMP_InputField`, `Text`/`TMP_Text`, bordered `Image`, toggle, etc., using the existing color/style constants (`UiButtonColor`, `UiTextColor`, corner-radius sprites, border helpers). Centralizes what `EnsureOverlayUi`/`EnsureTemplateText`/`ApplyButtonColors`/`AddBorder`/`AddFaintBorder` do today, so every panel below reuses it instead of duplicating construction code.
- `LanOverlayLayoutHelpers.cs` — thin wrapper utilities for attaching `VerticalLayoutGroup`/`HorizontalLayoutGroup`/`LayoutElement`/`ContentSizeFitter` with the repo's standard spacing/padding constants, replacing most manual `SetLocalTopLeftRect` call sites. Absolute/free positioning (e.g. the collapse button, panel-to-screen anchoring) can remain manual where a layout group doesn't fit.
- `HostPanelController.cs` — room name input, host button, the new "Require Password" toggle and password field (see the password plan), and host-availability warning text. Owns only host-panel widgets and their update logic.
- `ServerListPanelController.cs` — session list, row pooling (`EnsureSessionRow`/`HideUnusedRows`), join/refresh buttons, empty-state text, and the new per-row lock indicator.
- `JoinPasswordModalController.cs` — the new password-prompt modal (submit/cancel), built from the start as its own class per the password plan, not appended to `LanOverlayController`.
- `ConnectionStatePanelController.cs` — the client state log panel (currently `RenderClientStatePanel`/`EnsureClientStateLogUpdated`/`AppendClientStateLogEntry`).
- `AdminTelemetryPanelController.cs` — the admin panel block (title/body sizing, `BuildAdminTelemetryPanelData` usage).
- `LanOverlayController.cs` (slimmed down) — becomes a coordinator: owns the root canvas/panel object, decides overall panel sizing/collapse state, and delegates to the panel controllers above. Keeps `ShouldRenderLanUiOverlay`/`RenderLanUiOverlay`/`UpdateLanPanelCollapseForSettingsScreen` as the public entry points used by callers today, so the public surface used elsewhere in the mod does not change.

Each panel controller follows the same shape: constructed once with a reference to `LanOverlayWidgetFactory` (and whatever services it needs, e.g. `IDirectConnectCoordinator`), exposes an `EnsureUi(Transform parent)` method (build-once) and an `Update(...)` method (apply current state), mirroring the existing `Ensure*`/render split already present in the codebase (e.g. `EnsureSessionRow` vs. per-frame updates) but consistently applied everywhere instead of ad hoc.

## Change-driven updates instead of full per-frame rebuild

- Introduce a small snapshot/comparison struct per panel (e.g. `ServerListRenderState` capturing session list identity/count/selected index/version) computed cheaply once per frame by the coordinator.
- Each panel controller's `Update` early-returns when its relevant snapshot is unchanged since the last call, instead of unconditionally re-running all `SetLocalTopLeftRect`/text assignment/listener-rebinding work.
- Button listeners are attached once in `EnsureUi` (capturing a delegate that reads current state via a passed-in accessor/callback) instead of `RemoveAllListeners`/`AddListener` every frame.
- This directly removes the scattered `_last*` dirty-tracking fields in favor of one explicit "did the input state change" check per panel, which is both simpler and easier to unit-test in isolation (each snapshot struct can be compared/tested without any Unity object involved).

## Layout groups vs. manual rects

- Panels that are naturally linear (host panel row: label + input + button; footer row: last-refresh text + mod version text; admin panel: title above body) convert directly to `HorizontalLayoutGroup`/`VerticalLayoutGroup` + `LayoutElement` (fixed width/flexible width) with padding/spacing pulled from the existing constants (`PanelPaddingX`, `ControlGap`, `SectionGap`, etc.), removing the matching `SetLocalTopLeftRect` call sites.
- The session list rows are a natural fit for a `VerticalLayoutGroup` inside the scroll content, replacing the manual `rowStride`/`rowStartY` math in the render loop.
- Keep manual absolute positioning only where layout groups genuinely don't fit: the overall panel's screen-corner anchoring (`SetAbsoluteTopLeftRect` against `Screen.width`/`Screen.height`), the collapse button's fixed corner position, and the admin rail's position relative to the main panel.
- This is done incrementally, panel by panel, so each conversion is independently reviewable and testable rather than one large layout rewrite.

## Migration approach (small, reviewable steps)

1. Introduce `LanOverlayWidgetFactory` and move existing construction helpers (`EnsureTemplateText`, `ApplyButtonColors`, border/rounded-sprite helpers) into it verbatim first, with `LanOverlayController` calling into it. No behavior change; pure extraction.
2. Extract `ServerListPanelController` (largest, most self-contained surface: session rows, join/refresh buttons, empty state) into its own class, delegating from `LanOverlayController`. Verify session list behavior is unchanged (row selection, join button enablement, empty state).
3. Extract `HostPanelController`, `ConnectionStatePanelController`, and `AdminTelemetryPanelController` similarly, one at a time.
4. Convert one panel's manual rects to layout groups (start with the footer or host-panel row, the simplest linear cases) and confirm visually before converting the session list rows.
5. Introduce per-panel snapshot structs and change-driven `Update` early-return, starting with the panel least likely to change per frame (admin telemetry) before the most dynamic one (session list).
6. Only after the above is stable, build the password-protected-rooms UI pieces (`HostPanelController` additions, new `JoinPasswordModalController`, `ServerListPanelController` lock-badge addition) directly into the new structure, per the password plan's milestone 2–4 ordering.

## Relationship to the password-protected-rooms plan

This refactor and `docs/research/password-protected-rooms-plan.md` are sequenced so the password UI lands in the new structure rather than the old monolith: complete steps 1–3 above (widget factory + server list + host panel extraction) before starting password UI milestone 2 (host checkbox/password field) and step 3/host-panel extraction before password UI milestone 4 (join modal). The password plan has been updated to say new pieces are built as their own small classes from the start, consistent with this plan.

## Non-goals

- No AssetBundle/prefab pipeline (explicitly excluded by the user).
- No change to the public methods `LanOverlayController` exposes to the rest of the mod (`ShouldRenderLanUiOverlay`, `RenderLanUiOverlay`, `UpdateLanPanelCollapseForSettingsScreen`, `ILanOverlayController`), so call sites elsewhere are unaffected.
- No visual redesign; this is a structural/maintainability refactor only. Colors, fonts, and layout constants are preserved as-is during extraction, only reorganized.

## Validation plan

- Static review only where PEAK assemblies are unavailable in the working environment.
- Local compile with `dotnet build` where proprietary references are available.
- Manual main-menu visual check after each extraction step (panel renders identically, buttons/inputs still function, collapse/expand still works, session list selection/join still works).
- No runtime success will be claimed without logs, screenshots, or explicit user confirmation, per repository policy.

## Open questions

- Whether `TMP_InputField`/`InputField` mixed usage (the codebase uses both `InputField` and `TMP_Text`) should be normalized to one text stack as part of this refactor or left as-is to keep the change minimal.
- Whether `LayoutRebuilder.ForceRebuildLayoutImmediate` calls will be needed after dynamic content changes (e.g. session count changes) when switching the session list to a `VerticalLayoutGroup`, and whether that reintroduces a per-change cost worth measuring even though it's main-menu-only.
