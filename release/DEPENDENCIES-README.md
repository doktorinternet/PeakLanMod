# Optional dependency mods

Place additional required dependency mod files in this folder before distribution.

Recommended structure:

- Add each dependency under `dependencies/` with enough context to install it.
- Include exact version and source notes in your release notes.

Example:

- `dependencies/SomeDependencyMod/`
  - `README.md`
  - `plugins/SomeDependency.dll`

Installers should copy dependency plugins/config into their PEAK `BepInEx` directories following each dependency's instructions.
