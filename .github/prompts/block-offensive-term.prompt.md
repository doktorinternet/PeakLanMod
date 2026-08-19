---
description: Add one or more offensive room-name terms to the fingerprint blocklist
---

Add blocked room-name terms to the fingerprint-based filter.

Input terms to add:
${input:terms:Comma-separated terms or phrases to block}

Before editing:

- read `.github/copilot-instructions.md`,
- read `docs/research/current-network-findings.md`,
- inspect `src/PeakLanMod/Lan/Services/LanIdentityAndValidation.cs`.

Implementation rules (must match current code behavior):

- do not store plaintext blocked terms in source,
- compute fingerprints with the same algorithm as `LanIdentityAndValidation.Fingerprint(string)`:
  - UTF-8 bytes of the exact normalized term,
  - SHA-256 hash,
  - uppercase hex string with no separators,
  - take the first 10 characters,
- normalize each input term before hashing:
  - trim,
  - lowercase with invariant culture,
  - collapse internal whitespace to a single space,
- remove duplicates before inserting,
- add new values to `BlockedHostRoomNameTermFingerprints` only,
- keep comments and style consistent with existing file.

Important matching limitations to preserve:

- detection checks single-token matches,
- detection checks two-token phrase matches (`token + " " + nextToken`),
- do not silently expand behavior beyond those two cases unless explicitly requested.

Required workflow:

1. Parse the input list into terms/phrases.
2. Produce a short mapping table in your response: `normalized term -> fingerprint`.
3. Edit `src/PeakLanMod/Lan/Services/LanIdentityAndValidation.cs` and append only missing fingerprints.
4. Run diagnostics/build:
   - file error check for the edited file,
   - `dotnet build` from repo root when local PEAK references are available.
5. Report:
   - exactly which fingerprints were added,
   - whether compile succeeded,
   - any terms skipped as duplicates or unsupported.

If an input phrase has more than two tokens, warn that it will not be matched by current logic and ask whether to extend matcher behavior in a separate change.
