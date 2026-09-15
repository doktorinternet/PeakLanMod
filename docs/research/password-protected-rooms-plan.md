# PEAK LAN Mod: Password-Protected Rooms Plan

Status: Proposed
Date: 2026-09-15
Author: Copilot (peak-lan-engineer)
Scope type: Research and design only (no implementation in this document)

## Validation record for this plan

- Validation method: static analysis only.
- PEAK build/version: unknown in this task (not captured from runtime logs).
- Mod commit: not captured in this task.
- Host/client role observed in this task: not runtime-tested.
- Connection mode observed in this task: code-path review only (direct-connect and LAN discovery flow).
- Region/server observed in this task: not applicable (planning only).
- Test date: 2026-09-15.
- Compiled in this task: no.
- One-machine runtime tested in this task: no.
- Two-machine runtime tested in this task: no.
- Physically offline LAN tested in this task: no.

## Problem statement

Host players want an optional password gate on hosted rooms:

- a host-side toggle to enable/disable password protection,
- a password input shown when creating a room (plaintext entry is acceptable, no obfuscation required initially),
- a lock indicator on protected sessions in the server list,
- a join-time password prompt (submit/cancel) for protected sessions,
- passwords must never be logged,
- join attempts must be validated against a securely stored representation of the password, not the plaintext itself.

## Observed evidence (from repository code)

1. Room creation/join is driven by PEAK's own `NetworkConnector`, which reads `HostState.RoomName` / `JoinSpecificRoomState.RoomName` and calls `PhotonNetwork.CreateRoom` / `PhotonNetwork.JoinRoom`. This mod does not own that call site directly.
2. Other mods successfully Harmony-patch a `HostRoomOptions`-style method to mutate `RoomOptions` before creation (see `docs/research/discovery-occupancy-capacity-plan.md`, PEAKUNLIMITED `HostRoomOptions` MaxPlayers patch reference). The same pattern can inject `CustomRoomProperties`.
3. LAN discovery is this mod's own UDP broadcast/listener protocol (`src/PeakLanMod/Lan/Discovery/UdpLanDiscoveryBroadcaster.cs`, `UdpLanDiscoveryListener.cs`), independent of Photon's lobby room list. A "requires password" flag must be added to this announcement schema and to `LanSessionInfo` (`src/PeakLanMod/Lan/Model/LanSessionInfo.cs`), not derived from Photon.
4. There is no existing custom Photon event / RPC infrastructure in the mod (`RaiseEvent` / `IOnEventCallback` do not currently appear in the codebase). This is new surface area.
5. `LanIdentityAndValidation` (`src/PeakLanMod/Lan/Services/LanIdentityAndValidation.cs`) already performs SHA-256 fingerprinting for sensitive strings (room-name terms, user identity). This is the existing precedent for how password material must be hashed/fingerprinted before it can appear in logs.
6. `docs/research/host-authorization-hardening-options.md` already designed "Option A: mod-only host admission gate" (join-then-verify-then-kick) for a related unauthorized-hosting problem. Password auth should reuse that same mechanic rather than invent a new one.
7. `src/PeakLanMod/Lan/UI/LanOverlayController.cs` builds all UI manually at runtime (hand-constructed `InputField`/`Button`/`Text` objects with hardcoded layout constants, redrawn every frame in `RenderLanUiOverlay`). New controls must follow this same construction/layout style, not Unity editor prefabs.
8. `src/PeakLanMod/Patches/CloseConnectionProbe.cs` already probes/logs `PhotonNetwork.CloseConnection`, confirming host-side kick is an established, observable mechanism in this codebase.
9. `LanErrorCode` (`src/PeakLanMod/Lan/Model/LanErrorCode.cs`) is the existing structured-error enum surfaced through `LanErrorStateService`/UI; a new code is the natural extension point for "incorrect password".

## Threat model and limits

### Assumed threat model

- LAN-local, semi-trusted environment; goal is deterrence against casual/accidental joins and basic guessing, not resistance to a modified/malicious client.
- All participating clients run this mod (the verification protocol requires the same custom event handling on both sides).

### Hard limit

Photon's client-side `JoinRoom` cannot be gated *before* the join completes without a server-side plugin (Option C in `host-authorization-hardening-options.md` — a separate, larger effort, out of scope here). A modified client that ignores the verification event can still remain briefly connected until kicked. This design is explicitly "somewhat secure," matching the stated requirement, not authoritative server-side enforcement.

## Core security design: join-then-verify-then-kick

