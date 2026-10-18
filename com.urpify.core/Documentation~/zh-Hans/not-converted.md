# 哪些内容不会被转换，以及原因

Urpify 会在迁移开始前，列出它不转换的所有内容及原因。下面每一项都属于以下三组之一：

- **手动：项目在迁移前就已损坏。** Urpify 不修复项目本身的错误；它会告诉你哪里坏了、应先做什么。
- **手动：没有可转换的内容。** URP 中没有对应物，已对照 URP 源码确认。
- **暂不支持。** 可以做到，但这个版本的 Urpify 还没有做。这是我们接下来要做的工作；发现中会说明在此之前如何手动处理。

在开发者未见过的着色器上进行的独立测试中测得的 Urpify 着色器转换比例，见产品页面。本页列出不会被转换的内容及原因。

## 项目在迁移前就已损坏

| 情况 | 你会看到 | 怎么做 |
|---|---|---|
| 着色器在内置管线中无法编译 | “着色器本来就无法编译” | 先修复 Built-in 中的编译错误；这些物体已经是粉色了。 |
| 项目的脚本无法编译 | “项目脚本无法编译” | 先修复它们：它们会阻止 Play Mode、捕获以及重写脚本的步骤。旧项目常常需要更新已废弃的 API 调用。 |
| 着色器 include 了项目中不存在的文件 | “未找到 include” | 添加该文件或修正路径，然后重新扫描。 |
| `UsePass` 引用了项目中不存在的着色器 | “UsePass ...：未找到着色器” | 添加该着色器，或删除 `UsePass` 这一行。该物体在 Built-in 中也是粉色。 |
| 渲染器引用了项目中不存在的材质 | “... 个渲染器缺少材质” | 为其指定材质，或找回缺失的材质或包。当 Unity 升级某个包（例如 ProBuilder）而其旧的默认材质不复存在时会发生这种情况。 |

## 没有可转换的内容

| 情况 | 原因 |
|---|---|
| 内置渲染器的内部着色器：`Internal-DeferredShading`、`Internal-DeferredReflections`、`Internal-ScreenSpaceShadows`、`Internal-DepthNormalsTexture`、`Internal-MotionVectors`、`Internal-PrePassLighting`、`Internal-Flare`、`Internal-Halo` 及其副本 | Built-in 从 Graphics Settings 的内置着色器槽位中运行它们。URP 从不读取这些槽位：URP 14.0.12 和 17.5.0 的源码从未调用 `GraphicsSettings.GetCustomShader`，也不使用 `BuiltinShaderType`。URP 用自己的着色器渲染延迟光照、阴影、深度法线和运动矢量。 |
| LightMode 为 `Deferred`、`PrepassBase`、`PrepassFinal` 的 Pass | Built-in 的延迟和旧版延迟路径。URP 在其前向 Pass 中渲染物体，Urpify 根据着色器的前向 Pass 构建该 Pass。 |
| 表面着色器的 `finalgbuffer:` 函数 | 只在 Built-in 的延迟 G 缓冲区 Pass 中运行；转换后的前向着色器不会调用它们。 |
| `Camera.depthTextureMode` | URP 会忽略它。URP 根据 URP Asset 的设置生成深度和法线纹理，着色器读取它们时由 Urpify 开启。 |

## 暂不支持

