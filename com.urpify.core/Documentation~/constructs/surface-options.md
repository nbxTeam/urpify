# Surface shader options

`#pragma surface surf Standard <options>` changes what Unity generates. Urpify reads every option and keeps its meaning
in the passes it writes (Pro).

![Lab booth S03: Lambert with a vertex: wind function](../images/S03.jpg)

*S03: `vertex:vert` bends the mesh like wind. The vertex function runs in every pass, so shadows bend too.*

![Lab booth S08: dissolve with clip() and addshadow](../images/S08.jpg)

*S08: a dissolve with `clip()` and `addshadow`: the shadow is cut the same way.*

![Lab booth S10: decal:add](../images/S10.jpg)

*S10: `decal:add`, an additive overlay drawn after the opaque objects.*

![Lab booth S11: finalcolor: height fog](../images/S11.jpg)

*S11: `finalcolor:` height fog, applied after lighting as in Built-in.*

## Options and what they become

| Option | In URP |
|---|---|
| `vertex:func` | Runs in every generated pass: forward, shadow, depth, depth-normals, meta. Extra `Input` fields it fills are carried to the surface function. |
| `finalcolor:func` | Called on the lit colour, before fog. |
| `finalgbuffer:func` | Not used: it only runs in Built-in's deferred G-buffer pass. A note says so. |
| `alpha`, `alpha:blend`, `alpha:fade`, `alpha:premul` | Transparent queue and the matching blend; `premul` keeps premultiplied specular. |
| `alphatest:prop` | `clip()` against the property, in every pass. |
| `keepalpha` | The surface alpha is written in opaque passes. |
| `decal:add`, `decal:blend` | Drawn after the opaque objects with additive or alpha blending, no shadows. |
| `addshadow` | The shadow pass runs the vertex function and the surface clip, as the forward pass does. |
| `noshadow` | No shadow caster pass; the forward pass does not receive shadows. |
| `noambient`, `novertexlights`, `nolightmap`, `nofog`, `nometa`, `noforwardadd` | The matching part is left out: ambient light, per-vertex lights, lightmaps, fog, the Meta pass, additional lights. |
| `halfasview`, `interpolateview`, `approxview`, `dualforward` | View-direction quality switches URP does not need; ignored with a note. |
| `fullforwardshadows`, `nodynlightmap`, `nodirlightmap`, `nolppv`, `noshadowmask`, `noinstancing`, `exclude_path:*`, `dithercrossfade` | Recognized; URP's own settings decide these (additional light shadows, lightmap modes, light probe proxy volumes, LOD cross-fade), so they change nothing in the converted shader. |
| `tessellate:func`, `tessphong:` | See [Tessellation](surface-tessellation.md). |

An option Urpify does not know is listed in the finding "Unsupported surface options" (`SHADER.SURFACE.UNKNOWN_OPTION`);
the rest of the shader is still converted.

The Shadows setting (Project Settings > Urpify) can force shadows on or off for every converted surface shader.
