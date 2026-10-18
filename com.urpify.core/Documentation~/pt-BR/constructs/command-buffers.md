# Command buffers em eventos de câmera

![Estande C03: uma casca brilhante desenhada por um command buffer, some depois do conversor da Unity, volta depois do Urpify](../../images/C03.jpg)

*Estande C03: uma casca brilhante desenhada por um `CommandBuffer` adicionado à câmera em `CameraEvent.AfterForwardAlpha`.*

## O que quebra

O URP ignora `Camera.AddCommandBuffer`: o que o buffer desenha some sem aviso.

Achado: «CommandBuffer em ...» (`SCRIPT.COMMANDBUFFER.CAMERA`).

## O que o Urpify faz (Pro)

`camera.AddCommandBuffer(evt, cb)` vira `camera.AddCommandBufferInUrp(evt, cb)` (o mesmo para `RemoveCommandBuffer`). O
Renderer Feature Built-in Camera Hooks do Urpify roda o buffer no evento do URP que corresponde ao do Built-in:

| `CameraEvent` do Built-in | `RenderPassEvent` do URP |
|---|---|
| `BeforeDepthTexture`, `AfterDepthTexture` | `BeforeRenderingPrePasses`, `AfterRenderingPrePasses` |
| `BeforeForwardOpaque`, `AfterForwardOpaque` | `BeforeRenderingOpaques`, `AfterRenderingOpaques` |
| `BeforeSkybox`, `AfterSkybox` | `BeforeRenderingSkybox`, `AfterRenderingSkybox` |
| `BeforeForwardAlpha`, `AfterForwardAlpha` | `BeforeRenderingTransparents`, `AfterRenderingTransparents` |
| `BeforeImageEffectsOpaque`, `AfterImageEffectsOpaque` | `AfterRenderingSkybox` |
| `BeforeImageEffects`, `AfterImageEffects`, `AfterEverything` | `BeforeRenderingPostProcessing`, depois do pós-processamento, `AfterRendering` |
| Eventos do caminho deferred (`BeforeGBuffer`, `AfterLighting` e outros) | em volta dos objetos opacos, o ponto mais próximo que o renderer forward do URP tem |

`DrawRenderer`, `DrawMesh`, `Blit`, `SetGlobal*`, render textures temporárias, `SetRenderTarget` e `ClearRenderTarget`
rodam como foram gravados.

## Quão perto

`Automático, aproximado`; os eventos do caminho deferred são aproximados por natureza. Command buffers em luzes
(`Light.AddCommandBuffer`) ainda não têm suporte, veja [O que não é convertido](../not-converted.md).
