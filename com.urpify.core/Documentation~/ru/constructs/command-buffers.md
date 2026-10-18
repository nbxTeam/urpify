# Командные буферы на событиях камеры

![Стенд C03: светящаяся оболочка, нарисованная командным буфером, пропадает после конвертера Unity, возвращается после Urpify](../../images/C03.jpg)

*Стенд C03: светящаяся оболочка, которую рисует `CommandBuffer`, добавленный к камере на `CameraEvent.AfterForwardAlpha`.*

## Что ломается

URP игнорирует `Camera.AddCommandBuffer`: всё, что рисует буфер, молча пропадает.

Находка: «CommandBuffer на ...» (`SCRIPT.COMMANDBUFFER.CAMERA`).

## Что делает Urpify (Pro)

`camera.AddCommandBuffer(evt, cb)` становится `camera.AddCommandBufferInUrp(evt, cb)` (то же для `RemoveCommandBuffer`).
Renderer Feature Built-in Camera Hooks от Urpify выполняет буфер на событии URP, соответствующем событию Built-in:

| `CameraEvent` встроенного конвейера | `RenderPassEvent` URP |
|---|---|
| `BeforeDepthTexture`, `AfterDepthTexture` | `BeforeRenderingPrePasses`, `AfterRenderingPrePasses` |
| `BeforeForwardOpaque`, `AfterForwardOpaque` | `BeforeRenderingOpaques`, `AfterRenderingOpaques` |
| `BeforeSkybox`, `AfterSkybox` | `BeforeRenderingSkybox`, `AfterRenderingSkybox` |
| `BeforeForwardAlpha`, `AfterForwardAlpha` | `BeforeRenderingTransparents`, `AfterRenderingTransparents` |
| `BeforeImageEffectsOpaque`, `AfterImageEffectsOpaque` | `AfterRenderingSkybox` |
| `BeforeImageEffects`, `AfterImageEffects`, `AfterEverything` | `BeforeRenderingPostProcessing`, после постобработки, `AfterRendering` |
| События отложенного пути (`BeforeGBuffer`, `AfterLighting` и другие) | вокруг непрозрачных объектов — ближайшая точка, которая есть в forward-рендерере URP |

`DrawRenderer`, `DrawMesh`, `Blit`, `SetGlobal*`, временные render texture, `SetRenderTarget` и `ClearRenderTarget`
выполняются так, как записаны.

## Насколько близко

`Авто, приблизительно`; события отложенного пути приблизительны по своей природе. Командные буферы на источниках света
(`Light.AddCommandBuffer`) пока не поддерживаются, см. [Что не переводится](../not-converted.md).
