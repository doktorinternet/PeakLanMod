---
description: Diagnose one PEAK host/client networking failure from logs before changing behavior
---

Investigate this PEAK multiplayer failure:

${input:symptom:Describe the visible symptom and which attempt it occurred on}

Use the attached host and client logs from the same attempt. Read the repository instructions and the runtime-diagnostics skill.

Do not modify code initially.

Return:

1. a timestamped host/client timeline,
2. the first confirmed divergence,
3. confirmed facts versus hypotheses,
4. likely causes ranked by evidence,
5. the smallest instrumentation or one-variable experiment,
6. the exact expected result for confirming or rejecting each hypothesis.

Pay particular attention to Photon callback ordering, disconnect cause, room operation return codes, state-machine transitions, scene loading and whether a later retry is merely a consequence of the first failure.
