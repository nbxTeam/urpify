# Command line

Everything the window does also runs in batch mode, so a build server can scan a branch, migrate it and fail the build
when a scene turns pink. The free package runs Scan, Capture, Verify, Rollback, every Migrate step (its Urpify converters
step runs the free fixes) and the package analysis. Converting shaders, scripts and components, and exporting a converted
package, need Pro.

```
Unity -batchmode -projectPath <project> -executeMethod Urpify.Cli.Commands.<Command> [arguments]
```

**Do not pass `-quit`.** Each command exits the Editor itself, with its own exit code, after writing that code to a file:
some old projects make Unity crash while it exits after the work is done, so CI should read the result file rather than
trust the process exit code alone.

| Exit code | Meaning |
|---|---|
| 0 | Done. |
| 1 | Error: a wrong argument, a missing file, a step that failed. The JSON result says why. |
| 2 | Done, and found findings at or above `-urpifyFailOn`. |

Every command writes a JSON result (`command`, `step`, `status`, `exitCode`, `error`, `reports`) on success and on any
error, and a plain-text result: the exit code, or for a Migrate step the step and its status, such as `KitConverters Done`
(`Done`, `Skipped`, `Failed`, `TimedOut`). The files:

| Command | JSON result (`Urpify/Reports/`) | Text result |
|---|---|---|
| Scan | `urpify-scan-result.json` | `urpify-scan-result.txt` (both in `-urpifyOutput`, default `Urpify/Reports`) |
| Migrate | `urpify-step-result.json` | `Urpify/urpify-step-result.txt` |
| Capture | | `Urpify/Captures/<label>/urpify-capture-result.txt` |
| Verify | `urpify-verify-result.json` | `Urpify/urpify-verify-result.txt` |
| Rollback | `urpify-rollback-result.json` | `Urpify/urpify-rollback-result.txt` |
| Package | `urpify-package-result.json` | `Urpify/urpify-package-result.txt` |
| BakeEnvironment | `urpify-bake-result.json` | `Urpify/urpify-bake-result.txt` |
| Capture, Migrate or Package with a wrong argument | `urpify-command-result.json` | `Urpify/urpify-command-result.txt` |

## Commands

### Scan

```
-executeMethod Urpify.Cli.Commands.Scan
    [-urpifyOutput <dir>]              default Urpify/Reports
    [-urpifyScope <path>[;<path>...]]  default Assets
    [-urpifyFormat json,html]          default both
    [-urpifyFailOn pink|silent|degraded]
    [-urpifyNoCache]
```

Read-only. Writes the JSON report described in [Report schema](report-schema.md) and a self-contained HTML report.
`urp-migration-report-latest.json` is always written besides the timestamped files. The scan caches results per file in
`Library/Urpify/cache`; `-urpifyNoCache` ignores the cache.

### Capture and Verify

```
-executeMethod Urpify.Cli.Commands.Capture -urpifyLabel <label>
    [-urpifyScenes build|all|<a.unity>;<b.unity>]   default: Project Settings > Urpify > Compare > Scenes
    [-urpifyWidth 960 -urpifyHeight 540 -urpifyFrames 30]
-executeMethod Urpify.Cli.Commands.Verify [-urpifyBefore before] [-urpifyAfter after] [-urpifyFailOn pink|changed]
```

Capture plays each scene in Play Mode and saves frames of the main camera and every Migration Viewpoint to
`Urpify/Captures/<label>/`. Verify compares two labels and writes a report with the observed verdict of each scene.

### Migrate

```
-executeMethod Urpify.Cli.Commands.Migrate -urpifyStep <step>
    [-urpifyShaderOutput replace|write-copy|keep-builtin]       kit-converters; default: the project setting
    [-urpifyGrabPass opaque-texture|after-transparents]          kit-converters; default: the project setting
    [-urpifyScenes ...] [-urpifyWidth ...] [-urpifyHeight ...] [-urpifyFrames ...]   capture steps
    [-urpifyStepTimeout 20]                                      minutes without progress before the step is stopped; 0 = never
```

