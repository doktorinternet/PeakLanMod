---
description: Implement one small PEAK LAN milestone while preserving the working baseline
---

Implement this milestone:

${input:milestone:Describe one narrowly scoped behavior change}

Before editing:

- read `.github/copilot-instructions.md`,
- read `docs/research/current-network-findings.md`,
- inspect the relevant skill and current implementation,
- identify the known-good baseline that must remain available.

Requirements:

- make one hypothesis-driven change,
- guard experimental behavior with configuration,
- preserve custom Photon Cloud direct connect,
- avoid proprietary files and identifiers,
- add focused diagnostics,
- run `dotnet build` only if PEAK's local references are available,
- clearly report what was compiled, what remains a manual two-machine test, and the rollback path.
- update changes in CHANGELOG.md with a short description of the change and the date.
- update readme with any new configuration and/or troubleshooting instructions, if applicable.
- update release process, instructions, configuration templates, and/or troubleshooting instructions, if applicable.