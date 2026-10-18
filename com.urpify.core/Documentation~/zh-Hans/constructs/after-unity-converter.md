# Unity 转换器之后的修复

Unity 的 Render Pipeline Converter 是迁移中最先运行的步骤，大多数情况下它是对的。下面是它的结果明显有误的情况，以及免费包
随后所做的处理。这些都在免费包的“Urpify 转换器”步骤中。

## Transparent 模式的 Standard

![展台 STD04：Standard Transparent 模式的玻璃，经 Unity 转换器后变白，经 Urpify 后恢复为玻璃](../../images/STD04.jpg)

*STD04：Standard 的 Transparent 模式玻璃。*

Unity 转换器把 Transparent 映射为 URP 的 Premultiply 混合，而它要求颜色已经乘以 alpha。玻璃变得几乎不透明且发白。Urpify
把材质改为带 Preserve Specular Lighting 的 Alpha 混合，这正是 Built-in 的混合方式。发现：“...，Transparent 模式”
（`MATERIAL.STANDARD_TRANSPARENT`）。

## Legacy Self-Illumin

![展台 LEG03：Legacy Self-Illumin，经 Unity 转换器后变暗，经 Urpify 后发光](../../images/LEG03.jpg)

*LEG03：`Legacy Shaders/Self-Illumin/Diffuse`。*

Unity 转换器把这些材质改为 Simple Lit，而它不会从 Illumin 的 alpha 中取自发光：光消失了。Urpify 把它们改为自己的 URP
Self-Illumin 着色器，发光效果与 Built-in 一致。发现：`MATERIAL.SELF_ILLUMIN`。

## 编辑器的内置材质

使用编辑器内置 `Default-Material` 或 `Default-Diffuse` 的渲染器（新建的立方体、平面），会被 Unity 转换器指向 URP 的默认
材质，而它是中灰色而不是白色。Urpify 把它们指向带有内置材质数值的 URP 副本。发现：`SCENE.BUILTIN_MATERIAL`。

## 包中的材质

包中的文件是只读的，因此 Unity 转换器永远不会改动位于包中的材质。如果它的着色器只适用于 Built-in，使用它的每个渲染器都会
变成粉色：ProBuilder 5 的墙使用 `ProBuilderDefault.mat`，其着色器 `ProBuilder/Standard Vertex Color` 是一个表面着色器。
Urpify 会把场景和预制体中的这些渲染器指向 `Assets/Urpify/Materials/Packages/` 中的 URP 副本：内置着色器使用 Unity 自己的
升级器（`自动`），否则使用带有该材质颜色、纹理、光滑度和金属度的 URP Lit（`自动（近似）`：着色器本身添加的内容，例如顶点色，
不会保留）。发现：`SCENE.PACKAGE_MATERIAL`。

## 没有升级器的内置着色器

部分内置着色器在 Unity 转换器中没有升级器（“No upgrader found”）：`Legacy Shaders/Decal`、
`Legacy Shaders/Transparent/VertexLit`、`Transparent/Parallax Diffuse` 和 `Specular`，以及 URP 14 中两个以旧名称注册的
升级器。Urpify 把这些材质改为自己表中最接近的 URP 着色器，并迁移属性。发现：`MATERIAL.BUILTIN_NO_UPGRADER`。

## Post-processing Stack v2

Unity 转换器会把 PPv2 的体积和配置文件转换为 URP 的 Volume，但在批处理模式下不会等待它所需的资源搜索，因此一个也转换不了：
泛光在没有任何报错的情况下消失。Urpify 的“运行 Unity 转换器”步骤会等待搜索，然后在迁移泛光参数时做校准（threshold 和
scatter），让光晕更接近 Built-in。其余差异来自 URP 自身的泛光，参见 [URP 外观上的差异](../urp-differences.md)。发现：
`SCENE.PPV2`。

## URP Asset 设置

- **Depth Texture 和 Opaque Texture**：当 URP 按原样绘制的着色器读取它们时，在每个 URP Asset 中开启（参见
  [深度纹理](depth-texture.md)）。
- **URP Asset 纹理**（URP 14）：Unity 转换器保存的 URP Asset 中 blue noise 和 Bayer 纹理为空。编辑器会填充它们，构建出的
  播放器不会：它每帧抛出 `NullReferenceException` 并渲染为黑色。Urpify 从 URP 包中填充它们。发现：
  `SETTINGS.URP_ASSET_TEXTURES`。
- **主光源**：在 Pixel Light Count 为 0 的质量等级上，Built-in 仍对主方向光逐像素照明；Urpify 在这些等级上把 URP 的 Main
  Light 设为 Per Pixel。

## Material Variant

Unity 转换器把 Material Variant 改为其父材质的 URP 着色器，却丢弃了变体自身的数值：它成了父材质的一个无声副本。Urpify 从
“运行 Unity 转换器”步骤的备份中读取变体自身的数值，并作为 URP 属性的覆盖值写回。发现：`MATERIAL.VARIANT`。

## 模型中的只读材质

嵌入在 FBX 文件中的材质是只读的，而 Unity 转换器处理它们的模块在批处理模式下运行并不可靠。Urpify 针对找到的渲染器和地形，
逐个对象运行该模块，两个 Unity 版本都是如此。
