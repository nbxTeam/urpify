# Report schema

The scanner writes a JSON report and a self-contained HTML report with the same content.
This page describes the JSON, `schemaVersion` **5** (version 4: no fix tier `Review`, no `observedNote`, `renderErrors`,
`largestAreaPct`; version 3: no fix tier `NotSupportedYet`; version 2: no `packageFile` / `packageExport`; version 1: summary
counts were exclusive by the worst finding; no observed verdicts).

- Written by `Scan` in the window (Report > Export JSON) and by the command line
  (`-executeMethod Urpify.Cli.Commands.Scan`, see below).
- Enums are written as strings. New fields may be added without changing `schemaVersion`;
  renaming or removing a field, or changing its meaning, increments it.
- Paths are project-relative with forward slashes.

## Top level

| Field | Type | Meaning |
|---|---|---|
| `schemaVersion` | int | Version of this schema. |
| `generatedUtc` | string | ISO 8601 time of the scan, UTC. |
| `unityVersion` | string | Editor version that ran the scan. |
| `urpVersion` | string | Installed URP package version, `""` when URP is not installed. |
| `kitVersion` | string | Urpify version. |
| `projectName` | string | Name of the project folder. |
| `scope` | string | `Project`, `Folder`, `File` or `Package` (a `.unitypackage`). |
| `scopePaths` | string[] | Folders or files that were scanned. |
| `scanSeconds` | number | Wall-clock scan time. |
| `filesScanned` | int | Shaders, scripts, materials, models, scenes and prefabs in scope. |
| `filesFromCache` | int | Files whose results came from the cache in `Library/Urpify/cache`. |
| `cancelled` | bool | True when the scan was cancelled; the report then covers only part of the scope. |
| `timings` | string[] | Seconds per scan phase, `"phase=seconds"`. |
| `compareBefore`, `compareAfter` | string | Capture labels the observed verdicts compare; empty without Verify. |
| `packageFile` | string | Scope `Package`: the `.unitypackage` file; `""` otherwise. Paths in a package report are the package's own (`Assets/...` as the package imports them), not the temporary folder Urpify analysed it in. |
| `packageExport` | object | Scope `Package` with an export of the converted package (Pro); `file` is `""` without one. Below. |
| `summary` | object | Counts, below. |
| `scenes` | object[] | One verdict per scene in scope. |
| `assets` | object[] | One roll-up per asset that has findings. |
| `findings` | object[] | Every finding. |

## Package export

| Field | Type | Meaning |
|---|---|---|
| `file` | string | The converted `.unitypackage` written. |
| `output` | string | `ReplaceOriginal` (URP-only shaders) or `KeepBuiltInSupport` (one file for both pipelines). |
| `replaced` | int | Assets of the package whose content was replaced (converted shaders, scripts, materials). GUIDs and paths never change. |
| `added` | int | Files added: converted copies of include files, Urpify's shader library and runtime, the buyer's check. |
| `urpSettings` | string[] | What the converted package needs in the importing project, the same list the log and the buyer's check use: URP Asset settings (`DepthTexture`, `OpaqueTexture`, `DepthBeforeTransparents`), fields of the package's Built-in Camera Hooks renderer feature (`ExtraPasses`, `GrabAfterTransparents`, `Projectors`, `DepthNormalsTexture`), and `Hooks` when converted scripts need the feature. |
| `shaders` | object[] | Every converted or examined file (shaders, materials, scripts): `path`, `converter`, `outcome` (`Auto`, `AutoApprox`, `LooksDifferent` = converted but the self-check render differs, `NotSupportedYet`, `Manual`, `Restored` = converted but did not compile, so the original was kept, `Info`), `notes`. |

## Severity and fix tier

`severity` describes the state **after switching to URP and running Unity's Render Pipeline Converter**:

| Value | Meaning |
|---|---|
| `Pink` | Renders with the error shader in the Editor and development builds; not drawn in release builds. |
| `Silent` | Not pink, but the effect disappears or is distorted without any error. |
| `Degraded` | Renders with a noticeable difference. |
| `Info` | No visible change. |

`fixTier` says who fixes it:

