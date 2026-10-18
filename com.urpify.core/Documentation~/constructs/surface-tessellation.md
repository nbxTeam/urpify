# Tessellation

![Lab booth S09: tessellation with displacement](../images/S09.jpg)

*Lab booth S09: a surface shader with `tessellate:` and displacement.*

## What breaks

`tessellate:func` makes Unity generate hull and domain stages for a surface shader. URP has no surface shaders, so the
object is pink.

Finding: "Tessellation (...)" (`SHADER.SURFACE.TESSELLATION`).

## What Urpify does (Pro)

The converter generates hull and domain stages from a template that follows Built-in's: the author's tessellation
function decides the factors, `fractional_odd` partitioning, then the `vertex:` function displaces the new vertices. Every
pass that draws the object (forward, shadow, depth) is tessellated, so the shadow follows the displaced surface.
`tessphong:` adds Phong smoothing with the given strength.

Vert/frag shaders that write their own hull and domain stages keep them; the attributes Built-in defines
(`UNITY_domain`, `UNITY_partitioning`, `UNITY_outputtopology`, `UNITY_patchconstantfunc`, `UNITY_outputcontrolpoints`)
come from Urpify's library, and the compile check covers the hull and domain stages.

## How close

`AutoApprox`: the same factors and displacement, URP's lighting on top.
