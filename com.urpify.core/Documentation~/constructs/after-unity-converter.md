# Fixes after Unity's converter

Unity's Render Pipeline Converter is the first thing the migration runs, and it is right most of the time. These are the
cases where its result is measurably wrong, and what the free package does after it. All of them are in the free
package, in the Urpify converters step.

## Standard in Transparent mode

![Lab booth STD04: glass in Standard Transparent mode, white after Unity's converter, glass again after Urpify](../images/STD04.jpg)

*STD04: glass in Standard's Transparent mode.*

Unity's converter maps Transparent to URP's Premultiply blending, which expects colours already multiplied by alpha.
Glass turns almost opaque and white. Urpify switches the material to Alpha blending with Preserve Specular Lighting, which
is how Built-in blends it. Finding: "..., Transparent mode" (`MATERIAL.STANDARD_TRANSPARENT`).

## Legacy Self-Illumin

![Lab booth LEG03: Legacy Self-Illumin, dark after Unity's converter, glowing after Urpify](../images/LEG03.jpg)

*LEG03: `Legacy Shaders/Self-Illumin/Diffuse`.*

Unity's converter moves these materials to Simple Lit, which takes no emission from the Illumin alpha: the glow is gone.
Urpify moves them to its own Self-Illumin shader for URP, which glows as in Built-in. Finding: `MATERIAL.SELF_ILLUMIN`.

## Built-in materials of the Editor

Renderers that use the Editor's built-in `Default-Material` or `Default-Diffuse` (a new cube, a plane) are pointed by
Unity's converter to URP's default material, which is mid-grey instead of white. Urpify points them to URP copies with
the built-in material's values. Finding: `SCENE.BUILTIN_MATERIAL`.

## Materials inside packages

A package's files are read-only, so Unity's converter never changes a material that lives in a package. When its shader
is Built-in only, every renderer that uses it turns pink: ProBuilder 5's walls use `ProBuilderDefault.mat`, whose shader
`ProBuilder/Standard Vertex Color` is a surface shader. Urpify points those renderers, in scenes and prefabs, to a URP
copy in `Assets/Urpify/Materials/Packages/`: Unity's own upgrader for a built-in shader (`Auto`), otherwise URP Lit with
the material's color, texture, smoothness and metallic (`AutoApprox`: what the shader itself adds, such as vertex
colors, is not kept). Finding: `SCENE.PACKAGE_MATERIAL`.

## Built-in shaders without an upgrader

Some Built-in shaders have no upgrader in Unity's converter ("No upgrader found"): `Legacy Shaders/Decal`,
`Legacy Shaders/Transparent/VertexLit`, `Transparent/Parallax Diffuse` and `Specular`, and on URP 14 two upgraders that
are registered under old names. Urpify moves these materials to the nearest URP shader from its own table, with the
properties carried over. Finding: `MATERIAL.BUILTIN_NO_UPGRADER`.

## Post-processing Stack v2

Unity's converter turns PPv2 volumes and profiles into URP Volumes, but in batch mode it does not wait for the asset
search it needs and converts none of them: bloom disappears without an error. Urpify's Unity converter step waits for
the search, then carries the Bloom parameters over with a calibration (threshold and scatter) so the glow is closer to
Built-in's. The rest of the difference is URP's bloom itself, see [How URP looks different](../urp-differences.md).
Finding: `SCENE.PPV2`.

## URP Asset settings

- **Depth Texture and Opaque Texture** are turned on in every URP Asset when a shader URP draws as it is reads them (see
  [Depth texture](depth-texture.md)).
- **URP Asset textures** (URP 14): Unity's converter saves its URP Assets with empty blue-noise and Bayer textures. The
  Editor fills them in, a built player does not: it throws `NullReferenceException` every frame and renders black. Urpify
  fills them in from the URP package. Finding: `SETTINGS.URP_ASSET_TEXTURES`.
- **Main light**: on quality levels whose Pixel Light Count was 0, Built-in still lit the main directional light per
  pixel; Urpify sets URP's Main Light to Per Pixel there.

## Material variants

Unity's converter moves a Material Variant to its parent's URP shader but drops the variant's own values: it becomes a
silent copy of its parent. Urpify reads the variant's own values from the backup of the Unity converter step and puts
them back as overrides of the URP properties. Finding: `MATERIAL.VARIANT`.

## Read-only materials in models

Materials embedded in FBX files are read-only, and Unity's converter's module for them did not run reliably in batch
mode. Urpify runs that module object by object for the renderers and terrains it found, on both Unity versions.
