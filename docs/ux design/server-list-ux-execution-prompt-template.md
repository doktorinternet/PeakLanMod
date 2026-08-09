# Server List UX Execution Prompt Template

Use this in a future agent chat to execute the next UI pass.

## Copy/Paste Prompt

You are working in PEAK_LAN_MOD.

Goal:
Implement the next server-list UX pass described in docs/ux design/server-list-ux-follow-up-plan.md, using docs/ux design/design_suggestion.png as the visual reference.

Scope for this run:
- Execute only Phase 1 and Phase 2 from the plan.
- Keep changes UI-only in src/PeakLanMod/Lan/UI/LanOverlayController.cs unless a small helper extraction is clearly needed.

Hard constraints:
- Preserve current host/join/refresh behaviors and wiring.
- Preserve current state/log and admin telemetry functional behavior.
- Keep the dedicated client state panel with scroll history and sticky latest status.
- Keep direct-connect baseline behavior unchanged.

Do NOT implement:
- Content truncation rules.
- Blur background effects.
- Leaf icon in panel titles.
- Colored ping indicators or connectivity bars.

Visual direction:
- Unify spacing/typography/padding/border/alpha via constants (Phase 1).
- Improve server list visual hierarchy and panel rhythm (Phase 2).
- Slight transparency is allowed for panel backgrounds.

Quality bar:
- Avoid overlap regressions (buttons, list viewport, footer).
- Keep expanded and collapsed modes stable.
- Build must pass with dotnet build.

Output expectations:
1. Brief hypothesis of the UI change.
2. Exact files modified.
3. Summary of implemented Phase 1 and Phase 2 changes.
4. Validation results (build/errors).
5. Any remaining risks for Phase 3/4.
