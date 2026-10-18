# What is not converted, and why

Urpify lists everything it does not convert, with the reason, before you migrate. Each item below is in one of three
groups:

- **Manual: the project was broken before the migration.** Urpify does not fix your project's own errors; it says what
  is broken and what to do first.
- **Manual: there is nothing to convert.** URP has no counterpart, and this was checked in URP's sources.
- **Not supported yet.** Possible, but Urpify does not do it in this version. These are the items we work on next; the
  finding says what to do by hand meanwhile.

The share of shaders Urpify converts, measured in an independent test on shaders its developers did not see, is on
the product page. This page lists what is not converted, and why.

## The project was broken before the migration

| Case | What you see | What to do |
|---|---|---|
| The shader does not compile in Built-in | "Shader already fails to compile" | Fix the compile errors in Built-in first; the objects are already pink. |
| The project's scripts do not compile | "Project scripts do not compile" | Fix them first: they block Play Mode, the captures and the steps that rewrite scripts. Old projects often need obsolete API calls updated. |
| A shader includes a file that is not in the project | "Include not found" | Add the file or fix the path, then scan again. |
| `UsePass` names a shader that is not in the project | "UsePass ...: shader not found" | Add the shader or remove the `UsePass` line. The object is pink in Built-in too. |
| A renderer refers to a material that is not in the project | "Missing material on ... renderer(s)" | Assign a material, or bring back the missing material or package. This happens when Unity upgrades a package (ProBuilder, for example) whose old default material is gone. |

## There is nothing to convert

| Case | Why |
|---|---|
| Internal shaders of the Built-in renderer: `Internal-DeferredShading`, `Internal-DeferredReflections`, `Internal-ScreenSpaceShadows`, `Internal-DepthNormalsTexture`, `Internal-MotionVectors`, `Internal-PrePassLighting`, `Internal-Flare`, `Internal-Halo` and their copies | Built-in runs them from the built-in shader slots of Graphics Settings. URP never reads those slots: the sources of URP 14.0.12 and 17.5.0 never call `GraphicsSettings.GetCustomShader` or use `BuiltinShaderType`. URP renders deferred lighting, shadows, depth-normals and motion vectors with its own shaders. |
| Passes with LightMode `Deferred`, `PrepassBase`, `PrepassFinal` | Built-in's deferred and legacy deferred paths. URP renders the object in its forward pass, which Urpify builds from the shader's forward passes. |
| `finalgbuffer:` functions of surface shaders | They run only in Built-in's deferred G-buffer pass; the converted forward shader does not call them. |
| `Camera.depthTextureMode` | URP ignores it. URP makes the depth and normals textures from the URP Asset settings, which Urpify turns on when a shader reads them. |

## Not supported yet

| Construct | What happens now | What to do by hand |
|---|---|---|
| Effects fed by `Light.AddCommandBuffer` (volumetric lights and similar) | The effect is missing under URP; the rest of the frame is drawn. | Move the light's commands into a Renderer Feature at `BeforeRenderingShadows` or `AfterRenderingShadows`; URP renders shadow maps differently, so check what the commands expect. |
| Image effects whose `OnRenderImage` reaches a camera render, a compute dispatch or other drawing that runs at once | The effect is missing under URP; the rest of the frame is drawn. | Move that call out of the effect: into `OnPreRender`, or into a command buffer run with `ImageEffectGraphics.ExecuteCommandBuffer`. |
| Image effects whose shaders read Built-in's G-buffer (`_CameraGBufferTexture0-3`: screen-space reflections and similar) | The effect is missing under URP; the rest of the frame is drawn. | Rebuild the inputs from URP's depth, normals and opaque textures, or use URP's Deferred renderer and its G-buffer layout. |
| `Camera.onPreCull`, `Camera.onPreRender`, `Camera.onPostRender` delegates | The code added to them stops running. | Move it to `RenderPipelineManager.beginCameraRendering` or `endCameraRendering`. |
| `OnRenderImage` that draws immediately (`Graphics.DrawMeshNow` and similar) | The effect is missing. | Draw with a command buffer (`CommandBuffer.Blit`, `DrawMesh`) and run it with `ImageEffectGraphics.ExecuteCommandBuffer`. |
| `OnRenderImage` that renders a camera (`Camera.Render`, also through a helper method) | The effect is not run under URP: inside its own camera's frame no other camera can render, and the frame would come out black. | Render the other camera in `OnPreRender` or `OnPreCull` and keep only the Blit calls in `OnRenderImage`. A Blit that tests the camera's stencil buffer does not see it under URP either. |
| Command buffers at Built-in's deferred lighting events on a light | Missing. | A Renderer Feature at the matching URP event. |
| Shaders that read Built-in's G-buffer (`_CameraGBufferTexture0` to `3`), such as screen-space GI | The effect loses its input without an error: URP's Forward renderer has no G-buffer, and URP's Deferred path packs its own differently (`_GBuffer0` to `_GBuffer6`). | Rebuild the inputs from URP's depth, normals and opaque textures, or switch the URP renderer to Deferred and rewrite the reads for `UnityGBuffer.hlsl`. |
| Several GrabPasses with different textures in one shader | The shader is not converted. | URP has one Opaque Texture: merge the grabs, or keep one and read it twice. |
| More than 32 drawn passes in one SubShader | Not converted. | Merge passes, or move some into their own SubShader or material. |
| `UsePass` from a shader whose pass sits in a `CGINCLUDE` block | Not converted. | Copy the pass into the shader. |
| A Built-in include Urpify has no counterpart for yet | "Stopped: ... includes ...", the shader is not converted. | Port the include's functions, or send the shader with Send report. |
| A `ForwardAdd` pass that reads some of Built-in's per-light variables directly, lights per vertex, or does not return its colour | Not converted. | Rewrite the pass with `UNITY_LIGHT_ATTENUATION`, or port it to URP's additional lights loop. |
| A lighting function with a signature the converter does not know | Not converted. | Use one of Built-in's documented `Lighting<Name>` signatures. |
| Fixed-function passes (no shader program) | Not converted: URP draws only passes with a program. | Rewrite the pass as a vert/frag program. |
| A material variant whose file from before Unity's converter is not in the backup | Its own values are lost to Unity's converter. | Run the migration with Urpify's steps, which keep that file, or set the values again. |
| A converted shader or script that does not compile | The original is put back and the finding has the compiler's message. | Fix the result in the shader panel and Save as, or send the shader with Send report. |

## Converted, but not identical

These are converted and work, with a difference that is the nature of URP, not of the conversion. See
[How URP looks different](urp-differences.md) for pictures.

- **GrabPass with Opaque Texture** (the default): URP copies the screen before transparent objects, so transparent
  objects are missing from refractions. Set GrabPass source to After transparents to copy after them.
- **Custom lighting functions and ForwardAdd passes:** the author's function runs for the main light and for each
  additional light in one pass, with URP's shadows and light attenuation.
- **Post-processing Stack v2 bloom:** converted to URP's bloom, with a softer, wider glow; URP's bloom pyramid spreads
  small bright spots differently.
- **Standard to URP Lit** (Unity's own conversion): up to about 7 % of a frame differs on our test scenes.
- **`OnWillRenderObject` reflections** run before URP's culling for the renderers the camera sees, without occlusion
  culling, so a reflection behind a wall is still rendered.
