# Server List UX Follow-Up Plan

Date: 2026-08-09
Scope type: Static analysis and planning only (no runtime validation)
Target area: LAN overlay UI in main menu
Primary file to implement against: src/PeakLanMod/Lan/UI/LanOverlayController.cs
Design reference: docs/ux design/design_suggestion.png

## Scope update (locked for next implementation pass)

Included:
- Server list visual refinement and spacing normalization.
- Consistent panel styling across Server List, Log, and Admin Telemetry.
- Slightly transparent panel backgrounds where appropriate.

Deferred (do not implement in the next pass):
- Content truncation rules.
- Blur background effects.

Explicitly excluded by product direction:
- Leaf icon in panel titles.
- Colored ping indicators and connectivity bars.

## Current baseline snapshot

Observed baseline (from current code shape):
- Server list and actions are rendered by a custom Unity overlay controller.
- A dedicated state/log panel exists and supports scroll history plus latest status line.
- Admin telemetry panel exists and is conditionally visible.

Known stable constraints:
- Preserve current host/join/refresh interaction behavior.
- Preserve existing LAN diagnostics surfacing and callback/error pathways.
- Keep changes UI-only unless explicitly requested.

## Implementation sequence for future chat

### Phase 1: Visual tokens and layout constants

Objective:
- Introduce a unified visual system for spacing, typography, panel paddings, border intensity, and alpha levels.

Tasks:
- Define grouped constants near existing UI color and size constants.
- Replace repeated literal offsets with tokenized values.
- Keep existing functionality and event wiring unchanged.

Acceptance checks:
- No interaction changes in host/join/refresh or session selection.
- No new compile warnings/errors.

### Phase 2: Server List panel restyle

Objective:
- Align the Server List panel composition with the design reference while retaining current controls.

Tasks:
- Keep title row, room input, action buttons, list viewport, and footer as separate visual bands.
- Improve row card hierarchy (room identity first, technical metadata second).
- Tune panel alpha for slight transparency (no blur).

Acceptance checks:
- Action buttons always visible in expanded mode.
- List does not overlap controls across tested resolutions.

### Phase 3: Log panel refinement

Objective:
- Maintain the dark, high-contrast log interior and align panel chrome with the shared style tokens.

Tasks:
- Harmonize title spacing, inner margins, and border alpha.
- Preserve sticky latest line behavior while user scrolls history.
- Keep high readability for errors and state updates.

Acceptance checks:
- Latest status remains visible and updates correctly.
- Manual scroll position is preserved unless already at bottom.

### Phase 4: Admin panel alignment

Objective:
- Bring Admin Telemetry panel into the same visual family without changing data semantics.

Tasks:
- Match panel alpha/border treatment and title rhythm.
- Keep existing telemetry content and formatting logic.

Acceptance checks:
- Admin visibility conditions remain unchanged.
- Telemetry content remains complete and readable.

### Phase 5: Resolution and regression pass

Objective:
- Verify layout robustness and avoid overlap regressions.

Tasks:
- Test representative menu resolutions/aspect ratios.
- Confirm control hitboxes and scrolling behavior remain reliable.
- Verify collapsed/expanded transitions and settings auto-collapse interactions.

Acceptance checks:
- No overlap regressions.
- No interaction regressions.
- Build success.

## Non-goals for next pass

- No truncation engine or clipping policy changes.
- No blur shaders, render textures, or pipeline changes.
- No networking/state-machine behavior changes.

## Suggested handoff checklist for the next agent chat

1. Read docs/ux design/server-list-ux-follow-up-plan.md.
2. Confirm the deferred list is still deferred.
3. Implement only Phase 1 and Phase 2 first.
4. Build and validate UI interaction stability.
5. Continue to Phase 3 and Phase 4 only after Phase 2 visual review.
6. Finish with Phase 5 regression checks and note any unresolved edge cases.
