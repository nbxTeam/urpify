# Command buffers on camera events

![Lab booth C03: a glow shell drawn by a command buffer, gone after Unity's converter, back after Urpify](../images/C03.jpg)

*Lab booth C03: a glow shell drawn by a `CommandBuffer` added to the camera at `CameraEvent.AfterForwardAlpha`.*

## What breaks

URP ignores `Camera.AddCommandBuffer`: whatever the buffer draws disappears, silently.

Finding: "CommandBuffer at ..." (`SCRIPT.COMMANDBUFFER.CAMERA`).

## What Urpify does (Pro)

`camera.AddCommandBuffer(evt, cb)` becomes `camera.AddCommandBufferInUrp(evt, cb)` (and the same for
`RemoveCommandBuffer`). Urpify's Built-in Camera Hooks renderer feature runs the buffer at the URP event that matches the
Built-in event:

| Built-in `CameraEvent` | URP `RenderPassEvent` |
|---|---|
| `BeforeDepthTexture`, `AfterDepthTexture` | `BeforeRenderingPrePasses`, `AfterRenderingPrePasses` |
| `BeforeForwardOpaque`, `AfterForwardOpaque` | `BeforeRenderingOpaques`, `AfterRenderingOpaques` |
| `BeforeSkybox`, `AfterSkybox` | `BeforeRenderingSkybox`, `AfterRenderingSkybox` |
| `BeforeForwardAlpha`, `AfterForwardAlpha` | `BeforeRenderingTransparents`, `AfterRenderingTransparents` |
| `BeforeImageEffectsOpaque`, `AfterImageEffectsOpaque` | `AfterRenderingSkybox` |
| `BeforeImageEffects`, `AfterImageEffects`, `AfterEverything` | `BeforeRenderingPostProcessing`, after post-processing, `AfterRendering` |
| Deferred events (`BeforeGBuffer`, `AfterLighting` and others) | around the opaque objects, the nearest point URP's forward renderer has |

`DrawRenderer`, `DrawMesh`, `Blit`, `SetGlobal*`, temporary render textures, `SetRenderTarget` and `ClearRenderTarget` run
as recorded.

## How close

`AutoApprox`; deferred events are approximate by nature. Command buffers on lights (`Light.AddCommandBuffer`) are not
supported yet, see [What is not converted](../not-converted.md).
