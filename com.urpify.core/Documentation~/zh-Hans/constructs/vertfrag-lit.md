# 带光照的 vert/frag 着色器

![展台 V02：带 ForwardBase、ForwardAdd 和阴影的带光照 vert/frag 着色器](../../images/V02.jpg)

*展台 V02：ForwardBase 和 ForwardAdd Pass、`SHADOW_ATTENUATION`，以及一个投射阴影的 Pass。*

## 什么会损坏

URP 用错误着色器绘制带有 `ForwardBase`、`ForwardAdd`、`Vertex`、`VertexLM` 或 `PrepassBase` 标签的 Pass，因此物体为粉色。
Built-in 的光源变量（`_LightColor0`、`_WorldSpaceLightPos0`）、阴影宏（`SHADOW_COORDS`、`TRANSFER_SHADOW`、
`SHADOW_ATTENUATION`、`UNITY_LIGHT_ATTENUATION`）以及 `UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"` 在 URP 中都没有
对应物。

发现：“ForwardBase Pass”“ForwardAdd Pass”“来自 Built-in 的 ShadowCaster Pass”“LightMode ...”“UsePass ...”。

## Urpify 的做法（Pro）

| Built-in | URP |
|---|---|
| `ForwardBase` | `UniversalForward`；光照、阴影和环境光通过 Built-in 名称从 URP 获取 |
| `ForwardAdd` | 在前向 Pass 中遍历 URP 的额外光源：作者的片元代码每个光源调用一次，结果相加 |
| `ShadowCaster` | 使用 `ApplyShadowBias` 的 URP `ShadowCaster`，保留作者的顶点代码；一个 `DepthOnly` 副本绘制深度预通道 |
| `UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"` | 生成的阴影 Pass |
| 来自项目中另一个着色器的 `UsePass` | 复制该 Pass，并与其余部分一起转换 |
| `Vertex`、`VertexLM`（仅逐顶点光照） | `UniversalForward`，由 URP 的主光源和额外光源进行逐顶点光照 |
| `_LightColor0`、`_WorldSpaceLightPos0` | 当前绘制 Pass 所针对的光源 |
| `UNITY_LIGHT_ATTENUATION`、`SHADOW_ATTENUATION`、`LIGHT_ATTENUATION` | 来自 URP 的阴影和距离衰减 |
| `ShadeSH9`、`UNITY_FOG_*`、`UnityObjectToClipPos` 及 `UnityCG.cginc` 的其余内容 | Urpify 的库 `BirpCompat.hlsl` |

作者的 `vert` 和 `frag` 原样复制。CG 块变为 HLSL 块，`fixed` 变为 `half`，`sampler2D`/`tex2D` 保持不变：它们在 URP 14 和
17 中都能编译。

## 有多接近

只有一个 ForwardBase Pass 时为 `自动`，带 ForwardAdd 时为 `自动（近似）`：Built-in 用单独的 Pass 混合每个额外光源，URP
在一个 Pass 中把它们相加。Built-in 交给 ForwardBase 的逐顶点光源（`Shade4PointLights`）改为在循环中逐像素绘制。
