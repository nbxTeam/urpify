# Custom lighting functions

![Lab booth S05: a toon ramp lighting function, pink after Unity's converter, the same after Urpify](../images/S05.jpg)

*Lab booth S05: a surface shader with its own lighting function, a toon ramp.*

## What breaks

A surface shader can name its own lighting model, `#pragma surface surf Toon`, and define `LightingToon`. Built-in calls
it once per light: in ForwardBase for the main light and in ForwardAdd for each additional light, with
`_LightColor0` and the light's attenuation. URP has no surface shaders, so the object is pink.

Finding: "Custom lighting function ..." (`SHADER.SURFACE.CUSTOM_LIGHTING`).

## What Urpify does (Pro)

The author's function is copied unchanged. The generated forward pass calls it for URP's main light and then for each
additional light, with URP's shadows and attenuation, and adds the results, as Built-in's two passes did. Inside the
function, `_LightColor0` is the colour of the light it is called for.

All of Built-in's signatures are recognized:

| Signature | Example |
|---|---|
| Without view direction | `half4 LightingToon(SurfaceOutput s, half3 lightDir, half atten)` |
| With view direction | `half4 LightingToon(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)` |
| With global illumination | `half4 LightingToon(SurfaceOutput s, half3 viewDir, UnityGI gi)` and its `LightingToon_GI` |
| Deferred and prepass variants | `_Deferred`, `_PrePass`: not used, URP renders forward |

The function is found wherever the compiler would find it: in the program, in `CGINCLUDE` blocks, in include files of
the project at any depth, and in `#define` macros that declare functions.

## How close

`AutoApprox`. Built-in adds each additional light in its own pass with additive blending; URP lights them in one pass.
The sums match for opaque objects; transparent shaders can differ where lights overlap. Ambient light comes from URP's
spherical harmonics as in Built-in.

If the function is not in the shader or the scanned include files, the finding names it, and the shader is not converted
until its include file is in the scan scope.
