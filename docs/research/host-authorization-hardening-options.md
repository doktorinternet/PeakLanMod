# PEAK LAN Mod: Host Authorization Hardening Options

Status: Research draft
Date: 2026-08-17
Author: Copilot (GPT-5.3-Codex)
Scope type: Research and design only (no implementation in this document)

## Validation record for this research

- Validation method: static analysis only.
- PEAK build/version: unknown in this task (not captured from runtime logs).
- Mod commit: 663619a4ddaa.
- Host/client role observed in this task: not runtime-tested.
- Connection mode observed in this task: code-path review only (LanServer mode and discovery flow).
- Region/server observed in this task: local NameServer flow in code; no runtime region capture in this task.
- Test date: 2026-08-17.
- Compiled in this task: no.
- One-machine runtime tested in this task: no.
- Two-machine runtime tested in this task: no.
- Physically offline LAN tested in this task: no.

## Problem statement

Current behavior allows any machine that can reach the shared Photon/Luxon server endpoint to attempt room creation. If an endpoint becomes known (discovery list, local logs, screenshots, etc.), unauthorized hosting attempts are possible.

Goal: hinder or prevent unauthorized room hosting on a shared server process, while preserving the verified direct host/join baseline and keeping fallback paths.

## Observed evidence (from repository code)

1. Discovery announcements include join-relevant endpoint metadata:
- nameserver_address
- nameserver_port
- transport

2. Discovery and UI intentionally surface reachable sessions and endpoint data for join flow.

3. Direct connect in LanServer mode uses NameServer endpoint settings and calls the existing PEAK host/join state flow after readiness checks.

4. Authentication path remains CustomAuthenticationType.None in the baseline findings, and no server-side trust boundary is defined in current docs/code.

5. Host-side callback handling currently logs player joins/leaves but does not enforce admission policy by removing unauthorized actors.

6. Local server process startup is controlled by the mod, but process ownership does not imply network-level access control.

## Threat model and limits

### Threat model assumed

- Attacker has LAN reachability to the Photon/Luxon server endpoint.
- Attacker can run PEAK with or without this mod, and may know endpoint/room naming conventions.
- Attacker may learn endpoint via broadcast discovery, logs, or social sharing.

### Hard limit for mod-only controls

A client-side mod cannot guarantee prevention of unauthorized room creation on an open Photon server if the server accepts unauthenticated clients for CreateRoom operations. Mod-only controls can deter and reduce abuse, but cannot be authoritative against a determined peer on the same LAN.

## Option matrix

### Option A: Mod-only host admission gate (deterrence)

Summary:
- Host mints a per-room join proof secret.
- Legitimate clients provide proof (for example via Player Custom Properties after join).
- Host immediately kicks players lacking valid proof.

Pros:
- Implementable entirely in mod + callbacks.
- Strongly reduces unauthorized joins into a protected room.
- No Luxon server binary/plugin changes required.

Cons:
- Does not prevent unauthorized creation of other rooms on the same server.
- Secret distribution must be designed carefully (discovery payload or out-of-band).
- A reverse-engineering attacker can still imitate the protocol eventually.

Implementation steps:
1. Add config-gated feature flag, default off initially.
2. Generate per-room random secret when host intent starts.
3. Define proof format (for example HMAC(hostSecret, roomName|userId|nonce)).
4. Send proof material only to intended clients (discovery payload extension and/or manual code entry).
5. Validate in OnPlayerEnteredRoom and kick on failure.
6. Add minimal diagnostics with fingerprints only.
7. Keep rollback path by disabling the feature flag.

### Option B: Mod-only room namespace hardening (deterrence)

Summary:
- Use high-entropy room names that are not user-guessable.
- Optionally rotate per session.

Pros:
- Very low complexity.
- No server change required.
- Reduces accidental or casual abuse.

Cons:
- Security by obscurity only.
- Does not prevent endpoint-level unauthorized CreateRoom.
- Discovery list can still reveal room identity.

Implementation steps:
1. Add config to force randomized room names on host.
2. Avoid writing full room names to logs (use fingerprint).
3. Share room identity only through approved join path.

### Option C: Server-side authorization plugin (prevention)

Summary:
- Add authoritative server rule: reject CreateRoom unless client presents valid signed host token.
- Keep token validation server-side.

Pros:
- Best prevention quality for unauthorized host attempts.
- Centralized policy; cannot be bypassed by editing client mod alone.
- Can support allowlists, expiry, and revocation cleanly.

Cons:
- Requires Photon/Luxon server extensibility work and deployment complexity.
- Requires protocol and key-management design.
- Needs careful compatibility rollout with baseline.

Implementation steps:
1. Confirm Luxon/Photon server plugin hook availability for auth/CreateRoom interception.
2. Define token format and signing key strategy (per-server secret).
3. Add mod path to request/compose token before host attempt.
4. Validate token in server plugin; reject unauthorized CreateRoom.
5. Add structured reason codes for client diagnostics.
6. Ship behind config flag; preserve current open mode for fallback tests.

### Option D: Network perimeter controls (firewall/segmentation)

