# What Unity's converter handles

Unity's Render Pipeline Converter upgrades materials that use Unity's own Built-in shaders. Urpify runs it as the
migration's Unity converter step, with the workarounds it needs to run reliably from a script, and reports its log.

![Lab booth STD01: Standard metallic with normal and metallic maps](../images/STD01.jpg)

*STD01: Standard, metallic workflow, normal and metallic maps. Unity's converter upgrades it to URP Lit.*

![Lab booth TER01: terrain with two layers](../images/TER01.jpg)

*TER01: a terrain with two layers moves to URP's terrain shader.*

What the converter covers, measured on the lab booths STD01 to STD06, LEG01 to LEG05, PAR01, PAR02, FBX01, FBX02 and TER01:

- Standard and Standard (Specular setup) in Opaque, Cutout and Fade modes, with normal, emission and detail maps. Transparent
  mode is the exception, see [Fixes after Unity's converter](after-unity-converter.md).
- Legacy Shaders (Diffuse, Specular, Bumped, Reflective, Transparent, Cutout, Lightmapped, Parallax, VertexLit); Self-Illumin
  is the exception.
- Mobile shaders, Unlit/Texture, Particles/Standard Surface and Unlit, Legacy particles.
- Nature/Terrain/Standard and the default terrain material; SpeedTree.
- Materials embedded in imported models.
- Animation clips that animate Built-in material properties (the scan lists them; Urpify does not verify the result yet).
- Post-processing Stack v2, see [Fixes after Unity's converter](after-unity-converter.md).

Urpify's scan lists these materials as fixed by Unity's converter, so the overview counts them apart from what needs
Urpify.

## Running it reliably

On Unity 6.5, Unity's documented batch command throws `NullReferenceException` without `-typesFilter`, and the Editor
often crashes while it exits after a successful run (exit codes 21, 139). Urpify calls the converter through its API,
judges the result by the converter's log and a rescan rather than by the exit code, and includes the log in the report.
