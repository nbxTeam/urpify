# 额外 Pass：描边、壳层、分层

![展台 S12：带反转外壳描边 Pass 的卡通着色器](../../images/S12.jpg)

*展台 S12：带手写描边 Pass 的卡通表面着色器。只经过 Unity 转换器时，只剩下粉色球体周围的一圈描边。*

## 什么会损坏

URP 对一个物体最多只绘制 SubShader 中的两个 Pass：一个 `SRPDefaultUnlit` 和一个 `UniversalForward`，按此顺序。Built-in
会绘制每一个 Pass。光照 Pass 旁带有描边 Pass 的着色器、毛发壳层、阴影线分层，或两个没有 LightMode 标签的 Pass，最后只会
保留其中一个，而且往往没有任何提示。

发现：“表面程序旁的额外 Pass”（`SHADER.SURFACE.EXTRA_PASS`）、“没有 LightMode 的 Pass：N”
（`SHADER.PASS.MULTIPLE_UNTAGGED`）。

## Urpify 的做法（Pro）

- **两个绘制 Pass：** 第一个变为 `SRPDefaultUnlit`，第二个变为 `UniversalForward`，因此保留 Built-in 的绘制顺序。已在
  URP 14 和 17 上验证。
- **三个及以上的绘制 Pass：** 从第三个开始，Pass 获得 `UrpifyPass3`、`UrpifyPass4` 等标签。Urpify 的 Built-in Camera
  Hooks 渲染器功能在 URP 的不透明或透明物体之后，按顺序绘制每个物体的这些 Pass。每个 SubShader 最多 32 个绘制 Pass。
- **表面程序旁手写的 Pass** 会保留，并按 vert/frag 的规则转换。
- 由 `Graphics.Blit` 绘制的图像效果着色器不加标签：它们的 Pass 按索引选择。

渲染器功能会在“Urpify 转换器”步骤中添加到每个需要它的 URP 渲染器。

## 有多接近

两个 Pass 为 `自动`，三个及以上为 `自动（近似）`。当屏幕上有多个这样的物体时，每个物体的前两个 Pass 会先于任何物体的额外
Pass 绘制，这可能改变重叠物体的排序。