Summary:
- Limit who can reach server ports at all.

Pros:
- Strong, immediate protection independent of mod correctness.
- Works even for unmodded or malicious clients.
- Fastest operational mitigation.

Cons:
- Administrative overhead (IP changes, onboarding friction).
- Poor fit for ad-hoc LAN groups unless tooling is added.
- Can accidentally block legitimate clients.

Implementation steps (Windows host):
1. Restrict inbound server ports to private profile only.
2. Allow inbound only from allowlisted remote addresses for UDP/TCP server ports.
3. Block broad inbound on the same ports for all others.
4. Disable or restrict HTTP admin/API port unless explicitly needed.
5. Document quick rollback commands.

Example command templates (replace placeholders):
- SERVER_EXE_PATH
- ALLOWED_CLIENT_IPS
- NS_PORT, MS_PORT, GS_PORT

PowerShell examples:

```powershell
New-NetFirewallRule -DisplayName "PEAK LAN Photon Allow UDP" -Direction Inbound -Action Allow -Program "SERVER_EXE_PATH" -Protocol UDP -LocalPort NS_PORT,MS_PORT,GS_PORT -RemoteAddress ALLOWED_CLIENT_IPS -Profile Private
New-NetFirewallRule -DisplayName "PEAK LAN Photon Allow TCP" -Direction Inbound -Action Allow -Program "SERVER_EXE_PATH" -Protocol TCP -LocalPort NS_PORT,MS_PORT,GS_PORT -RemoteAddress ALLOWED_CLIENT_IPS -Profile Private
New-NetFirewallRule -DisplayName "PEAK LAN Photon Block Others UDP" -Direction Inbound -Action Block -Program "SERVER_EXE_PATH" -Protocol UDP -LocalPort NS_PORT,MS_PORT,GS_PORT -RemoteAddress Any -Profile Private
New-NetFirewallRule -DisplayName "PEAK LAN Photon Block Others TCP" -Direction Inbound -Action Block -Program "SERVER_EXE_PATH" -Protocol TCP -LocalPort NS_PORT,MS_PORT,GS_PORT -RemoteAddress Any -Profile Private
```

Operational note:
- For home LANs with stable clients, this is often the highest-value immediate control.

### Option E: Discovery privacy reduction (hinder endpoint leakage)

Summary:
- Reduce endpoint exposure in client UI/logs and discovery payload.
- Move endpoint details to on-demand join handshake where possible.

Pros:
- Lowers accidental endpoint disclosure.
- Reduces casual copy-paste abuse.

Cons:
- Does not stop attackers who can sniff traffic or inspect memory.
- May complicate troubleshooting.
- Must preserve two-machine usability.

Implementation steps:
1. Minimize endpoint display in user-visible UI by default.
2. Keep endpoint in verbose diagnostics only, sanitized.
3. Evaluate discovery schema change: publish opaque session identifier, resolve endpoint only on join intent.
4. Validate no regression in host/join success path.

## Recommendation

Use layered controls, in this order:

1. Immediate: Option D (firewall/segmentation) for real prevention now.
2. Near-term mod update: Option A + Option E for deterrence and reduced leakage.
3. Long-term robust solution: Option C for authoritative host authorization.
4. Keep Option B as supplemental friction only.

Rationale:
- Firewall and segmentation are the only immediately enforceable controls without server-plugin engineering.
- Mod-only controls improve posture but cannot fully prevent unauthorized room creation on an open server.
- Authoritative prevention requires server-side policy enforcement.

## Proposed phased implementation plan (no code yet)

Phase 1: Operational hardening
- Deliver firewall guidance and tested command templates in release docs.
- Add checklist item for allowlisted remote addresses.

Phase 2: Mod deterrence hardening
- Add config-gated host admission proof protocol.
- Add host-side kick enforcement for proof failures.
- Add discovery/log privacy reductions.

Phase 3: Server authoritative gate
- Implement and validate CreateRoom authorization plugin path.
- Roll out with feature flag and fallback to open mode.

## Validation plan for future implementation

For each phase, capture:
- host and client logs from the same attempt,
- first divergent callback/state on failure,
- whether unauthorized host attempt was blocked, delayed, or succeeded,
- one-machine, two-machine, and physically offline-LAN status.

Suggested tests:
1. Baseline regression: normal host/join still succeeds.
2. Unauthorized create attempt from non-allowlisted machine.
3. Unauthorized join attempt against protected room.
4. Endpoint leak test: verify reduced UI/log exposure.
5. Rollback test: disable hardening flags and verify old behavior restores.

## Open questions

1. Does the deployed Luxon/Photon stack in this repo support custom server-side operation hooks for CreateRoom authorization, and at which interception point?
2. Should authorization be per-host user identity, per-device key, or per-session invitation token?
3. Should discovery remain plaintext LAN broadcast, or be split into public metadata plus authenticated join metadata?
4. What host UX is acceptable for invite secret entry/sharing when no central service exists?
5. Which minimum hardening baseline should be required for release: firewall-only, mod deterrence, or server-authoritative gate?
