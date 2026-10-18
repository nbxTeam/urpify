# Unity and URP versions

| Unity | URP | Status | Tested on |
|---|---|---|---|
| 6000.6 | 17.6 | Supported | 6000.6.5f1 with URP 17.6.0: unit tests, the 37 lab booths, the demo, 4 real projects |
| 6000.5 | 17.5 | Supported | 6000.5.9f1 with URP 17.5.0: unit tests, the 37 lab booths, real projects, the shader corpus |
| 6000.4 | 17.4 | Not tested | |
| 6000.3 LTS | 17.3 | Supported | 6000.3.26f1 with URP 17.3.0: unit tests, the 37 lab booths, the demo, 4 real projects. Unity's own batch converter fails on this version; Urpify works around it (below) |
| 6000.1, 6000.2 | 17.1, 17.2 | Not tested | |
| 6000.0 LTS | 17.0 | Supported | 6000.0.84f1 with URP 17.0.4: unit tests, the 37 lab booths, the demo, 4 real projects |
| 2022.3 LTS | 14 | Supported | 2022.3.62f3 with URP 14.0.12: the same set as 6000.5 |
| 2021.3 LTS | 12 | Not supported in 1.0 | Planned on request |

Migrate in the Editor version you will ship with: the before and after captures are only comparable within one Unity
version.

## Unity's converter and the exit code on 6000.0 and 6000.3

- URP 17.0 and 17.3 have the older batch API of Unity's converter (`ConverterContainerId`). On URP 17.3.0 (6000.3.26f1) it throws
  `MissingMethodException` for the abstract `Base2DMaterialUpgrader` before it converts anything, in any project. Urpify then
  lists the Built-in to URP converters itself and runs them the same way; the step's Details say so.
- Unity 6000.0.84f1 and 6000.3.26f1 often, and 6000.5.9f1 now and then, crash while they quit in batch mode (exit code 139) after the
  work is done, with or without Urpify. On the command line, read `Urpify/urpify-<command>-result.txt` (see [Command line](cli.md)).

## What differs between URP 14 and URP 17

Urpify's shader library (`BirpCompat*.hlsl`) and its renderer feature handle each difference below, so a converted shader
compiles and draws on both. Each row was checked in the package sources of URP 14.0.12 (2022.3.62f3) and URP 17.5.0
(6000.5.9f1).

| Topic | URP 14.0.12 | URP 17.5.0 | Where it was checked |
|---|---|---|---|
| Main light | `GetMainLight()`, `(shadowCoord)`, `(shadowCoord, positionWS, shadowMask)`, `(inputData, shadowMask, aoFactor)` | The same 4 overloads | `ShaderLibrary/RealtimeLights.hlsl` |
| Additional lights | `GetAdditionalLight(i, positionWS)`, `(i, positionWS, shadowMask)`, `(i, inputData, shadowMask, aoFactor)` | The same 3 overloads | `ShaderLibrary/RealtimeLights.hlsl` |
| Forward+ light loop | `USE_FORWARD_PLUS` | `USE_CLUSTER_LIGHT_LOOP`; `USE_FORWARD_PLUS` stays as an alias in `ForwardPlusKeyword.deprecated.hlsl` | `ShaderLibrary/RealtimeLights.hlsl`, `Core.hlsl` |
| Additional light shadows | `AdditionalLightRealtimeShadow(lightIndex, positionWS, lightDirection)` | The same, plus an overload with shadow parameters and sampling data | `ShaderLibrary/Shadows.hlsl` |
| Spherical harmonics | `SampleSH(normalWS)` in URP's `GlobalIllumination.hlsl` | `SampleSH(normalWS)` in the Core package's `AmbientProbe.hlsl` | URP and Core `ShaderLibrary` |
| Lightmaps | `LIGHTMAP_ON`, `DIRLIGHTMAP_COMBINED` | The same keywords | `ShaderLibrary/GlobalIllumination.hlsl` |
| LOD cross-fade | `LODCrossFade.hlsl` | `LODCrossFade.hlsl` | `ShaderLibrary/` |
| Decals | `DBuffer.hlsl` | `DBuffer.hlsl` | `ShaderLibrary/` |
| Depth, normals, opaque textures | `DeclareDepthTexture.hlsl`, `DeclareNormalsTexture.hlsl`, `DeclareOpaqueTexture.hlsl` | The same files | `ShaderLibrary/` |
| Renderer features | `ScriptableRenderPass.Execute`; Render Graph is internal | Render Graph (`RecordRenderGraph`); from Unity 6.4 the compatibility mode is gone: `RenderGraphSettings.enableRenderCompatibilityMode` always returns false | `Runtime/Passes/ScriptableRenderPass.cs`, `Runtime/Settings/RenderGraphSettings.cs` |
| Opaque Texture for preview cameras | Not copied for a preview camera (`copyColorPass &= !isPreviewCamera`) | Copied | `Runtime/UniversalRenderer.cs`. Urpify's self-check renders GrabPass shaders with a game camera on 2022.3 for this reason. |
| URP Asset textures | A player throws `NullReferenceException` every frame and renders black when the URP Asset's `m_Textures` (blue noise, Bayer) are empty, as Unity's converter saves them | Not observed in 6000.5 players of the same projects | `UniversalRenderPipeline.cs` (`SetupPerFrameShaderConstants`). The free package fills them in after Unity's converter. |

## What differs in Unity's converter

| Topic | 2022.3 / URP 14 | 6000.5 / URP 17.5 |
|---|---|---|
| Material upgraders for `Legacy Shaders/Particles/VertexLit Blended` and `Mobile/Bumped Specular (1 Directional Realtime Light)` | Registered under old shader names, so Unity's converter skips them; Urpify converts these materials from its own table | Unity converts them |
| Batch mode without `-typesFilter` | Not measured: Urpify does not use Unity's batch command there either | Throws `NullReferenceException` (`Converters.cs:134`); Urpify runs the converter through its API instead |
| Exit code of Unity's batch command after a successful run | Not measured | 3 of 4 real projects exited with 21 or 139 (the process crashes while it exits); Urpify judges the result by the converter's log and a rescan, on both versions |
