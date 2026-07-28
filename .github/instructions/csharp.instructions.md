---
applyTo: "src/**/*.cs"
---

# C# and Harmony requirements

- Match the existing namespace and file organization.
- Keep nullable-reference handling explicit; do not hide warnings with blanket suppression.
- Use `Plugin.Log` or the existing BepInEx logger rather than `Console.WriteLine`.
- Avoid logging secrets or complete identifiers.
- Prefer named methods over large lambdas for Harmony patches and callback handlers.
- Resolve overloaded patch targets explicitly with parameter types.
- Use `PhotonPlayer` as an alias for `Photon.Realtime.Player` wherever PEAK's `Player` type could collide.
- Harmony prefixes that skip original behavior must:
  - be restricted to the intended experimental mode,
  - log that the original was skipped,
  - set `__result` correctly for non-void methods.
- Never add a transpiler unless prefix/postfix, state injection, or configuration changes are demonstrably insufficient.
- Keep the direct Photon Cloud baseline available while implementing local-server support.
- After changes, run `dotnet build` when the local PEAK references are available.
