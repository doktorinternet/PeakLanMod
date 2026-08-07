---
description: Build-check, cut release version, package LAN release, then bump to next -prel version
---

Run the PEAK LAN release flow.

Optional release bump request:

${input:releaseBump:Enter one of: patch, minor, major (default patch)}

Requirements:

1. Validate build first (no version edits yet):
   - Run:
     - `dotnet build PeakLanMod.slnx --configuration Release --no-restore -p:DeployModFiles=false -p:RunThunderPipePackAfterBuild=false`
   - If this fails, stop and report the compile errors.

2. Compute and set the version to be released now:
   - Edit only `src/PeakLanMod/PeakLanMod.csproj` `<Version>`.
   - Accept either `X.Y.Z-prel` or `X.Y.Z` as input state, normalizing to base `X.Y.Z` first.
   - Determine release bump mode from `${input:releaseBump}`. If omitted/invalid, use `patch`.
   - Compute release version from base `X.Y.Z`:
     - `patch`: `X.Y.(Z+1)`
     - `minor`: `X.(Y+1).0`
     - `major`: `(X+1).0.0`
   - Write release version as plain `X.Y.Z` (without `-prel`).

3. Build the release package with the release version:
   - Run:
     - `dotnet build -c Release -t:LanRelease -p:RunThunderPipePackAfterBuild=false`
   - Report generated output paths from the build log.

4. Bump to next development version and append `-prel`:
   - Parse the release version as `major.minor.patch`.
    - Always apply a `patch` bump for post-release development:
       - `major.minor.(patch+1)`
   - Write back `nextVersion-prel` to `src/PeakLanMod/PeakLanMod.csproj` `<Version>`.

Behavioral constraints:

- Preserve existing release workflow and commands; do not change packaging targets.
- Do not modify unrelated files.
- Keep all changes small and reviewable.

Final report format:

1. Build-check result (step 1)
2. Release version used (step 2)
3. Release package build result and artifact locations (step 3)
4. New development version written (step 4, always patch bump)
5. Any manual follow-up needed