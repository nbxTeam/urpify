# Unlit vert/frag shaders

![Lab booth V01: an unlit CG shader with fog and scrolling UVs, the same in all three](../images/V01.jpg)

*Lab booth V01: an unlit CG shader with fog and UV scrolling. URP draws it as it is.*

## What happens

A vert/frag shader with no LightMode tag, or with `Always`, often renders in URP without any change: URP draws a pass
without a LightMode tag as `SRPDefaultUnlit`, and CG code with `UnityCG.cginc` still compiles. Urpify checks that the
shader compiles in URP and leaves it alone when it does.

Finding: none, or a note that URP draws the shader as it is.

What can still break, and has its own page:
- two or more passes without LightMode: [Extra passes](extra-passes.md);
- reading `_CameraDepthTexture`: [Depth texture](depth-texture.md);
- `GrabPass`: [GrabPass](grabpass.md);
- Built-in light variables such as `_LightColor0` in an unlit pass: they are zero in URP; Pro maps them to URP's main
  light.
