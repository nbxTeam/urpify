# Tree Creator 与软遮挡树木

实验室中没有树木的展台；它们在 ShaderTutorials（ronja-tutorials）中用 Nature Starter Kit 的树木验证过。

## 什么会损坏

URP 没有 Tree Creator 着色器（`Nature/Tree Creator Bark`、`Leaves`、`Leaves Fast`、`Optimized`），也没有软遮挡树木的着色器。
用它们制作的树会渲染为粉色。地形的草和细节网格不受影响：URP 用自己的细节着色器绘制它们。

发现：“...，没有 URP 版本”（`MATERIAL.BUILTIN_TREE`）。

## Urpify 的做法（Pro）

Unity 以 MIT 许可证公开其内置着色器的源码。Urpify Pro 用常规转换器转换 Unity 自己的 8 个树木着色器，把它们所需的
`TerrainEngine.cginc` 函数（风、弯曲、公告板）加入自己的库，并把树皮和树叶材质（包括预制体中的）改为使用转换后的着色器。
许可证文本见 Pro 的 `Third Party Notices.md`。

## 有多接近

`自动（近似）`。树木不再是粉色，并保留其材质。URP 下的风动画和公告板只在几秒钟的画面上目视检查过，未做逐像素测量；请在场景
比较中对比你的场景。SpeedTree 资源由 Unity 转换器升级，而不是 Urpify。
