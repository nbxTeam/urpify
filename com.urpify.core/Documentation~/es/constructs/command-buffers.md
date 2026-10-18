# Command buffers en eventos de cámara

![Puesto C03: una envoltura brillante dibujada por un command buffer, desaparece tras el conversor de Unity, vuelve tras Urpify](../../images/C03.jpg)

*Puesto C03: una envoltura brillante dibujada por un `CommandBuffer` añadido a la cámara en
`CameraEvent.AfterForwardAlpha`.*

## Qué se rompe

URP ignora `Camera.AddCommandBuffer`: lo que dibuja el buffer desaparece sin avisar.

Hallazgo: «CommandBuffer en ...» (`SCRIPT.COMMANDBUFFER.CAMERA`).

## Qué hace Urpify (Pro)

`camera.AddCommandBuffer(evt, cb)` pasa a ser `camera.AddCommandBufferInUrp(evt, cb)` (igual con `RemoveCommandBuffer`).
El Renderer Feature Built-in Camera Hooks de Urpify ejecuta el buffer en el evento de URP que corresponde al de Built-in:

| `CameraEvent` de Built-in | `RenderPassEvent` de URP |
|---|---|
| `BeforeDepthTexture`, `AfterDepthTexture` | `BeforeRenderingPrePasses`, `AfterRenderingPrePasses` |
| `BeforeForwardOpaque`, `AfterForwardOpaque` | `BeforeRenderingOpaques`, `AfterRenderingOpaques` |
| `BeforeSkybox`, `AfterSkybox` | `BeforeRenderingSkybox`, `AfterRenderingSkybox` |
| `BeforeForwardAlpha`, `AfterForwardAlpha` | `BeforeRenderingTransparents`, `AfterRenderingTransparents` |
| `BeforeImageEffectsOpaque`, `AfterImageEffectsOpaque` | `AfterRenderingSkybox` |
| `BeforeImageEffects`, `AfterImageEffects`, `AfterEverything` | `BeforeRenderingPostProcessing`, tras el posprocesado, `AfterRendering` |
| Eventos del camino diferido (`BeforeGBuffer`, `AfterLighting` y otros) | alrededor de los objetos opacos, el punto más cercano que tiene el renderer forward de URP |

`DrawRenderer`, `DrawMesh`, `Blit`, `SetGlobal*`, render textures temporales, `SetRenderTarget` y `ClearRenderTarget` se
ejecutan tal como se grabaron.

## Cuánto se acerca

`Automático, aproximado`; los eventos del camino diferido son aproximados por naturaleza. Los command buffers en luces
(`Light.AddCommandBuffer`) aún no son compatibles, consulta [Qué no se convierte](../not-converted.md).
