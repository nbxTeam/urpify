# 深度法线纹理与替换着色器

实验室中没有这些结构的展台；它们在 ToonWaterShader（IronWarrior，Unlicense）上验证过，其水面泡沫同时依赖这两者。

## 什么会损坏

- URP 中没有 `_CameraDepthNormalsTexture`。读取它的着色器什么也得不到：基于法线的描边和沿岸的泡沫消失。
- URP 不支持 `Camera.SetReplacementShader` 和 `Camera.RenderWithShader`。用替换着色器把场景渲染到纹理的脚本（为效果获取
  法线或深度的常见做法）会在没有任何提示的情况下渲染不出有用的内容。ToonWaterShader 的泡沫就是这样丢失的。

发现：“读取 _CameraDepthNormalsTexture”（`SHADER.DEPTHNORMALS_TEXTURE`）、替换着色器调用（`SCRIPT.REPLACEMENT_SHADER`）。

## Urpify 的做法（Pro）

- Urpify 的 Built-in Camera Hooks 渲染器功能用 URP 的深度和法线纹理，以 Built-in 的编码（`EncodeDepthNormal`）写出
  `_CameraDepthNormalsTexture`，因此着色器可以像以前一样读取它。只有在着色器需要时，该功能才会向 URP 请求法线纹理。
- 使用替换着色器的相机变为同一渲染器功能中的一个 Pass，用替换着色器把该相机的不透明物体绘制到同一目标，标签匹配规则
  （`RenderType`）也相同。

## 有多接近

`自动（近似）`。URP 的法线来自它自己的深度法线 Pass，因此边缘可能与 Built-in 略有不同。
