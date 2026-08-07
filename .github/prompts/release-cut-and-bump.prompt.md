## description: Build-check, cut release version, package LAN release, then bump to next -preview version

Run the PEAK LAN release flow.

Optional release bump request:

${input:releaseBump:Enter one of: patch, minor, major (default patch)}

Version model:

* `src/PeakLanMod/PeakLanMod.csproj` `<Version>` is the authoritative full SemVer/package version.
* It may contain prerelease suffixes such as:

  * `X.Y.Z-preview`
  * `X.Y.Z-preview.N`
  * `X.Y.Z-rc.N`
  * `X.Y.Z`
* BepInEx plugin metadata must use only the numeric SemVer core `X.Y.Z`.
* User-facing display version is derived from assembly informational version and should reflect the full `<Version>`.
* Do not write prerelease suffixes directly into BepInEx plugin metadata.

Requirements:

1. Validate build first (no version edits yet):

   * Run:

     * `dotnet build PeakLanMod.slnx --configuration Release --no-restore -p:DeployModFiles=false -p:RunThunderPipePackAfterBuild=false`
   * If this fails, stop and report the compile errors.

2. Read and normalize the current development version:

   * Read only `src/PeakLanMod/PeakLanMod.csproj` `<Version>`.
   * Accept:

     * `X.Y.Z-preview`
     * `X.Y.Z-preview.N`
     * `X.Y.Z-rc.N`
     * `X.Y.Z`
   * Normalize to numeric core `X.Y.Z` before computing the release version.
   * Do not modify BepInEx version constants or metadata directly.

3. Compute and set the version to be released now:

   * Determine release bump mode from `${input:releaseBump}`.
   * If omitted or invalid, use `patch`.
   * Compute release version from normalized base `X.Y.Z`:

     * `patch`: `X.Y.(Z+1)`
     * `minor`: `X.(Y+1).0`
     * `major`: `(X+1).0.0`
   * Write release version as plain `X.Y.Z` to `<Version>`.

4. Validate release-version metadata:

   * Build enough of the project to verify:

     * `.csproj <Version>` is the release version,
     * BepInEx plugin metadata resolves to numeric `X.Y.Z`,
     * assembly informational version resolves to the same full release version.
   * If BepInEx metadata contains prerelease text or otherwise fails numeric parsing, stop before packaging.

5. Build the release package:

   * Run:

     * `dotnet build -c Release -t:LanRelease -p:RunThunderPipePackAfterBuild=false`
   * Report generated output paths from the build log.

6. Bump to next development version:

   * Parse the release version as `major.minor.patch`.
   * Always apply a patch bump for post-release development:

     * `major.minor.(patch+1)`
   * Write:

     * `major.minor.(patch+1)-preview`
       to `src/PeakLanMod/PeakLanMod.csproj` `<Version>`.

7. Validate post-release development metadata:

   * Verify that:

     * `.csproj <Version>` contains the full `-preview` version,
     * BepInEx plugin metadata resolves to numeric core only,
     * user-facing assembly informational/display version retains `-preview`.

Behavioral constraints:

* Preserve existing release workflow and commands.
* Do not change packaging targets.
* Do not modify unrelated files.
* Do not duplicate version strings manually if the project/build system can derive them.
* Keep BepInEx numeric version and display/package SemVer clearly separated.

Final report format:

1. Build-check result
2. Original development version
3. Normalized numeric base version
4. Release version used
5. BepInEx plugin version verified
6. Assembly/display version verified
7. Release package build result and artifact locations
8. New development version written
9. Any manual follow-up needed
