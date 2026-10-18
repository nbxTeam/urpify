# The Urpify window

`Window > Rendering > Urpify`. The window keeps its own text short; this page explains what its icons, counts
and tags mean. Hover any icon or count in the window for the same explanation.

## Severity icons

Each icon has its own shape, so the colour is never the only cue.

| Icon | Name | What happens after switching to URP |
|---|---|---|
| Filled square (pink) | Renders pink | The object renders with URP's error shader. In a release build it is invisible. |
| Filled triangle (amber) | Breaks without error | No error and no pink, but the effect is gone or wrong: a portal turns into a grey wall, water loses its foam, a post effect disappears. |
| Filled diamond (blue) | Looks different | Renders, with a visible difference, such as a darker scene. |
| Ring (grey) | No visible change | Nothing to do, or Unity's converter handles it as it is. |

A list row, a scene and an asset take the icon of their worst finding.

## The strip under the toolbar

Counts in named groups. Before the migration: `Scenes` and `Assets`, each with an icon and a number per severity: how
many scenes have objects with that severity, and how many assets have it. A scene or asset counts in every category it
has, so the numbers overlap. After the captures, `Captured` adds what the frames show: new pink, changed, unchanged.
After the migration the first group is `Result`: fixed, to check, to do. Every count is a button: select it to list only
what it counts, select it again to list everything. Hover a count for what it counts, and `Last scan` for the scan's file
count and time.

## Scene verdicts

A scene has two verdicts:

- **Static**, from the scan: the worst break among everything the scene uses. A pink object can be outside every captured
  frame, so a static Pink can stand next to an observed Same.
- **Observed**, from the captures before and after the migration: the worst frame. Each frame shows the share of changed
  pixels and of pink pixels after; hover the line for the mean difference and the pink share before. One larger changed
  area (a lost reflection, a lost glow) makes a frame Changed even when few pixels change overall. Errors in the console
  while the scene played after the migration make it Changed too, with the first error shown. When the scan expected a
  silent break that the migration did not fix (Unity's converter only, the free package for a break only Pro fixes, or a
  break Urpify could not convert) and the frames still look the same, the panel says so: check the difference map.

In the before/after view, the part left of the line is before and the part right of it is after. `Show differences`
marks changed pixels: the larger the change, the redder the pixel.

## Who fixes it

The overview counts the broken assets by who fixes them:

- **Unity's converter and Urpify** - Unity's Render Pipeline Converter, then the fixes Urpify runs after it (both in
  the free package).
- **Urpify Pro** (in the free package: **Need Pro**, with a lock) - shaders, scripts and components that only
  the Pro converters translate.
- **By hand or not supported yet** - the project was broken before the migration, the construct has no URP counterpart,
  or Urpify does not convert it yet. The finding says what to do.

A list row says who fixes its asset: `Unity` (Unity's converter), `Urpify` (the free fixes), `Pro` (Urpify Pro; in the
free package a lock and `Pro`), `Manual`. Hover it for how close the result is: it matches the original, it is close to
it, or it is converted but looks different (check it). The `Who fixes` filter selects the same groups. After the
migration the row says what became of the asset instead: `Fixed`, `Check` or `To do`. Before the Urpify converters step
runs, this is what the scan expects; after it, what the step did. The report keeps the converters' own tiers (`Auto`,
`Approx.`, `Review`, `Not yet`, `Manual`).

A shader Urpify converts is checked by a render: before the conversion a sphere with the project's own materials of the
shader is rendered in Built-in, after it in URP, and the two are compared. When a large part of the sphere differs, the
shader is still converted, but it is not called `Auto`: it is `Review`, and the pair of images is in
`Urpify/Reports/self-check/`. "A large part" is more than 25 % of the sphere: Unity's own Standard to URP Lit
conversion changes dark, saturated colours by up to about 20 %, and that is how URP Lit looks, not a fault. Pink, or an
object that hides what is behind it and shows the camera's clear colour, is `Review` at any size.

Some shaders cannot be judged on a sphere and are at most `Approx.`, with the reason in the report; check them in Compare
scenes:

- a screen effect without a material (scripts run it on the whole picture);
- an internal shader under `Hidden/` (scripts draw it with their own data);
- a shader that reads the time (`_Time`): the Built-in render is made before the conversion and the URP one after it,
  so they show different moments of the animation (only pink or a render going dark still counts).

In Unity 2022.3 a shader with a `GrabPass` is rendered with a hidden camera of type Game instead of a preview camera:
URP 14 gives a preview camera no Opaque Texture. Such a camera also runs the renderer features of your URP Asset.

## Show what breaks

`Show what breaks`, on the overview and above the steps in Migrate, shows what URP does to the project on its own before
you fix anything: it backs up the project, captures the scenes in Built-in, installs URP, runs Unity's converter, captures
the scenes again and opens the result. Urpify's own fixes have not run yet. Then choose `Fix with Urpify` (Urpify
converters, the captures again and the report) or `Roll back`. In the free package the fixes are the free ones, and the
result says what needs Urpify Pro.

## Steps in order

A step whose earlier steps are not done cannot run and says why under its description: Capture before needs the backup,
Unity's converter needs URP installed, Urpify converters needs Unity's converter (except with Keep Built-in support, where
the project stays on Built-in), and Capture after needs the frames before and a project that renders with URP. Two
captures made with the same pipeline are never compared: the result says there is nothing to compare.

## The result after the migration

Once Unity's converter has run, the right side shows the result instead of the overview: one line with what matters (for
example `Done. Your scenes look the same as before the switch`), the scene that changed most before and after, then only
what needs you, each with a button, and the fixed assets folded under `Fixed automatically`. The window scans the project
again after every step, so the list shows what is fixed and what is left. Files Urpify adds to the project
(`Assets/Urpify/`, a package's `UrpCompat` folder, `UrpSettingsCheck_*` scripts) are never listed.

## Compare scenes

Every captured scene with its result in words. Select a scene to compare its frames in place, with the slider; `Open
scene` opens it. With one scene the comparison is open from the start. The window always shows the latest comparison,
also after a later scan. Frames are saved as the screen shows them, without transparency, at the Game view's size unless
`Capture size` in the settings says otherwise.

## License tag

A shader whose header names a license (an SPDX identifier such as `MIT` or `CC-BY-NC-4.0`) shows it as a tag next to
its name. A converted copy is a derived work and stays under the same license. A non-commercial license (`-NC`) has an
amber tag: a converted copy of such a shader may not be sold.

## Ambient light (scene panel)

In Unity 6, a player renders a scene that takes its ambient light from the skybox and has no baked lighting data without
that light, in Built-in and in URP alike, so the scene looks darker than in the Editor. The migration does not cause this,
so the finding is informational (a ring icon). (In Unity 2022.3 the player gets the ambient light without baked data, and
Urpify does not report these scenes.) `Bake environment` bakes only the skybox's ambient light and reflections into the
scene's lighting data (lightmaps are not baked, and Auto Generate is turned off for the scene), so the player matches the
Editor. The project is backed up first; `Roll back bake` in the same place, or `Roll back` in the Migrate window,
undoes it.
