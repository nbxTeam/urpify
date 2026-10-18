# 相机事件上的命令缓冲区

![展台 C03：由命令缓冲区绘制的发光外壳，经 Unity 转换器后消失，经 Urpify 后恢复](../../images/C03.jpg)

*展台 C03：由添加到相机 `CameraEvent.AfterForwardAlpha` 上的 `CommandBuffer` 绘制的发光外壳。*

## 什么会损坏

URP 会忽略 `Camera.AddCommandBuffer`：缓冲区绘制的内容会在没有任何提示的情况下消失。

发现：“... 上的 CommandBuffer”（`SCRIPT.COMMANDBUFFER.CAMERA`）。

## Urpify 的做法（Pro）

`camera.AddCommandBuffer(evt, cb)` 变为 `camera.AddCommandBufferInUrp(evt, cb)`（`RemoveCommandBuffer` 同理）。Urpify 的
Built-in Camera Hooks 渲染器功能在与 Built-in 事件对应的 URP 事件处运行该缓冲区：

| Built-in 的 `CameraEvent` | URP 的 `RenderPassEvent` |
|---|---|
| `BeforeDepthTexture`、`AfterDepthTexture` | `BeforeRenderingPrePasses`、`AfterRenderingPrePasses` |
| `BeforeForwardOpaque`、`AfterForwardOpaque` | `BeforeRenderingOpaques`、`AfterRenderingOpaques` |
| `BeforeSkybox`、`AfterSkybox` | `BeforeRenderingSkybox`、`AfterRenderingSkybox` |
| `BeforeForwardAlpha`、`AfterForwardAlpha` | `BeforeRenderingTransparents`、`AfterRenderingTransparents` |
| `BeforeImageEffectsOpaque`、`AfterImageEffectsOpaque` | `AfterRenderingSkybox` |
| `BeforeImageEffects`、`AfterImageEffects`、`AfterEverything` | `BeforeRenderingPostProcessing`、后期处理之后、`AfterRendering` |
| 延迟路径的事件（`BeforeGBuffer`、`AfterLighting` 等） | 不透明物体前后，URP 前向渲染器中最接近的位置 |

`DrawRenderer`、`DrawMesh`、`Blit`、`SetGlobal*`、临时渲染纹理、`SetRenderTarget` 和 `ClearRenderTarget` 按记录的方式运行。

## 有多接近

`自动（近似）`；延迟路径的事件本质上是近似的。光源上的命令缓冲区（`Light.AddCommandBuffer`）暂不支持，参见
[哪些内容不会被转换](../not-converted.md)。
