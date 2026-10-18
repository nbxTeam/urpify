# Unity 与 URP 版本

| Unity | URP | 状态 | 验证环境 |
|---|---|---|---|
| 6000.6 | 17.6 | 支持 | 6000.6.5f1 与 URP 17.6.0：单元测试、37 个展台、演示、4 个真实项目 |
| 6000.5 | 17.5 | 支持 | 6000.5.9f1 与 URP 17.5.0：单元测试、37 个展台、真实项目、着色器语料库 |
| 6000.4 | 17.4 | 未测试 | |
| 6000.3 LTS | 17.3 | 支持 | 6000.3.26f1 与 URP 17.3.0：单元测试、37 个展台、演示、4 个真实项目。Unity 自带的批量转换器在此版本上会失败，Urpify 会绕过它（见下文） |
| 6000.1、6000.2 | 17.1、17.2 | 未测试 | |
| 6000.0 LTS | 17.0 | 支持 | 6000.0.84f1 与 URP 17.0.4：单元测试、37 个展台、演示、4 个真实项目 |
| 2022.3 LTS | 14 | 支持 | 2022.3.62f3 与 URP 14.0.12：与 6000.5 相同的一套测试 |
| 2021.3 LTS | 12 | 1.0 不支持 | 如有需求再计划 |

请使用你将用来发布的编辑器版本进行迁移：迁移前后的捕获只能在同一个 Unity 版本内比较。

## 6000.0 与 6000.3 上的 Unity 转换器和退出代码

- URP 17.0 和 17.3 使用 Unity 转换器较旧的批量 API（`ConverterContainerId`）。在 URP 17.3.0（6000.3.26f1）上，它在任何项目中都会在转换任何内容之前，
  因抽象类 `Base2DMaterialUpgrader` 抛出 `MissingMethodException`。此时 Urpify 会自己列出 Built-in 到 URP 的转换器并以同样方式运行，步骤的详细信息会说明这一点。
- Unity 6000.0.84f1 和 6000.3.26f1 经常、6000.5.9f1 偶尔会在工作完成后退出批处理模式时崩溃（退出代码 139），与是否安装 Urpify 无关。
  在命令行中，请读取 `Urpify/urpify-<command>-result.txt`（见 [命令行](cli.md)）。

## URP 14 与 URP 17 的差异

Urpify 的着色器库（`BirpCompat*.hlsl`）及其渲染器功能考虑了下面的每一项差异，因此转换后的着色器在两个版本中都能编译和
渲染。每一行都已对照 URP 14.0.12（2022.3.62f3）和 URP 17.5.0（6000.5.9f1）的包源码确认。

| 主题 | URP 14.0.12 | URP 17.5.0 | 确认位置 |
|---|---|---|---|
| 主光源 | `GetMainLight()`、`(shadowCoord)`、`(shadowCoord, positionWS, shadowMask)`、`(inputData, shadowMask, aoFactor)` | 相同的 4 个重载 | `ShaderLibrary/RealtimeLights.hlsl` |
| 额外光源 | `GetAdditionalLight(i, positionWS)`、`(i, positionWS, shadowMask)`、`(i, inputData, shadowMask, aoFactor)` | 相同的 3 个重载 | `ShaderLibrary/RealtimeLights.hlsl` |
| Forward+ 光源循环 | `USE_FORWARD_PLUS` | `USE_CLUSTER_LIGHT_LOOP`；`USE_FORWARD_PLUS` 在 `ForwardPlusKeyword.deprecated.hlsl` 中保留为别名 | `ShaderLibrary/RealtimeLights.hlsl`、`Core.hlsl` |
| 额外光源的阴影 | `AdditionalLightRealtimeShadow(lightIndex, positionWS, lightDirection)` | 同上，另有一个带阴影参数和采样数据的重载 | `ShaderLibrary/Shadows.hlsl` |
| 球谐函数 | URP 包 `GlobalIllumination.hlsl` 中的 `SampleSH(normalWS)` | Core 包 `AmbientProbe.hlsl` 中的 `SampleSH(normalWS)` | URP 和 Core 的 `ShaderLibrary` |
| 光照贴图 | `LIGHTMAP_ON`、`DIRLIGHTMAP_COMBINED` | 相同的关键字 | `ShaderLibrary/GlobalIllumination.hlsl` |
| LOD 交叉淡化 | `LODCrossFade.hlsl` | `LODCrossFade.hlsl` | `ShaderLibrary/` |
| 贴花 | `DBuffer.hlsl` | `DBuffer.hlsl` | `ShaderLibrary/` |
| 深度、法线、不透明纹理 | `DeclareDepthTexture.hlsl`、`DeclareNormalsTexture.hlsl`、`DeclareOpaqueTexture.hlsl` | 相同的文件 | `ShaderLibrary/` |
| 渲染器功能 | `ScriptableRenderPass.Execute`；Render Graph 为内部实现 | Render Graph（`RecordRenderGraph`）；自 Unity 6.4 起没有兼容模式：`RenderGraphSettings.enableRenderCompatibilityMode` 始终返回 false | `Runtime/Passes/ScriptableRenderPass.cs`、`Runtime/Settings/RenderGraphSettings.cs` |
| 预览相机的 Opaque Texture | 预览相机不复制（`copyColorPass &= !isPreviewCamera`） | 复制 | `Runtime/UniversalRenderer.cs`。因此 Urpify 在 2022.3 上的自检用游戏相机渲染 GrabPass 着色器。 |
| URP Asset 的纹理 | 当 URP Asset 的 `m_Textures`（blue noise、Bayer）为空时（Unity 转换器保存时就是这样），构建出的播放器每帧抛出 `NullReferenceException` 并渲染为黑色 | 在相同项目的 6000.5 播放器中未观察到 | `UniversalRenderPipeline.cs`（`SetupPerFrameShaderConstants`）。免费包会在 Unity 转换器之后填充它们。 |

## Unity 转换器的差异

| 主题 | 2022.3 / URP 14 | 6000.5 / URP 17.5 |
|---|---|---|
| `Legacy Shaders/Particles/VertexLit Blended` 和 `Mobile/Bumped Specular (1 Directional Realtime Light)` 的材质转换规则 | 以旧着色器名称注册，因此 Unity 转换器会跳过它们；Urpify 用自己的表转换这些材质 | Unity 自己转换 |
| 不带 `-typesFilter` 的批处理模式 | 未测量：Urpify 在此版本中也不使用 Unity 的批处理命令 | 抛出 `NullReferenceException`（`Converters.cs:134`）；Urpify 通过 API 调用转换器 |
| 成功运行后 Unity 批处理命令的退出码 | 未测量 | 4 个真实项目中有 3 个以代码 21 或 139 结束（进程在退出时崩溃）；Urpify 在两个版本中都根据转换器日志和重新扫描判断结果 |
