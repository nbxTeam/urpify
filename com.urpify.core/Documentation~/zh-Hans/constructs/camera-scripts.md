# 渲染相机的脚本：镜子、传送门、水面反射

![展台 M01：平面镜，经 Unity 转换器后为黑色，经 Urpify 后重新反射](../../images/M01.jpg)

*展台 M01：按 MirrorReflection 和 Standard Assets Water 的写法实现的平面镜：`OnWillRenderObject`、`Camera.current`、带倾斜
近裁剪面的反射相机，以及 `Camera.Render`。只经过 Unity 转换器时，镜子是黑色的。*

## 什么会损坏

URP 从不调用相机的 `OnPreCull`、`OnPreRender` 和 `OnPostRender`，而 `OnWillRenderObject` 是在剔除期间调用的，那时无法渲染
其他相机。在这些方法中渲染第二台相机的传送门和镜子会显示为平面或黑色。不会有错误。已在 URP 14 和 17 上验证。

发现：“相机上的 OnPreCull”等（`SCRIPT.CAMERA_CALLBACKS`）、“OnWillRenderObject 渲染相机”（`SCRIPT.WILLRENDER_CAMERA`）。

## Urpify 的做法（Pro）

脚本保留其方法和逻辑。Urpify 把它注册到 URP 的相机事件上：

- `OnPreCull` 和 `OnPreRender` 由其相机的 `RenderPipelineManager.beginCameraRendering` 调用，`OnPostRender` 由
  `endCameraRendering` 调用。
- `OnWillRenderObject` 在 URP 相机剔除之前、针对该相机能看到的渲染器调用，此时 `Camera.current` 指向该相机。
- 其中的 `Camera.Render()` 通过渲染请求渲染到 URP 的帧中，因此反射或传送门纹理会在显示它的相机绘制之前准备好。
- `OnRenderObject` 在相机的不透明和透明物体之后调用。

Urpify 的目录能识别已知脚本：SebLague 的 Portals、MirrorReflection、Standard Assets Water 和 ToonWaterShader 的法线相机，
会用在这些项目上验证过的方案转换。其他脚本使用同样的机制，得到“自动（近似）”。

## 有多接近

目录中已验证的方案为 `自动`，其他为 `自动（近似）`：请在场景比较中检查这些场景。与 Built-in 有一处不同：
`OnWillRenderObject` 会针对相机视锥内的渲染器调用，且不做遮挡剔除，因此藏在墙后的镜子仍会渲染其反射。

挂在 `Camera.onPreCull`、`Camera.onPreRender` 和 `Camera.onPostRender` 委托上的代码暂不转换；发现会说明如何迁移。渲染相机的
`OnRenderImage` 在 URP 中不运行，参见 [哪些内容不会被转换](../not-converted.md)。
