# Constructs

Urpify supports constructs, not particular shaders: a shader converts when every construct it uses is supported,
otherwise the scan names the one that is missing. Each page below explains one construct of the Built-in Render
Pipeline: what breaks in URP, what Urpify does about it, and how close the result is.

The pictures come from Urpify's test lab, a scene with one booth per construct. Each shows the same booth three times:
in Built-in, in URP after Unity's Render Pipeline Converter alone, and in URP after Unity's converter and Urpify. They are
the frames Urpify's own Capture steps took, cropped to the booth.

| Construct | Page | Lab booth | Fixed by |
|---|---|---|---|
| Surface shaders with Built-in lighting models | [Surface shaders](surface-shaders.md) | S01 to S04 | Pro |
| Custom lighting functions | [Custom lighting](surface-custom-lighting.md) | S05 | Pro |
| `#pragma surface` options: `vertex:`, `finalcolor:`, `decal:`, `alphatest:`, `addshadow` and others | [Surface options](surface-options.md) | S03, S06 to S08, S10, S11 | Pro |
| Surface tessellation | [Tessellation](surface-tessellation.md) | S09 | Pro |
| Extra passes: outlines, fur shells, 3 or more passes | [Extra passes](extra-passes.md) | S12 | Pro |
| Unlit vert/frag shaders | [Unlit vert/frag](vertfrag-unlit.md) | V01 | Nothing to do |
| Lit vert/frag shaders: ForwardBase, ForwardAdd, shadows, vertex-lit passes | [Lit vert/frag](vertfrag-lit.md) | V02 | Pro |
| GrabPass | [GrabPass](grabpass.md) | G01, G02 | Pro |
| `_CameraDepthTexture` | [Depth texture](depth-texture.md) | D01 | Free and Pro |
| `_CameraDepthNormalsTexture`, replacement shaders | [Depth-normals and replacement shaders](depth-normals.md) | none | Pro |
| `OnRenderImage` image effects | [Image effects](image-effects.md) | C02 | Pro |
| `CommandBuffer` on camera events | [Command buffers](command-buffers.md) | C03 | Pro |
| Scripts that render cameras: mirrors, portals, water reflections | [Camera scripts](camera-scripts.md) | M01 | Pro |
| `Projector` components | [Projectors](projectors.md) | C01 | Pro |
| `Shader.Find` with Built-in shader names | [Shader.Find in code](shader-find.md) | C04 | Free |
| What Unity's converter breaks: Standard Transparent, Self-Illumin, built-in materials, Post-processing v2, URP Asset textures, material variants | [Fixes after Unity's converter](after-unity-converter.md) | STD04, LEG03 | Free |
| What Unity's converter handles: Standard, Legacy, Mobile, particles, terrain, imported models | [Unity's converter](unity-converter.md) | STD01 to STD06, LEG01 to LEG05, PAR01, PAR02, FBX01, FBX02, TER01 | Unity |
| Tree Creator and soft-occlusion trees | [Trees](trees.md) | none | Pro |
| One shader file for Built-in and URP | [Keep Built-in support](keep-builtin-support.md) | all shader booths | Pro |

Constructs Urpify does not convert are listed, with the reason, in [What is not converted](../not-converted.md).