| Value | Meaning |
|---|---|
| `None` | Nothing to fix. |
| `UnityConverter` | Unity's Render Pipeline Converter handles it. |
| `Auto` | Urpify converts it automatically with a practically identical result. Who does it, the Free package (the fixes after Unity's converter) or Pro (shaders, scripts, components), depends on the rule; the window marks what needs Pro with a lock. |
| `AutoApprox` | Urpify converts it; close to the original, not pixel-identical. |
| `Review` | Urpify converted it, but its self-check render (a sphere with the project's own materials, Built-in against URP) looks different: check it. Set from the Urpify converters step's outcome `LooksDifferent`. |
| `NotSupportedYet` | Urpify does not convert this yet; it is planned. `manualSteps` explains how to do it until then. |
| `Manual` | A person has to act, in two cases only: the project was broken before the migration (a shader that does not compile in Built-in, a missing include or shader, a file that is not a shader), or the construct has no URP counterpart or need. `manualSteps` explains what to do. |

Before the Urpify converters step the tier is the scanner's estimate. Once the step is done, the report of the Review step and
the window use what the step actually did for every file it handled (`kit-converters.json`), so the report, the list and
the panels always show one tier per finding.

## summary

| Field | Meaning |
|---|---|
| `scenes.total`, `.pink`, `.silent`, `.degraded`, `.unaffected` | Scenes that have at least one break of that severity. **Counts overlap**: a scene with a pink material and a silent script counts in both. `unaffected` = no break at all. |
| `assets.withFindings`, `.pink`, `.silent`, `.degraded`, `.info` | Assets with at least one finding of that severity (overlap as above); `info` = assets whose findings are all Info. |
| `observed.pink`, `.changed`, `.same`, `.notCaptured` | Scenes by observed verdict (exclusive: the worst captured frame). |
| `fixTiers.unityConverter`, `.auto`, `.autoApprox`, `.review`, `.notSupportedYet`, `.manual` | Assets by the tier that decides their fix. |
| `kitNeeded` | Assets that break and that Unity's converter does not fix. |
| `kitConvertible` | Of those, assets Urpify converts (`Auto`, `AutoApprox` or `Review`). |
| `parseFailures` | Findings with rule `PARSE.FAILED`. |

## scenes[]

| Field | Meaning |
|---|---|
| `path` | Scene asset. |
| `severity` | Worst severity among the scene's own findings and the findings of every asset it uses (materials, shaders, scripts on its objects, prefabs). |
| `fixTier` | Tier that decides the fix of the worst findings. |
| `causes` | Assets whose findings produced the verdict. |
| `rules` | Rule ids behind the verdict. |
| `breaks` | Every breaking rule in the scene as `"<Severity> <RuleId>"`, worst first. A `Pink` verdict can hide `Silent` breaks; this list shows them. |
| `observed` | `NotCaptured`, `Same`, `Changed` or `Pink`: what the before/after captures show (worst viewpoint). Independent of the static verdict above, which is never softened. |
| `pinkNotInFrame` | The static verdict is `Pink`, but no captured frame shows new pink: the pink objects were outside the captured views. |
| `observedNote` | What the comparison adds: errors during rendering, a capture problem, or "Check: a silent break was expected here" when the static verdict has a silent break and the frames look the same. For "Shader properties can't be added to this global property sheet" it names the shaders of the scene that are not converted and have a GrabPass into that texture. |
| `renderErrors` | Errors and exceptions in the console while the scene played after the migration that the capture before did not have (first line each, `(xN)` when repeated). A scene with any is at least `Changed`. |
| `viewpoints[]` | Per camera or Migration Viewpoint: `name`, `verdict`, `pinkBeforePct`, `pinkAfterPct` (share of pure-magenta pixels), `meanDiff` (0..255), `changedPixelsPct` (pixels whose largest channel difference is above 48), `largestAreaPct` (the largest connected changed area, percent of the frame), and the `before`, `after`, `diff` image paths. |

## assets[]

`path`, `kind` (`Shader`, `Script`, `Material`, `Scene`, `Prefab`, `Settings`, `Project`), `severity`, `fixTier`,
`findingCount`, `usedBy` (materials, scenes and prefabs that use the asset).

## findings[]

| Field | Meaning |
|---|---|
| `ruleId` | Stable id of the construct, e.g. `SHADER.SURFACE.CUSTOM_LIGHTING`. Never renamed after release. |
| `severity`, `fixTier` | As above. |
| `kind` | Asset kind. |
| `assetPath` | Asset the finding is about. For materials inside a model file, the model file. |
| `line` | 1-based line in the asset, `0` for the whole asset. |
| `title` | Short name of the construct. |
| `explanation` | What breaks and why. |
| `manualSteps` | For `Manual` findings: what to do. |
| `evidence` | The source line or object path that triggered the rule. For code in an include file: `path:line`. |
| `converterId` | Reserved for Pro converters; empty in this version. |
| `catalogId` | The catalog entry the asset was recognized as (`Catalog/catalog.json` in the package, for example `seblague-portals/Portal.cs`); empty when the catalog does not know it. The asset also gets a `CATALOG.KNOWN` finding (`Info`) with the entry's name, source, license, how it matched (`evidence`: `Exact`, `Renamed` — the same code with other names or property defaults — or `Similar 0.87`, the share of code in common) and what was checked for it. |
| `usedBy` | Materials, scenes and prefabs that use the asset. |

## Observed verdict

| Verdict | Rule |
|---|---|
| `Pink` | Pink share of the frame grew by more than 0.02 % (TZ section 10). Pink is the error shader's exact colour: red and blue at least 250, green at most 5; a scene's own magenta objects do not count. |
| `Changed` | More than 5 % of the pixels changed by more than 48 in some channel, or one connected changed area is larger than 1 % of the frame, or the console had errors while the scene played that the capture before did not have. |
| `Same` | Otherwise. |

The 48 and 5 % come from Urpify's test lab: Standard and Legacy materials that Unity's converter switches to URP Lit
(the reference of an acceptable difference) change at most 3.8 % of pixels at 48 even when they fill the frame. The
connected area is counted on 8x8 blocks (a block counts when a quarter of its pixels changed), so noise spread over the
frame does not add up: frames that are the same after the migration stay at or under 0.67 %, while a lost reflection or a
lost glow is one area of 1 to 3 %.

## Command line

The commands that write these reports, their arguments and exit codes are on the [Command line](cli.md) page.
