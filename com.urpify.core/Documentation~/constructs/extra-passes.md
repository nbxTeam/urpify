# Extra passes: outlines, shells, layers

![Lab booth S12: a toon shader with an inverted-hull outline pass](../images/S12.jpg)

*Lab booth S12: a toon surface shader with a hand-written outline pass. After Unity's converter alone only the outline
survives around a pink sphere.*

## What breaks

URP draws at most two passes of a SubShader for an object: one `SRPDefaultUnlit` and one `UniversalForward`, in that
order. Built-in draws every pass. A shader with an outline pass next to its lighting pass, fur shells, hatching layers,
or two passes without a LightMode tag loses all but one of them; often silently.

Findings: "Extra passes next to the surface program" (`SHADER.SURFACE.EXTRA_PASS`), "N passes without LightMode"
(`SHADER.PASS.MULTIPLE_UNTAGGED`).

## What Urpify does (Pro)

- **Two drawn passes:** the first becomes `SRPDefaultUnlit`, the second `UniversalForward`, so they keep Built-in's
  drawing order. Checked on URP 14 and 17.
- **Three or more drawn passes:** passes 3 and later get the tags `UrpifyPass3`, `UrpifyPass4` and so on. Urpify's
  Built-in Camera Hooks renderer feature draws them after URP's opaque or transparent objects, each object's passes in
  order. Up to 32 drawn passes per SubShader.
- **Hand-written passes next to a surface program** are kept as they are and converted by the vert/frag rules.
- Screen-effect shaders drawn by `Graphics.Blit` are not retagged: their passes are chosen by index.

The renderer feature is added to every URP renderer that needs it during the Urpify converters step.

## How close

`Auto` for two passes, `AutoApprox` for three or more. With several such objects on screen, every object's first two
passes draw before the extra passes of any object, which can change how overlapping objects sort.
