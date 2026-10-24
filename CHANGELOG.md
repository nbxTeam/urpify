# Changelog

All notable changes to Urpify are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and versions follow [Semantic Versioning](https://semver.org/): a patch release fixes, a minor release adds rules,
converters, catalog entries or languages without breaking anything, a major release may change the report schema, the
command line or the project files Urpify writes.

## [1.0.1] - 2026-10-23

### Changed
- Every text of findings, steps, verdicts and the HTML report is written for the person migrating: what breaks, then who
  fixes it or what to do. Details about how Urpify works inside are gone from them. English and the seven translations
  were written again.
- The HTML report says who fixes each item (Unity's converter, Urpify, Urpify Pro, or by hand) instead of the converters'
  tiers. The JSON report is unchanged (schema 5).
- Window: a group in the list shows a tick when everything in it is fixed; the result screen without Urpify's fixes opens
  the comparison on the frame after, so what broke is in sight; the quick start says Urpify Pro installs from one file.
- No deprecation warnings on Unity 6000.6, in Urpify and in the files it adds to a project. Those files and the demo's
  scripts work in Play Mode without a domain reload (Enter Play Mode Options), and the demo's scripts are in the
  `Urpify.Demo` namespace.
- The version shown in reports and in exported shader libraries is Urpify's own, also when Urpify was imported from a
  `.unitypackage`.

### Asset Store version
The Asset Store version follows the Asset Store's rule that a package never adds, updates or removes other packages, and
uses no internal Editor API. The same code otherwise:
- Install URP opens the Package Manager on Universal RP and goes on by itself once you have installed it there. The
  command line asks for URP to be installed first.
- A rollback leaves `Packages/manifest.json` and `packages-lock.json` as they are: URP stays installed and unused, and the
  rollback's message offers to open the Package Manager to remove it. Everything Urpify changed is restored as in the
  other version, but while URP is installed Unity adds URP's own settings files when the project opens (on URP 14:
  `Assets/UniversalRenderPipelineGlobalSettings.asset`, `ProjectSettings/ShaderGraphSettings.asset` and a line in
  `ProjectSettings/GraphicsSettings.asset`), so the project is not byte for byte as before. It still renders with Built-in.
- The window is in English until you choose a language in Urpify's settings.
- Where Unity's batch converter cannot run with public API (Post-processing v2 in use, URP 17.3), the step opens Render
  Pipeline Converter and waits for you to convert there and select Continue. On URP 17.5 and later Unity's read-only
  material converter is left out of the batch run; Urpify's own fix for the Editor's built-in materials covers them.
- On Unity 2022.3 and 6000.0 Unity's list of material upgraders is internal, so the fixes that use it (material variants,
  materials inside models) and, in Pro, the catalog's recipes and package export leave those materials to Unity's converter.

### Fixed
- Pro: shaders that call `FresnelLerp` or `FresnelLerpFast`, declare `UNITY_VPOS_TYPE ... : VPOS`, define their own
  attenuation under `#ifdef DIRECTIONAL` in a ForwardBase pass, or return an output struct with `SHADOW_COORDS` that a
  pass without shadows never sets ("variable 'o' used without having been completely initialized") now convert.
- Before Unity's converter or Urpify converters run from the window, unsaved changes in open scenes are saved on your
  answer, or the step does not start (they used to be saved without asking).
- A long Backup on the command line reports its progress, so the step timeout no longer stops it.
- A damaged `Urpify/state.json` is kept as `state.json.damaged-<time>` with a warning in the Console, instead of being
  overwritten.
- The git check looks only at the Unity project's own folder and gives up after 20 seconds.
- The Migrate window follows a running chain: a step that starts after the one before it shows as running at once, and a
  step that waits for you scrolls into view with its buttons.
- Urpify converters run again in the same Editor session after a failed run compile the rewritten scripts as many times
  as the first run.

## [1.0.0] - 2026-10-18

First release.

### Urpify (free)
- Scan of a project, folder, file or `.unitypackage`: every shader, script, material, scene and setting that breaks in
  URP, including silent breaks, with severity, who fixes it and why.
- Migrate window and command line: Scan, Capture before, Back up, Install URP, Unity's converter (with the workarounds it
  needs in batch mode), Urpify converters, Capture after and compare, Review. Every step is backed up and can be rolled
  back, also after an interruption.
