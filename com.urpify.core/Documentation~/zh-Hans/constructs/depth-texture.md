# 深度纹理

![展台 D01：交界处带泡沫的水，经 Unity 转换器后变白，经 Urpify 后与 Built-in 一致](../../images/D01.jpg)

*展台 D01：读取 `_CameraDepthTexture` 来计算深度颜色和岩石周围泡沫的透明水。只经过 Unity 转换器时，水变成白色。*

## 什么会损坏

读取 `_CameraDepthTexture` 的着色器，只有在 URP Asset 中开启 Depth Texture 时才能得到深度。Unity 转换器只在启用了 Soft
Particles 的质量等级上开启它。透明着色器还有第二个问题：URP 默认在透明物体之后复制深度，而 Unity 转换器保持不变。在
Unity 6 中，这样的着色器完全得不到深度，水变成白色；在 2022.3 中，它读取上一帧的深度，因此静止画面看起来正确，但移动的
物体会留下拖影。

发现：“读取 _CameraDepthTexture”（`SHADER.DEPTH_TEXTURE`）、“透明，读取 ...”（`SHADER.DEPTH_TEXTURE_TRANSPARENT`）。

## Urpify 的做法

- **Free 和 Pro：** 在每个 URP Asset 中开启 Depth Texture，并在有透明着色器读取时，把渲染器的 Depth Texture Mode 设为
  After Opaques。URP 按原样绘制的着色器于是无需改代码即可工作：展台 D01 只靠免费包就能修复。
- **Pro（转换该着色器时）：** 作者的 `sampler2D _CameraDepthTexture` 声明替换为 URP 的 `DeclareDepthTexture.hlsl`，读取
  （`tex2D`、`tex2Dproj`、`SAMPLE_DEPTH_TEXTURE`、`SAMPLE_DEPTH_TEXTURE_PROJ`）改为 `SampleSceneDepth`。这样着色器在深度
  纹理为数组的单通道实例化 XR 中也能工作。`LinearEyeDepth`、`Linear01Depth` 和 `COMPUTE_EYEDEPTH` 来自 Urpify 的库。

## 有多接近

`自动`。如果某处读取无法重写，作者的声明会保留，发现中会说明该着色器不支持单通道实例化 XR。