Steps, in order, one per Editor process: `scan`, `backup`, `capture-before`, `install-urp`, `unity-converter`,
`kit-converters`, `capture-after`, `review`. Install URP adds a package that the next process compiles, which is why
each step runs in its own process. Backup runs before Capture before, which refuses to run without a backup: playing
scenes lets Unity re-save old assets and lets packages create files, and the backup must hold the project as it was.

The steps that change files warn in the log when the git working tree has uncommitted changes.

### Rollback

```
-executeMethod Urpify.Cli.Commands.Rollback [-urpifyToStep backup]
```

Without `-urpifyToStep`, rolls back every step. Files changed after the migration, and files that appeared between
steps, are left as they are and listed in the log.

### Package

```
-executeMethod Urpify.Cli.Commands.Package -urpifyPackage <file.unitypackage> [-urpifyOutput <dir>]
    [-urpifyExport <converted.unitypackage>] [-urpifyShaderOutput replace|keep-builtin] [-urpifyKeepScope]
```

Imports the package into a temporary folder, scans it, and with `-urpifyExport` (Pro) writes the converted package, as
described in [Convert an Asset Store package](scenario-asset.md). The analysis report is always written, also when the
export fails or is refused; the exit code is then 1. The export compiles the converted shaders for URP, so URP must be
installed in the project. `-urpifyKeepScope` leaves the temporary folder for inspection.

### Bake environment

```
-executeMethod Urpify.Cli.Commands.BakeEnvironment -urpifyScene <Assets/a.unity> [-urpifyUndo]
```

Bakes the skybox's ambient light and reflections into a scene that has none, with a backup; `-urpifyUndo` rolls it back.
See the finding "Skybox ambient light not baked".

## Interrupted steps and timeouts

A step that stops half way (the Editor is killed or crashes, or the step fails) keeps a complete record of what it
changed: the next Editor closes it as interrupted, and its changes are part of the rollback. While a step runs,
`Urpify/progress.json` says what it last did (`Step`, `What`, `Done`, `Total`, `UpdatedUtc`). In batch mode a step that
makes no progress for `-urpifyStepTimeout` minutes is stopped like a killed Editor, with the reason in `progress.json`
and in the step's message. Progress is what the step reports and the files it writes, not Unity's log. An Editor with a
window is never stopped.

## Projects whose scripts do not compile

Unity stops a batch run before any command starts ("Scripts have compiler errors"). Add `-ignorecompilererrors`: the
scan then reports the compile errors itself (`PROJECT.SCRIPT_COMPILATION_FAILED`), and the steps that change scripts
leave them alone.

## Example: fail a build when a scene turns pink

```
U="/path/to/Unity"; P=/path/to/project
for step in scan backup capture-before install-urp unity-converter kit-converters capture-after review; do
  "$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Migrate -urpifyStep $step -logFile -
  grep -q " Done\| Skipped" "$P/Urpify/urpify-step-result.txt" || { echo "step $step failed"; exit 1; }
done
"$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Verify -urpifyFailOn pink -logFile -
exit $(cat "$P/Urpify/urpify-verify-result.txt")
```

## Files the steps write

- `Urpify/state.json`: the migration's state, outside `Assets`.
- `Urpify/Backup/<id>/`: one backup per step that changes files, with a manifest of each file's hash before and after.
- `Urpify/Captures/<label>/`: captured frames and `captures.json`.
- `Urpify/Reports/`: scan, verify and review reports; `kit-converters.json`, the Urpify converters step's own report
  (per file: `Path`, `Converter`, `Outcome`, `Notes`; the URP Asset changes; the URP settings the converted files need);
  `self-check/`, the Built-in and URP renders of each converted shader.

Add `Urpify/` to version control if you want the migration's history in it; nothing in a build reads it.
