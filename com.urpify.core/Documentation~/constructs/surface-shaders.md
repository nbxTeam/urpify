# Surface shaders

![Lab booth S01: a Standard surface shader, pink after Unity's converter, the same as Built-in after Urpify](../images/S01.jpg)

*Lab booth S01: a surface shader with the Standard lighting model, albedo, normal map and occlusion.*

## What breaks

`#pragma surface` is a Built-in feature: Unity generates the shader's passes (ForwardBase, ForwardAdd, ShadowCaster,
Deferred, Meta) from the surface function. URP has no surface shaders and renders those passes with the error shader,
so every object with the shader turns pink. Unity's Render Pipeline Converter does not touch custom shaders.

Finding: "Surface shader, lighting ..." (`SHADER.SURFACE`), severity Renders pink.

## What Urpify does (Pro)

The converter writes the passes URP needs around the author's surface function, which it copies as it is:

| Pass | From |
|---|---|
| `UniversalForward` | the surface function, then URP's lighting for the lighting model |
| `ShadowCaster` | the same vertex code, with URP's shadow bias |
| `DepthOnly`, `DepthNormals` | the same vertex code; used for the depth texture, depth priming and SSAO |
| `Meta` | the surface function's albedo and emission, for lightmap baking |

| Lighting model | URP lighting |
|---|---|
| `Standard`, `StandardSpecular` | `UniversalFragmentPBR`, metallic or specular workflow |
| `Lambert`, `BlinnPhong` | Built-in's `LightingLambert` and `LightingBlinnPhong` from Urpify's library, called for the main light and each additional light |
| Your own `Lighting<Name>` | your function, see [Custom lighting](surface-custom-lighting.md) |

The `Input` struct is filled as Built-in fills it: `uv_` and `uv2_` coordinates with the texture's tiling, `worldPos`,
`worldNormal`, `worldRefl`, `viewDir`, `screenPos`, `color : COLOR`, `facing : VFACE`, `INTERNAL_DATA` with
`WorldNormalVector`, and fields your `vertex:` function writes. Material properties move into `UnityPerMaterial` for the
SRP Batcher, and the shader's name and property names stay, so materials keep their values.

Several `SubShader` blocks and LODs keep their meaning. A `Fallback` to a Built-in shader points to the matching URP
shader.

## How close

`Auto` for Standard, StandardSpecular, Lambert and BlinnPhong: the result matches Built-in within the differences of URP's
own lighting (see [How URP looks different](../urp-differences.md)). Each converted shader is also rendered on a sphere
in Built-in and URP; more than 25 % of the sphere changed marks it `Review`.

![Lab booth S02: StandardSpecular](../images/S02.jpg)

*S02: StandardSpecular.*

![Lab booth S04: BlinnPhong with alpha:fade](../images/S04.jpg)

*S04: BlinnPhong with `alpha:fade`, a transparent sphere.*
