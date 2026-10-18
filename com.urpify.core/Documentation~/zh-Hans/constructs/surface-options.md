# 表面着色器的选项

`#pragma surface surf Standard <选项>` 会改变 Unity 生成的内容。Urpify 读取每个选项，并在它写出的 Pass 中保持其含义（Pro）。

![展台 S03：带 vertex: 风函数的 Lambert](../../images/S03.jpg)

*S03：`vertex:vert` 像风一样弯曲网格。顶点函数在每个 Pass 中运行，因此阴影也会弯曲。*

![展台 S08：使用 clip() 和 addshadow 的溶解](../../images/S08.jpg)

*S08：使用 `clip()` 和 `addshadow` 的溶解：阴影也以同样方式被裁剪。*

![展台 S10：decal:add](../../images/S10.jpg)

*S10：`decal:add`，叠加在不透明物体上的加法层。*

![展台 S11：finalcolor: 中的高度雾](../../images/S11.jpg)

*S11：`finalcolor:` 中的高度雾，与 Built-in 一样在光照之后应用。*

## 选项及其结果

| 选项 | 在 URP 中 |
|---|---|
| `vertex:func` | 在生成的每个 Pass 中运行：前向、阴影、深度、深度法线、meta。它填充的额外 `Input` 字段会传给表面函数。 |
| `finalcolor:func` | 以光照后的颜色调用，在雾之前。 |
| `finalgbuffer:func` | 不使用：只在 Built-in 的延迟 G 缓冲区 Pass 中运行。会附带说明。 |
| `alpha`、`alpha:blend`、`alpha:fade`、`alpha:premul` | 透明队列及相应的混合；`premul` 保留预乘的高光。 |
| `alphatest:prop` | 在每个 Pass 中针对该属性执行 `clip()`。 |
| `keepalpha` | 表面函数的 alpha 在不透明 Pass 中也会写入。 |
| `decal:add`、`decal:blend` | 在不透明物体之后以加法或 alpha 混合绘制，不带阴影。 |
| `addshadow` | 阴影 Pass 与前向 Pass 一样运行顶点函数和表面裁剪。 |
| `noshadow` | 没有阴影 Pass；前向 Pass 不接收阴影。 |
| `noambient`、`novertexlights`、`nolightmap`、`nofog`、`nometa`、`noforwardadd` | 去掉对应部分：环境光、逐顶点光源、光照贴图、雾、Meta Pass、额外光源。 |
| `halfasview`、`interpolateview`、`approxview`、`dualforward` | URP 不需要的视线方向质量开关；忽略并附带说明。 |
| `fullforwardshadows`、`nodynlightmap`、`nodirlightmap`、`nolppv`、`noshadowmask`、`noinstancing`、`exclude_path:*`、`dithercrossfade` | 能识别；这些由 URP 自己的设置决定（额外光源阴影、光照贴图模式、光照探针代理体积、LOD 交叉淡化），因此在转换后的着色器中不做任何改变。 |
| `tessellate:func`、`tessphong:` | 参见 [曲面细分](surface-tessellation.md)。 |

Urpify 不认识的选项会列在发现“不支持的表面选项”（`SHADER.SURFACE.UNKNOWN_OPTION`）中；着色器的其余部分仍会被转换。

“阴影”设置（Project Settings > Urpify）可以对所有转换后的表面着色器强制开启或关闭阴影。