1. **Host side, at room creation:**
   - Generate a random per-room salt.
   - Compute `hash = SHA256(salt || password)` (or PBKDF2/Rfc2898DeriveBytes with a modest iteration count if verification-time cost is acceptable; decide during implementation based on join-time latency).
   - Store `salt` and `hash` in `RoomOptions.CustomRoomProperties` (not published to any Photon lobby; LAN discovery is a separate channel).
   - Never store or log the plaintext password. Only a short fingerprint (same convention as `LanIdentityAndValidation.Fingerprint`) may appear in diagnostics.

2. **Discovery side:**
   - Add a `requires_password: bool` field to the UDP announcement schema (schema version bump) and to `LanSessionInfo`.
   - Do **not** broadcast the salt or hash — only the boolean flag — so nothing password-derived is exposed to passive LAN listeners who never join.

3. **Join side:**
   - Client sees the lock indicator and is prompted for a password before the join attempt proceeds.
   - Client proceeds through the existing `DirectConnectCoordinator` join flow unchanged (Photon itself has no password concept).
   - Immediately after `OnJoinedRoom`, the client reads `CurrentRoom.CustomProperties["pwd_salt"]` (now replicated to the new actor), computes `clientHash = SHA256(salt || enteredPassword)`, and sends it to the Master Client via a new custom Photon event (`RaiseEvent`, reliable, targeted to the room's master actor).
   - The Master Client compares `clientHash` to its authoritative `pwd_hash` and replies with an `AuthOk` / `AuthDenied` custom event.
   - On `AuthDenied` or timeout, the host calls `PhotonNetwork.CloseConnection(player)` to kick the actor. The client, on denial/disconnect, surfaces a new `LanErrorCode.IncorrectPassword` and returns to the server list instead of loading into the map.
   - While awaiting the verdict, the client UI shows a brief "Verifying password…" state so it does not render into the world before a possible kick.

4. Rooms without password protection enabled behave exactly as today — the feature is additive and config/UI-gated end to end.

## Data model changes

- `Lan/Discovery/UdpLanDiscoveryBroadcaster.cs` (and listener/codec): add `requires_password` (bool, default `false`) to `LanDiscoveryAnnouncement` and its serializer/parser. Bump schema version; old announcements without the field must parse with `requires_password = false` for backward compatibility.
- `Lan/Model/LanSessionInfo.cs`: add `RequiresPassword` property, threaded through `LanDiscoveryRuntimeCoordinator` wherever `LanSessionInfo` is constructed.
- New small model (e.g. `Lan/Model/LanRoomPasswordPolicy.cs`) holding `Enabled`, `Salt`, `Hash`, plus helper methods to compute/verify hashes.
- New Photon custom event codes (e.g. in a new `Lan/Services/LanRoomAuthService.cs`) for `PasswordVerify`, `PasswordAuthOk`, `PasswordAuthDenied`.
- New `LanErrorCode.IncorrectPassword` (and possibly `PasswordVerificationTimedOut`) in `LanErrorCode.cs`.

## Config additions (`LanPluginOptions`)

- `RequirePasswordForHostedRoom` (bool, default `false`) — mirrors the checkbox state.
- Hosted room password itself is **not** persisted via `config.Bind`/BepInEx config file (avoids a plaintext secret on disk); it is held in memory only, owned by the overlay/coordinator, for the duration of the host session.
- `PasswordVerificationTimeoutMs` (e.g. 4000), following the style of existing `*TimeoutMs` options.
- All additions default to preserving current behavior exactly when unused.

## UI changes (`LanOverlayController`)

Related work: `docs/research/lan-overlay-ui-refactor-plan.md` proposes splitting `LanOverlayController` into small per-panel classes (`HostPanelController`, `ServerListPanelController`, a shared `LanOverlayWidgetFactory`, etc.) before/alongside this feature. The password UI pieces below should be built as their own small classes from the start, not as more private methods appended to the existing monolithic `LanOverlayController`, so they land directly in the improved structure instead of adding to the debt it replaces:

1. **Host panel additions:** a "Require Password" toggle and a conditional password `InputField`, added to (or as part of) `HostPanelController` if the UI refactor has landed, or as a small standalone `HostPasswordFieldController`-style helper otherwise — not inlined into whatever class currently owns the room-name input. Plaintext-visible input is acceptable per requirements.
2. **Server list row lock indicator:** a small, self-contained helper (e.g. `SessionRowLockBadge` rendering logic) called from `ServerListPanelController`/the session row builder, rather than adding another inline conditional block to the row-rendering loop.
3. **Join password prompt:** a new, standalone `JoinPasswordModalController` (own file, own construction/update methods) with a password `InputField`, a "Submit" button (stores the password for the pending join attempt and calls the existing `RequestDirectJoinStart` flow), and a "Cancel" button (dismisses the modal and aborts the join attempt with no partial join left behind). Triggered only for `RequiresPassword` sessions; unlocked sessions are unaffected. This is the clearest case for its own class, since it is a distinct, self-contained interactive surface (not a tweak to an existing panel).
4. **Verifying/denied state:** a brief "Verifying password…" status text, owned by whichever panel hosts join-phase status (today's client state panel), and an incorrect-password message routed through the existing `LanErrorStateService` / `LanErrorDetail` UI surface used for other `LanErrorCode`s today. No new class needed here beyond a small state field, since this reuses an existing display surface.

If the UI refactor plan has not landed yet when this feature is implemented, still extract these three pieces (host password field, row lock badge, join modal) into their own classes/methods with clear boundaries, so `LanOverlayController` gains three small collaborators instead of three more blocks of inline code — this keeps the eventual refactor a pure extraction rather than a rewrite.

## Coordinator/service changes

- `DirectConnectCoordinator`: add an in-memory pending-password field for the active join attempt and a pending host password to consume at room-create time. Both cleared after the attempt completes, fails, or is canceled.
- New `LanRoomAuthService` (name indicative), responsible for:
  - Host: generating salt/hash at room-create time, storing them into `RoomOptions.CustomRoomProperties`, listening for the verify event, comparing, replying, and kicking on failure.
  - Client: reading room properties post-join, computing the local hash, sending the verify event, awaiting the reply/timeout, and triggering leave + error surfacing on failure.
  - Implements `IOnEventCallback` (new pattern for this repo), registered/unregistered alongside existing Photon callback wiring. May warrant its own listener class rather than extending `PhotonCallbackProbe`, to keep diagnostic probing and auth enforcement separate.
- New Harmony patch (in `Patches/`) targeting the same room-options construction method other mods patch for `MaxPlayers` (exact method to be identified via decompiled reference during implementation) to inject `CustomRoomProperties["pwd_salt"]` / `["pwd_hash"]` only when `RequirePasswordForHostedRoom` is enabled for the current host attempt.

## Logging and secrecy rules

- Never log the plaintext password, the raw hash, or the raw salt bytes.
- Log only: whether a password is required (bool), verification result (ok/denied/timeout), and a short fingerprint (SHA-256 prefix, same convention as `LanIdentityAndValidation`) if a correlation identifier is needed.
- Do not persist the password to BepInEx `config.cfg`; it lives in memory only for the host session / join attempt.

## Compatibility and rollback

- Fully backward compatible: senders without `requires_password` parse as `false`; unmodded/unprotected rooms are unchanged.
- Opt-in per host via the checkbox; default off preserves the verified baseline exactly.
- Rollback path: disabling the checkbox (or the config default) fully restores current behavior; no changes to non-password host/join code paths.

## Validation plan

- Static review only where PEAK assemblies are unavailable in the working environment.
- Local compile with `dotnet build` where proprietary references are available.
- Manual two-machine test matrix (extend `docs/testing/manual-two-machine-checklist.md`):
  - host with password + correct client password: success, full join.
  - host with password + wrong client password: kicked, correct error shown, no map load.
  - host without password: unchanged baseline.
  - stale/older client attempting a password-protected room: fails gracefully, no crash.
- No runtime success will be claimed without logs or explicit user confirmation, per repository policy.

## Suggested implementation milestones

1. Data model + discovery schema: `requires_password` field, schema bump, `LanSessionInfo` propagation, server-list lock badge as its own small helper (see UI section above). No host/join enforcement yet.
2. Config flags + host UI (checkbox + password field) as its own small helper/collaborator, not inlined into `LanOverlayController`. Captures input only, no enforcement.
3. Room-creation patch: inject salt/hash into `CustomRoomProperties` when enabled. Verify via logs that properties exist; no client-side check yet.
4. Client-side join prompt UI (`JoinPasswordModalController`, submit/cancel) and pending-password plumbing in `DirectConnectCoordinator`. Wiring only, no verification yet.
5. Verification service: custom Photon events, post-join hash check, kick-on-fail, new `LanErrorCode`, "verifying" UI state.
6. Two-machine manual test pass and documentation update (`current-network-findings.md` and/or this document) recording validation status honestly.

## Open questions

- Exact method name/signature to patch for `RoomOptions`/`CustomRoomProperties` injection at host time (needs decompiled reference confirmation, same as the PEAKUNLIMITED `HostRoomOptions` precedent).
- Whether SHA-256(salt||password) is sufficient or PBKDF2/Rfc2898DeriveBytes should be used, weighed against join-time verification latency.
- Whether the lock indicator should use a text glyph or an image, depending on available font glyph support in the existing UI text rendering.
- Whether older/incompatible clients attempting a password room should be blocked earlier (e.g. via existing compatibility gating) rather than only via the post-join event round trip.
