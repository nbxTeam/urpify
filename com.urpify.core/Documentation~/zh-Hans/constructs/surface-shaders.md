# 表面着色器

![展台 S01：Standard 表面着色器，经 Unity 转换器后为粉色，经 Urpify 后与 Built-in 一致](../../images/S01.jpg)

*展台 S01：使用 Standard 光照模型、反照率、法线贴图和遮蔽的表面着色器。*

## 什么会损坏

`#pragma surface` 是内置管线的功能：Unity 根据表面函数生成着色器的各个 Pass（ForwardBase、ForwardAdd、ShadowCaster、
Deferred、Meta）。URP 没有表面着色器，这些 Pass 会用错误着色器绘制，因此使用这类着色器的每个物体都会变成粉色。Unity 转换器
不处理自定义着色器。

发现：“表面着色器，光照 ...”（`SHADER.SURFACE`），严重程度“渲染为粉色”。

## Urpify 的做法（Pro）

转换器原样复制作者的表面函数，并在其周围写出 URP 需要的 Pass：

| Pass | 来源 |
|---|---|
| `UniversalForward` | 表面函数，然后是该光照模型对应的 URP 光照 |
| `ShadowCaster` | 同样的顶点代码，加上 URP 的阴影偏移 |
| `DepthOnly`、`DepthNormals` | 同样的顶点代码；用于深度纹理、深度预通道和 SSAO |
| `Meta` | 表面函数的反照率和自发光，用于烘焙光照贴图 |

| 光照模型 | URP 中的光照 |
|---|---|
| `Standard`、`StandardSpecular` | `UniversalFragmentPBR`，金属或高光工作流 |
| `Lambert`、`BlinnPhong` | Urpify 库中 Built-in 的 `LightingLambert` 和 `LightingBlinnPhong`，用于主光源和每个额外光源 |
| 你自己的 `Lighting<Name>` | 你的函数，参见 [自定义光照](surface-custom-lighting.md) |

`Input` 结构的填充方式与 Built-in 相同：带纹理平铺的 `uv_` 和 `uv2_` 坐标、`worldPos`、`worldNormal`、`worldRefl`、
`viewDir`、`screenPos`、`color : COLOR`、`facing : VFACE`、带 `WorldNormalVector` 的 `INTERNAL_DATA`，以及你的 `vertex:`
函数填充的字段。材质属性为 SRP Batcher 移入 `UnityPerMaterial`，着色器名称和属性名称保持不变，因此材质保留其数值。

多个 `SubShader` 块和 LOD 保持原有含义。指向内置着色器的 `Fallback` 会改为指向对应的 URP 着色器。

## 有多接近

Standard、StandardSpecular、Lambert 和 BlinnPhong 为 `自动`：除了 URP 自身光照的差异（参见
[URP 外观上的差异](../urp-differences.md)）之外，结果与 Built-in 一致。每个转换后的着色器还会在球体上分别用 Built-in 和
URP 渲染；若超过 25 % 的球体发生变化，就会被标记为 `检查`。

![展台 S02：StandardSpecular](../../images/S02.jpg)

*S02：StandardSpecular。*

![展台 S04：使用 alpha:fade 的 BlinnPhong](../../images/S04.jpg)

*S04：使用 `alpha:fade` 的 BlinnPhong，一个透明球体。*
