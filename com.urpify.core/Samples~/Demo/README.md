# Urpify demo

A Built-in Render Pipeline scene with one booth per construct that breaks when a project moves to URP. Open
`Demo.unity` in a project that uses the Built-in Render Pipeline, then:

1. Window > Rendering > Urpify > **Scan project**: every booth's problem is listed, with what breaks and why.
2. **Migrate...** > **Run all**: back up, install URP, run Unity's converter and Urpify's converters, capture the booths
   before and after.
3. **Compare scenes**: each booth before and after, with a slider. **Roll back all** in Migrate puts the project back.

| Booth | Construct | After Unity's converter alone | Fixed by |
|---|---|---|---|
| S01 | Surface shader, Standard lighting | pink | Pro |
| S05 | Surface shader with a toon lighting function | pink | Pro |
| S09 | Surface shader with tessellation | pink | Pro |
| S12 | Toon surface shader with an outline pass | pink, outline only | Pro |
| V02 | Lit vert/frag shader, ForwardBase and ForwardAdd | pink | Pro |
| G01 | GrabPass refraction glass | the glass disappears | Pro |
| D01 | Water that reads the depth texture | white | Free |
| C01 | Projector blob shadow | the shadow disappears | Pro |
| C04 | Material created in code with `Shader.Find("Standard")` | pink | Free |
| STD04 | Standard in Transparent mode (glass) | opaque white | Free |
| LEG03 | Legacy Self-Illumin | the glow disappears | Free |
| M01 | Planar mirror rendered by a script (`OnWillRenderObject`) | black | Pro |

Every booth has a Migration Viewpoint, so the comparison shows each one up close.

## Licenses

The shaders, scripts, materials and the scene are part of Urpify and are under its license. The textures are from
ambientCG (https://ambientcg.com): Bricks076C, Fabric048, Rock030 and Wood049, CC0 1.0 (public domain).
