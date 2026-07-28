---
name: runtime-diagnostics
description: Diagnose PEAK/BepInEx runtime failures, Photon callback ordering, disconnect causes, scene-loading stalls, Harmony patch failures, or differences between host and client logs.
---

# Runtime diagnostics

Use evidence-first debugging.

## Required inputs

Prefer logs from both host and client from the same test attempt. Establish:

- exact test start,
- room name fingerprint,
- host/client role,
- connection mode,
- region or local server destination,
- first input action,
- first divergence,
- final disconnect cause or exception.

## Analysis format

Produce:

1. concise event timeline,
2. first confirmed divergence,
3. facts,
4. hypotheses ranked by likelihood,
5. smallest next instrumentation or experiment,
6. what result would confirm or reject each hypothesis.

Never classify a timeout, kick, plugin rejection and client-logic disconnect as equivalent.

## Instrumentation rules

- Prefer Photon callbacks for precise lifecycle transitions.
- Add stack traces to explicit `Disconnect`, `LeaveRoom`, state changes, or relevant PEAK wrapper methods.
- Patch exact overloads.
- If an override fails to compile, check for type collisions, especially PEAK `Player` versus `Photon.Realtime.Player`.
- Keep instrumentation non-invasive unless the task explicitly requests a behavior experiment.
- For a bypass experiment, gate it behind config and label it diagnostic-only.

## Helper script

To extract networking-related lines from a BepInEx log, run:

```powershell
./.github/skills/runtime-diagnostics/summarize-log.ps1 -Path <log-file>
```

Review the full log around any extracted error before drawing a conclusion.
