# Tree Creator and soft-occlusion trees

The lab has no booth for trees; they were checked on ShaderTutorials (ronja-tutorials) with the Nature Starter Kit trees.

## What breaks

URP has no Tree Creator shaders (`Nature/Tree Creator Bark`, `Leaves`, `Leaves Fast`, `Optimized`) and no soft-occlusion
tree shaders. Trees made with them render pink. Terrain grass and detail meshes are not affected: URP draws them with its
own detail shaders.

Finding: "..., no URP version" (`MATERIAL.BUILTIN_TREE`).

## What Urpify does (Pro)

Unity publishes the sources of its Built-in shaders under the MIT license. Urpify Pro converts Unity's own 8 tree shaders
with its regular converter, adds the functions of `TerrainEngine.cginc` they need (wind, bending, billboards) to its
library, and moves the bark and leaf materials, including those inside prefabs, to the converted shaders. The license
text is in Pro's `Third Party Notices.md`.

## How close

`AutoApprox`. The trees are no longer pink and keep their materials. Wind animation and billboards under URP were looked
at on a few seconds of frames, not measured pixel by pixel; compare your scenes in Compare scenes. SpeedTree assets are
upgraded by Unity's converter, not by Urpify.
