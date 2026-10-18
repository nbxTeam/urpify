# 自定义光照函数

![展台 S05：卡通渐变光照函数，经 Unity 转换器后为粉色，经 Urpify 后一致](../../images/S05.jpg)

*展台 S05：带自定义光照函数（卡通渐变）的表面着色器。*

## 什么会损坏

表面着色器可以命名自己的光照模型 `#pragma surface surf Toon`，并定义 `LightingToon`。Built-in 对每个光源调用一次：主光源在
ForwardBase 中，每个额外光源在 ForwardAdd 中，并使用 `_LightColor0` 和光源的衰减。URP 没有表面着色器，因此物体为粉色。

发现：“自定义光照函数 ...”（`SHADER.SURFACE.CUSTOM_LIGHTING`）。

## Urpify 的做法（Pro）

作者的函数原样复制。生成的前向 Pass 先为 URP 的主光源调用它，再为每个额外光源调用，使用 URP 的阴影和衰减，并把结果相加，
与 Built-in 的两个 Pass 的做法一致。在函数内部，`_LightColor0` 是本次调用所针对的光源颜色。

能识别 Built-in 的所有签名：

| 签名 | 示例 |
|---|---|
| 不带视线方向 | `half4 LightingToon(SurfaceOutput s, half3 lightDir, half atten)` |
| 带视线方向 | `half4 LightingToon(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)` |
| 带全局光照 | `half4 LightingToon(SurfaceOutput s, half3 viewDir, UnityGI gi)` 及其 `LightingToon_GI` |
| 延迟和预通道变体 | `_Deferred`、`_PrePass`：不使用，URP 以前向方式渲染 |

只要编译器能找到该函数的地方，都能找到它：程序中、`CGINCLUDE` 块中、任意深度的项目 include 文件中，以及声明函数的
`#define` 宏中。

## 有多接近

`自动（近似）`。Built-in 用单独的叠加混合 Pass 叠加每个额外光源；URP 在一个 Pass 中照亮它们。对不透明物体，结果相同；
对透明着色器，在光源重叠处可能有差异。环境光与 Built-in 一样来自 URP 的球谐函数。

如果该函数既不在着色器中，也不在扫描到的 include 文件中，发现会指出它的名称，在其 include 文件进入扫描范围之前该着色器
不会被转换。
