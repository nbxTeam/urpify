# 无光照的 vert/frag 着色器

![展台 V01：带雾和 UV 滚动的无光照 CG 着色器，三张图相同](../../images/V01.jpg)

*展台 V01：带雾和 UV 滚动的无光照 CG 着色器。URP 按原样绘制它。*

## 会发生什么

没有 LightMode 标签或标签为 `Always` 的 vert/frag 着色器，通常在 URP 中无需改动即可渲染：URP 把没有 LightMode 标签的 Pass
当作 `SRPDefaultUnlit` 绘制，使用 `UnityCG.cginc` 的 CG 代码也仍能编译。Urpify 会检查着色器能否在 URP 中编译，能编译就不
改动它。

发现：无，或一条说明 URP 按原样绘制该着色器的备注。

仍可能损坏、且各有专页的情况：
- 两个及以上没有 LightMode 的 Pass：[额外 Pass](extra-passes.md)；
- 读取 `_CameraDepthTexture`：[深度纹理](depth-texture.md)；
- `GrabPass`：[GrabPass](grabpass.md)；
- 无光照 Pass 中使用 `_LightColor0` 等 Built-in 光源变量：在 URP 中它们为零；Pro 会把它们映射到 URP 的主光源。
