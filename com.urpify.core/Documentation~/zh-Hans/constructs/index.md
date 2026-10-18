# 结构

Urpify 支持的是结构，而不是具体的着色器：着色器用到的每种结构都受支持时，它就会被转换；否则扫描会指出缺少的那一种。下面每
一页解释 Built-in Render Pipeline 的一种结构：在 URP 中什么会损坏、Urpify 如何处理，以及结果有多接近。

图片来自 Urpify 的测试实验室：一个为每种结构设置独立展台的场景。每张图把同一个展台展示三次：Built-in、只经过 Unity 的
Render Pipeline Converter 的 URP，以及经过 Unity 转换器和 Urpify 的 URP。这些是 Urpify 自己的捕获步骤拍下、并按展台裁剪的
画面。

| 结构 | 页面 | 展台 | 由谁修复 |
|---|---|---|---|
| 使用内置光照模型的表面着色器 | [表面着色器](surface-shaders.md) | S01–S04 | Pro |
| 自定义光照函数 | [自定义光照](surface-custom-lighting.md) | S05 | Pro |
| `#pragma surface` 选项：`vertex:`、`finalcolor:`、`decal:`、`alphatest:`、`addshadow` 等 | [表面着色器选项](surface-options.md) | S03、S06–S08、S10、S11 | Pro |
| 表面着色器的曲面细分 | [曲面细分](surface-tessellation.md) | S09 | Pro |
| 额外 Pass：描边、毛发壳层、3 个及以上的 Pass | [额外 Pass](extra-passes.md) | S12 | Pro |
| 无光照的 vert/frag 着色器 | [无光照 vert/frag](vertfrag-unlit.md) | V01 | 无需处理 |
| 带光照的 vert/frag 着色器：ForwardBase、ForwardAdd、阴影、逐顶点光照 | [带光照的 vert/frag](vertfrag-lit.md) | V02 | Pro |
| GrabPass | [GrabPass](grabpass.md) | G01、G02 | Pro |
| `_CameraDepthTexture` | [深度纹理](depth-texture.md) | D01 | Free 和 Pro |
| `_CameraDepthNormalsTexture`、替换着色器 | [深度法线与替换着色器](depth-normals.md) | 无 | Pro |
| `OnRenderImage` 图像效果 | [图像效果](image-effects.md) | C02 | Pro |
| 相机事件上的 `CommandBuffer` | [命令缓冲区](command-buffers.md) | C03 | Pro |
| 渲染相机的脚本：镜子、传送门、水面反射 | [相机脚本](camera-scripts.md) | M01 | Pro |
| `Projector` 组件 | [投影器](projectors.md) | C01 | Pro |
| 使用内置着色器名称的 `Shader.Find` | [代码中的 Shader.Find](shader-find.md) | C04 | Free |
| Unity 转换器会弄坏的内容：Standard Transparent、Self-Illumin、内置材质、包中的材质、Post-processing v2、URP Asset 纹理、Material Variant | [Unity 转换器之后的修复](after-unity-converter.md) | STD04、LEG03 | Free |
| Unity 转换器能处理的内容：Standard、Legacy、Mobile、粒子、地形、导入的模型 | [Unity 转换器](unity-converter.md) | STD01–STD06、LEG01–LEG05、PAR01、PAR02、FBX01、FBX02、TER01 | Unity |
| Tree Creator 和软遮挡树木 | [树木](trees.md) | 无 | Pro |
| Built-in 与 URP 通用的单个着色器文件 | [保留 Built-in 支持](keep-builtin-support.md) | 所有着色器展台 | Pro |

Urpify 不转换的结构及其原因见 [哪些内容不会被转换](../not-converted.md)。
