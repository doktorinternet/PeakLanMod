# Copilot setup for PEAK LAN Mod

Copy these files into the repository root, preserving their paths.

## Recommended use

- Repository-wide rules: `.github/copilot-instructions.md`
- Path-specific rules: `.github/instructions/`
- Specialized context: `.github/skills/`
- Selectable specialist agent: `.github/agents/peak-lan-engineer.agent.md`
- Reusable IDE prompts: `.github/prompts/`
- Cross-agent fallback: `AGENTS.md`
- Verified research baseline: `docs/research/current-network-findings.md`

## Local versus cloud agent

Use a local Copilot agent in the IDE for changes that need to compile against the installed PEAK assemblies or run PEAK.

A cloud agent can still handle documentation, analysis and isolated source changes, but it will normally lack the proprietary local PEAK assemblies. Do not add a cloud setup workflow that uploads or commits those assemblies.

## First tasks for the agent

Good first prompt:

> Use the peak-lan-engineer agent. Review the current connection configuration and propose the smallest change that adds a selectable local Photon Server mode while preserving the working custom Photon Cloud mode. Do not implement until you have listed the exact settings and patch point.

For a runtime failure, invoke the `investigate-network-failure` prompt and attach host/client logs from the same attempt.
