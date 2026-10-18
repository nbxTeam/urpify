# 曲面细分

![展台 S09：带置换的曲面细分](../../images/S09.jpg)

*展台 S09：带 `tessellate:` 和置换的表面着色器。*

## 什么会损坏

`tessellate:func` 让 Unity 为表面着色器生成外壳（hull）和域（domain）阶段。URP 没有表面着色器，因此物体为粉色。

发现：“曲面细分（...）”（`SHADER.SURFACE.TESSELLATION`）。

## Urpify 的做法（Pro）

转换器按照与内置管线一致的模板生成外壳和域阶段：作者的曲面细分函数给出系数，分割方式为 `fractional_odd`，然后 `vertex:`
函数移动新顶点。绘制该物体的每个 Pass（前向、阴影、深度）都会细分，因此阴影也跟随置换后的表面。`tessphong:` 以指定强度
添加 Phong 平滑。

自己编写外壳和域阶段的 vert/frag 着色器会保留它们；Built-in 定义的属性（`UNITY_domain`、`UNITY_partitioning`、
`UNITY_outputtopology`、`UNITY_patchconstantfunc`、`UNITY_outputcontrolpoints`）来自 Urpify 的库，编译检查也覆盖外壳和域
阶段。

## 有多接近

`自动（近似）`：相同的系数和置换，在其上应用 URP 光照。
