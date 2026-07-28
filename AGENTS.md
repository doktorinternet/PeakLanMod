# Agent guidance

The authoritative repository guidance is `.github/copilot-instructions.md`.

Before modifying networking behavior, also read:

- `docs/research/current-network-findings.md`
- `.github/skills/peak-network-research/SKILL.md`
- `.github/skills/runtime-diagnostics/SKILL.md` when logs or runtime failures are involved

Core constraints:

- preserve the verified direct custom-Photon-Cloud baseline,
- make one behavioral change at a time,
- do not commit PEAK binaries, decompiled source, App IDs or personal identifiers,
- do not claim runtime verification without evidence,
- do not fake missing PEAK dependencies in cloud environments.
