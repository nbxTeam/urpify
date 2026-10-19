# Quick start

The whole cycle in 5 minutes: scan, migrate, compare. Try it on the demo scene first, then on a copy of your project.

## 1. Install

Install into a project that still uses the Built-in Render Pipeline:

- **Urpify Pro:** one file, `Urpify-Pro-1.0.0.unitypackage` (from the Asset Store: Package Manager > My Assets > Import). Assets > Import Package > Custom Package, import everything: Urpify, Urpify Pro, the demo and this documentation
  are all in it. Free Urpify already installed from a `.unitypackage`: import Pro over it, nothing to remove. Installed
  with the Package Manager: remove it there first (Package Manager > Urpify > Remove), since Urpify Pro contains it.
- **Urpify (free):** `Urpify-1.0.0.unitypackage` from [GitHub Releases](https://github.com/nbxTeam/urpify/releases/latest),
  imported the same way, or Package Manager > `+` > Add package from git URL:
  `https://github.com/nbxTeam/urpify.git?path=com.urpify.core#v1.0.0`.

Commit your project to version control before you migrate. Urpify backs up every file it changes, but a commit is the
safest restore point.

## 2. Open the demo (optional)

Help > Urpify > **Import Demo**, or **Import demo** in the empty Urpify window. The demo goes to `Assets/Samples/Urpify/1.0.0/Demo` and its
scene opens. Installed with the Package Manager, Package Manager > Urpify > Samples > **Demo** > Import does the same. The scene has
one booth per construct that breaks in URP: surface shaders, a lit vert/frag shader, GrabPass glass, water that reads
depth, a projector, a mirror rendered by a script, a material created in code and glass in Standard Transparent mode. Every booth has a
Migration Viewpoint, so the comparison shows each of them up close.

## 3. Scan

Window > Rendering > **Urpify**, then **Scan project**.

Scan only reads files. The list on the left shows every shader, script, material, scene and setting that breaks in URP,
grouped by type. Each row has an icon:

- filled square: renders pink;
- filled triangle: breaks without an error (a portal turns into a wall, water loses its foam, an effect disappears);
- filled diamond: looks different;
- ring: no visible change.

Select a row to see what breaks, why, and who fixes it. A shader opens in the shader panel: its code before and after
conversion, and a preview in Built-in and URP side by side.

## 4. Migrate

Select **Migrate...**. The window lists the steps:

1. **Scan**: the same read-only scan.
2. **Back up**: copies the project's assets, settings and package list to `Urpify/Backup`.
3. **Capture before**: plays each scene in Play Mode and saves frames from the main camera and every Migration Viewpoint.
4. **Install URP**: adds the URP version that ships with your Editor.
5. **Run Unity's converter**: Unity's Render Pipeline Converter, with the workarounds Urpify needs to run it reliably.
6. **Urpify converters**: fixes what Unity's converter leaves broken. With Pro, also converts shaders, scripts and
   components.
7. **Capture after and compare**: the same frames in URP, compared with the frames from before.
8. **Review**: the final report.

Select **Run all**. Each step can also run on its own, and each can be rolled back: **Roll back to here** on a step, or
**Roll back all**.

## 5. Compare

Select **Compare scenes**. Each scene shows its frames before and after with a slider between them. **Show differences**
marks changed pixels: the redder, the larger the change. **Changed only** hides the scenes that look the same.

In the main window, a scene has two verdicts: **Static**, from the scan, and **Observed**, from the frames. A pink object
can be outside every captured frame, so a static verdict can be worse than the observed one. Add a Migration Viewpoint
(an empty GameObject with the `MigrationViewpoint` component) where you want a closer look, and capture again.

## 6. Done, or roll back

Open **Report** > Export HTML for a report you can send to your team. If you do not like the result, **Roll back all**
restores every file the migration changed and moves the files it created into the backup. Files that appeared between
steps are never touched, so your own work stays.
Unity restarts right after a rollback in the window, so it loads the project as it is on disk.

Next: [Migrate a game](scenario-game.md), [Convert a shader](scenario-shader.md),
[Convert an Asset Store package](scenario-asset.md).