- Compare scenes: frames before and after from the main camera and Migration Viewpoints, with Static and Observed verdicts.
- Fixes after Unity's converter: Standard Transparent, Legacy Self-Illumin, the Editor's built-in materials, Built-in
  shaders without an upgrader, Post-processing v2 bloom, URP Asset settings and textures, material variants, read-only
  materials in models, materials inside packages whose shader URP cannot draw (ProBuilder 5's default), `Shader.Find`
  with Built-in names.
- Shader panel: code, preview in Built-in and URP, usages, license line, Send report.
- JSON and HTML reports (schema 5).
- Languages: English, 简体中文, 日本語, 한국어, Русский, Español, Português (Brasil), Deutsch.
- Demo scene: Help > Urpify > Import Demo, or Import demo in the empty window (a `.unitypackage` inside Urpify when it
  was imported from one; the package sample when installed with the Package Manager).
- Rollback from the window restarts the Editor right after it, so no stale assembly of the migration is left loaded.
- Documentation in all eight languages comes with Urpify (in a `.unitypackage` it is `Assets/Urpify/Documentation`);
  Help > Urpify > Documentation opens it in the window's language.
- The project's files stay safe: a step does not start when the disk cannot hold its backup and says how much space it
  needs; a texture that Unity 2022.3 switches to Normal map while the migration saves materials gets its import settings
  back, with a line in the step's report; files that are not UTF-8 (Windows-1251, Shift-JIS, GBK and others) keep every
  byte outside the code that changes; a `Name_URP` file that is already in the project is never overwritten; a rollback
  keeps the version of each file it replaces in the backup.
- Unity 2022.3 LTS, 6000.0 LTS, 6000.3 LTS, 6000.5 and 6000.6 tested; on 6000.3 Unity's own batch converter fails and Urpify
  runs its converters itself.

### Urpify Pro
- Converters for surface shaders (all lighting models, custom lighting, every `#pragma surface` option, tessellation),
  vert/frag shaders (ForwardBase, ForwardAdd, shadows, vertex-lit, UsePass, extra passes), GrabPass, depth and
  depth-normals textures, image effects (`OnRenderImage`), command buffers on cameras, camera scripts (portals, mirrors,
  reflections), projectors and Tree Creator trees.
- Output: Replace original, Write copy, Keep Built-in support (one shader file for both pipelines).
- Self-check of every converted shader by a render in Built-in and URP; a converted shader that renders pink there is
  put back to its original.
- Image effects: Blit calls in the scripts an `OnRenderImage` calls (a ScriptableObject effect, a manager script) are
  recorded into URP's frame too; an effect whose chain renders a camera, dispatches a compute shader or reads Built-in's
  G-buffer is left as it is (Not supported yet), so the frame is drawn without it instead of going black or white.
- A scene whose frame turns almost one flat colour after the migration puts the screen effects it runs under Review.
- Export of a converted `.unitypackage` for Built-in and URP, with its own copy of the shader library.

### Known issues
These stay in 1.0.1: fixing them would change how rollback or the verdicts work, which needs a full
re-test of both; each item says what to do meanwhile.
- Install URP interrupted (the Editor closed while the package was being added), then rolled back: the project can keep a
  new `ProjectSettings/ShaderGraphSettings.asset`, which Unity writes when the rollback opens the project with URP still
  in the manifest. It holds settings of a package that is no longer there; delete it if you want the project byte for
  byte as before.
- Two converted packages exported from folders with the same name (for example both `Assets/Effects`) put their shader
  library at the same path in a buyer's project, so the second import replaces the first one's library files. With the
  same library version this does no harm; give each package its own root folder name.
- Pro: fur and hair shaders with their own lighting on shell layers can come out darker or sparser in URP while their
  conversion is marked Approx. Compare scenes shows such a scene as Changed; check it there.
- Pro: for a shader that reads the time (an animated one), the self-check renders Built-in and URP at different moments and
  cannot tell a conversion error from the animation, so the conversion stays Approx. Compare scenes captures with a fixed
  time: check the scenes that use animated shaders there.
- Pro on Unity 2022.3: a shader whose first pass has no LightMode and whose second pass draws an outline renders black in
  the self-check and is marked Review, although it renders correctly in the scene.
- Pro: a few object shaders (for example terrain grass with `Cull Off`) are taken for screen effects and are not checked by
  a render; they say "not checked by a render" and stay Approx. Check them in Compare scenes.
- Compare scenes: pink objects under the project's own screen effects or fog are not counted as pink, because the effects
  change the error colour; the scene is Changed and the report can say the pink objects were outside the captured views.
  The scan's list of objects that turn pink is still right.
- Compare scenes: errors the project already had (a missing prefab) can make a scene Changed although its frames match,
  because Unity words the same error differently before and after the migration. The frame comparison in the note is what
  counts.
- If the Editor closes or crashes while Urpify converters compile the rewritten scripts, the project can be left with
  scripts that do not compile, and Unity offers Safe Mode. Choose Ignore, then roll back the step in Urpify (or restore from
  git).
- Changes made outside Unity (in an IDE, by git) to files a step changed, between command-line steps or after a crash,
  can be taken for the step's and rolled back with it. The rollback keeps the version it replaces in
  `Urpify/Backup/<id>/replaced/` and lists it; still, do not edit the project between steps, and commit before migrating.
- A long command-line Scan of a very large project on a slow disk can be stopped by the step timeout. Use
  `-urpifyStepTimeout 0` for such projects.
- After a command-line rollback, the next Editor start puts `ProjectSettings` and `Packages/manifest.json` back as the
  rollback left them, also when git changed them in between. Open the project in Unity once right after a command-line
  rollback.
- After `Urpify/state.json` is damaged (it is kept aside with a warning), the steps show as not run and the rollback
  buttons are gone, while the backups stay in `Urpify/Backup`. Keep the `Urpify` folder out of cloud sync.
- Smaller ones: a rollback restores a file you deleted after the migration; a file another program holds exclusively
  stops a step from starting (the error is in the Console); one scene that fails to capture stops the capture of the
  remaining ones, and two scenes whose paths differ only in `/` and `_` overwrite each other's images; Bake environment
  interrupted by a crash stays running; converting one shader in the shader panel is not part of the migration's
  backups (Roll back all does not undo it; the original is in `Urpify/Backup/ShaderPanel`); exporting a package overwrites
  Urpify's runtime files outside the migration's backup; in a ParrelSync clone (linked `Assets`) the migration changes
  the main project's files; some inserted lines end in LF in a file with CRLF line endings, and Unity warns about
  inconsistent line endings.
- A shader that includes a file outside the project (`../../Shared/Lighting.cginc`) gets the converted copy written next
  to that file, outside the project and its backup.
