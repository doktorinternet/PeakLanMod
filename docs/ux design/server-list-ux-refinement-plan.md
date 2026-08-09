# Server List UX Refinement Plan

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

### Phase 6: Server row parity polish

Objective:
- Close the remaining visual gap between current rows and the design suggestion while keeping existing data semantics and interactions unchanged.

Tasks:
- Refine row card emphasis for selected vs non-selected states using alpha, border, and typography only.
- Align style of implementation with `design_suggestion.png`, in the areas of colors and borders.
- Improve primary/secondary line hierarchy for better scanability at a glance.
- Move IP and Port to secondary line.
- Remove transport
- Keep the visible row cap at six and preserve existing scroll behavior for larger session counts.
- Player count rightmost in each row, centered vertically.

Out of scope:
- Unsolicited new tags, components and text. 

Acceptance checks:
- Session selection behavior remains unchanged.
- No regressions in join-selected enable/disable behavior.
- List remains stable with 0, 1-6, and 10+ sessions.

### Phase 7: Header, control, and footer rhythm calibration

Objective:
- Tighten spacing and typography cadence in the Server List panel to better match the design suggestion.

Tasks:
- Adjust title, room-name band, action band, and footer vertical rhythm in small tokenized increments.
- Re-check button widths and inter-button spacing at representative widths.
- Preserve current host/join/refresh wiring and click areas.

Acceptance checks:
- Action buttons remain fully visible in expanded mode.
- No overlap between controls, list viewport, and footer across target resolutions.
- Build success.

### Phase 8: Cross-panel chrome harmonization

Objective:
- Finalize visual consistency across Server List, Log, and Admin Telemetry panel chrome.

Tasks:
- Harmonize border alpha, panel alpha, and corner treatment across all three panels.
- Fine-tune title rhythm consistency between panel headers.
- Preserve the high-contrast dark log interior readability.

Acceptance checks:
- All three panels read as one visual family.
- Log history and latest status remain highly readable.
- Admin visibility conditions and telemetry formatting remain unchanged.

### Phase 9: Runtime validation matrix and sign-off

Objective:
- Complete final runtime validation for layout robustness and interaction reliability.

Tasks:
- Validate representative resolutions and aspect ratios, including at least one low-height case.
- Verify expanded/collapsed transitions and settings-driven auto-collapse behavior.
- Verify button hitboxes and list scrolling under simulation load.

Acceptance checks:
- No overlap regressions in tested scenarios.
- No interaction regressions in host/join/refresh/session-selection pathways.
- Build success and documented manual test notes.

## Non-goals for next pass

- No truncation engine or clipping policy changes.
- No blur shaders, render textures, or pipeline changes.
- No networking/state-machine behavior changes.

## Suggested handoff checklist for the next agent chat

1. Read docs/ux design/server-list-ux-refinement-plan.md.
2. Confirm the deferred list is still deferred.
3. Confirm completion status for Phases 1 through 5 before starting additional polish.
4. Execute Phase 6 and Phase 7 first, then run a build and quick layout sanity check.
5. Execute Phase 8 after visual review of Phase 6/7.
6. Complete Phase 9 runtime validation and capture unresolved edge cases.
