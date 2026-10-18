# Migrate a game

You have a game on the Built-in Render Pipeline and want it on URP, looking the same.

## Before you start

- Commit the project. Urpify backs up what it changes, but a commit is the restore point you trust most.
- Make sure the project compiles and enters Play Mode in Built-in. Urpify reports script compile errors as a finding:
  they block Play Mode, and so the captures.
- Decide which scenes to compare: Project Settings > Urpify > Compare > Scenes. By default, the scenes in Build Settings.
- Place a few Migration Viewpoints in scenes where you know an effect matters: a portal, a mirror, water, a post effect.
  Add the `MigrationViewpoint` component to an empty GameObject and point it like a camera. It is tagged EditorOnly
  and never reaches a build.

## Scan and read the overview

Window > Rendering > Urpify > **Scan project**. The overview leads with the scenes that break, then counts the broken
assets by who fixes them:

- **Unity's converter and Urpify**: built-in shaders that Unity's Render Pipeline Converter upgrades, and the cases
  where its result is wrong, which the free package fixes after it.
- **Urpify Pro**: custom shaders, image effects, camera scripts, command buffers and projectors.
- **By hand or not supported yet**: the project was broken before the migration, or URP has nothing to map the construct
  to, or Urpify does not convert it yet. See [What is not converted](not-converted.md).

The list on the left can be filtered by issue, fix and type, and searched by file name or rule id. **Fixed without Pro**
shows what the free package handles.

## Migrate

**Migrate...** > **Run all**, or step by step. The steps that change files back them up first. While a step runs,
the window shows its progress, and any step can be run again. If the Editor
closes in the middle of a step, the next time the window opens it offers to run the step again or roll it back.

## Compare and review

After **Capture after and compare**, open **Compare scenes**:

- **Same**: the frames match within the thresholds calibrated on Standard to URP Lit.
- **Changed**: some pixels differ, or one area changed a lot (a lost reflection, a lost glow), or the scene logged errors
  while it played.
- **New pink**: an object renders with the error shader.

Read each Changed scene with **Show differences**. Some differences are URP, not the migration: see
[How URP looks different](urp-differences.md).

The final report (**Review**) lists every converted file with its tier: `Auto` (practically identical), `Approx.`
(close, not pixel-identical), `Review` (converted, but Urpify's own test render differs from the original: check it), and
what needs work by hand.

## Roll back

**Roll back all** restores the project as it was before the first step. **Roll back to here** on a step undoes every step
after it. Files you changed after the migration are left as they are and listed in the Console.

## In a build pipeline

Every step runs from the command line, so a build server can migrate a branch and fail the build when a scene turns
pink. See [Command line](cli.md).
