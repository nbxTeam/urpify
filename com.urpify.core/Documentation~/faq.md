# FAQ

## Does Urpify change my project when I scan?

No. Scan only reads files. Changes happen only in the Migrate window's steps that say so, and in the shader panel's
Convert, each with a backup first.

## How much of my project will convert?

The scan tells you before anything changes: the overview counts the broken assets by who fixes them.
The product page lists what Urpify converted in an independent test on shaders its developers did not see.

## Can I undo a migration?

Yes. **Roll back all** restores every file the migration changed and moves the files it created into the backup. Files
you changed after the migration, and files that appeared between steps, are left as they are and listed. A step that was
interrupted is rolled back like a finished one. Commit your project first anyway: a commit is the restore point you trust
most.

## Do I need Pro?

The free package finds everything that breaks, runs Unity's converter reliably, fixes what Unity's converter gets wrong,
compares scenes before and after, and rolls back. Pro converts what the free package cannot: custom shaders, image
effects, camera scripts, command buffers and projectors. The overview shows how many of your broken assets need Pro
before you buy it.

## Will my custom shader code be rewritten?

Only its wiring. The author's functions (`surf`, `vert`, `frag`, lighting functions, helpers) are copied as they are; what
changes is how the shader gets lights, shadows, depth, the screen picture and matrices. Built-in names keep working
through Urpify's compatibility library. The shader's name and property names stay, so materials keep their values.

## What if a converted shader looks wrong?

Every converted shader is rendered in Built-in and URP on a sphere with your materials. When it differs, it is marked
`Review` and the pair of images is in `Urpify/Reports/self-check/`. Open it in the shader panel: the Preview tab shows
Built-in and URP side by side, and the Code tab shows every change with the reason. You can edit and recompile the result
there, or roll the shader back with the migration.

## Does it work with my Unity version?

Tested on Unity 2022.3 LTS, 6000.0 LTS, 6000.3 LTS, 6000.5 and 6000.6; 6000.1, 6000.2 and 6000.4 are not tested. See [Unity and URP versions](urp-version-matrix.md).

## Does it work in CI?

Yes. Every step runs from the command line and writes a JSON result, see [Command line](cli.md).

## Does Keep Built-in support make builds bigger?

A little. On the lab scene (Unity 6.5, Mono), the player's data was 32.5 MB in Built-in, 38.2 MB in URP with URP-only
shaders, and 38.5 MB with shaders for both pipelines: 0.34 MB, or 0.9 %, more.

## Do converted image effects and projectors cost more in URP?

They run through Urpify's renderer feature, which records the same draws into URP's frame. A projector costs about
0.02 ms per frame on a small scene; with 5,000 visible renderers, about 2.2 ms. Built players keep the effects, in Mono
and IL2CPP builds.

## What does Send report send?

Nothing, until you press Send. Report > Send report... packs the selected shader, its include files, the findings, the
Unity, URP and Urpify versions, the compiler errors, and your comment and contact if you give them. The window lists
every file before anything is sent. **Send** uploads the archive to our support channel; **Save zip instead** saves it on
your computer. The files are used only to find out what the converter is missing and are deleted afterwards. This is the
only network call in Urpify. One report a minute is allowed.

## Can I sell shaders converted with Urpify?

Yes, under their own licenses. See [Redistributing converted shaders](license-redistribution.md). A shader whose header
states a non-commercial license stays under it; the shader panel shows the license line.

## Why does the scan say my project was broken before the migration?

Because it was: a shader that does not compile in Built-in, scripts that do not compile, an include file that is not in
the project. Urpify reports these separately, since migrating cannot fix them. Fix them first, then scan again.

## After rolling back URP, shaders show include errors once. Why?

After URP is removed from a project that had it (a rollback, or switching back by hand), the first Editor session can
still compile the URP SubShader of shaders made with Keep Built-in support and log include errors, from import results
left in `Library/Artifacts`. The second session has none. This is Unity's import behaviour, not the shader files: a
project that never had URP shows no errors. Reopen the project, or run Assets > Reimport All.

## The window is in the wrong language.

Project Settings > Urpify > Personal > Language. By default it follows the Editor's language and uses English when Urpify
does not have it.