| 结构 | 现在会怎样 | 手动怎么做 |
|---|---|---|
| 由 `Light.AddCommandBuffer` 驱动的效果（体积光等） | URP 中效果缺失；画面其余部分正常渲染。 | 把光源的命令移到 `BeforeRenderingShadows` 或 `AfterRenderingShadows` 处的渲染器功能中；URP 渲染阴影贴图的方式不同，请检查这些命令的前提。 |
| `OnRenderImage` 会调用到相机渲染、计算着色器派发或其他立即执行的绘制的图像效果 | 该效果在 URP 下缺失；帧的其余部分照常绘制。 | 把该调用移出效果：移到 `OnPreRender`，或放进由 `ImageEffectGraphics.ExecuteCommandBuffer` 执行的命令缓冲区。 |
| 着色器读取内置管线 G-buffer（`_CameraGBufferTexture0-3`：屏幕空间反射等）的图像效果 | 该效果在 URP 下缺失；帧的其余部分照常绘制。 | 用 URP 的深度、法线和不透明纹理重建输入，或使用 URP 的 Deferred 渲染器及其 G-buffer 布局。 |
| `Camera.onPreCull`、`Camera.onPreRender`、`Camera.onPostRender` 委托 | 挂在上面的代码不再运行。 | 把它移到 `RenderPipelineManager.beginCameraRendering` 或 `endCameraRendering`。 |
| 立即绘制的 `OnRenderImage`（`Graphics.DrawMeshNow` 等） | 效果缺失。 | 用命令缓冲区（`CommandBuffer.Blit`、`DrawMesh`）绘制，并用 `ImageEffectGraphics.ExecuteCommandBuffer` 运行。 |
| 渲染相机的 `OnRenderImage`（`Camera.Render`，包括通过辅助方法） | 该效果在 URP 中不运行：在其自身相机的帧内无法渲染其他相机，画面会变成全黑。 | 在 `OnPreRender` 或 `OnPreCull` 中渲染另一台相机，`OnRenderImage` 中只保留 Blit 调用。测试相机模板缓冲区的 Blit 在 URP 中同样看不到它。 |
| 光源上挂在 Built-in 延迟光照事件的命令缓冲区 | 缺失。 | 在对应的 URP 事件处使用渲染器功能。 |
| 读取 Built-in G 缓冲区（`_CameraGBufferTexture0` 至 `3`）的着色器，例如屏幕空间 GI | 效果在没有任何报错的情况下失去输入：URP 的 Forward 渲染器没有 G 缓冲区，URP 的 Deferred 路径对自己的 G 缓冲区采用不同的打包方式（`_GBuffer0` 至 `_GBuffer6`）。 | 用 URP 的深度、法线和不透明纹理重建输入，或将 URP 渲染器切换为 Deferred，并按 `UnityGBuffer.hlsl` 重写读取。 |
| 一个着色器中有多个使用不同纹理的 GrabPass | 着色器不会被转换。 | URP 只有一张 Opaque Texture：合并这些捕获，或只保留一个并读取两次。 |
| 一个 SubShader 中有超过 32 个绘制 Pass | 不转换。 | 合并 Pass，或把其中一些移到单独的 SubShader 或材质中。 |
| 从 Pass 位于 `CGINCLUDE` 块中的着色器 `UsePass` | 不转换。 | 把该 Pass 复制到着色器中。 |
| Urpify 尚无对应物的内置 include | 显示“Stopped: ... includes ...”，着色器不会被转换。 | 手动移植该 include 的函数，或通过“发送报告”发送该着色器。 |
| 直接读取部分 Built-in 光源变量、逐顶点光照或不返回颜色的 `ForwardAdd` Pass | 不转换。 | 用 `UNITY_LIGHT_ATTENUATION` 重写该 Pass，或将其移入 URP 的额外光源循环。 |
| 签名不为转换器所知的光照函数 | 不转换。 | 使用 Built-in 文档中的某个 `Lighting<Name>` 签名。 |
| 固定功能管线的 Pass（没有着色器程序） | 不转换：URP 只绘制带程序的 Pass。 | 把该 Pass 重写为 vert/frag 程序。 |
| 在 Unity 转换器之前其文件不在备份中的 Material Variant | 其自有数值在 Unity 转换器中丢失。 | 使用会保存该文件的 Urpify 步骤进行迁移，或重新设置这些数值。 |
| 转换后无法编译的着色器或脚本 | 恢复原文件，编译器消息写在发现中。 | 在着色器面板中修正结果并用“另存为”保存，或通过“发送报告”发送该着色器。 |

## 已转换，但并不完全相同

以下内容会被转换并能工作，但有一处差异来自 URP 本身，而不是转换。图片见 [URP 外观上的差异](urp-differences.md)。

- **使用 Opaque Texture 的 GrabPass**（默认）：URP 在透明对象之前复制屏幕，因此折射中看不到它们。若要在之后复制，请将
  GrabPass 来源设为“透明对象之后”。
- **自定义光照函数和 ForwardAdd Pass：** 作者的函数在一个 Pass 中为主光源和每个额外光源运行，使用 URP 的阴影和衰减。
- **Post-processing Stack v2 的泛光：** 变为 URP 的泛光，光晕更柔和、更宽；URP 的泛光金字塔对小亮点的扩散方式不同。
- **Standard 到 URP Lit**（Unity 自己的转换）：在我们的测试场景中，最多约 7 % 的画面不同。
- **`OnWillRenderObject` 中的反射** 会在 URP 剔除之前、针对相机可见的渲染器调用，且不做遮挡剔除，因此墙后的反射仍会被
  渲染。
