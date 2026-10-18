# GrabPass

![展台 G01：使用 GrabPass 折射的玻璃，经 Unity 转换器后消失，经 Urpify 后重新折射](../../images/G01.jpg)

*展台 G01：带折射和无名 `GrabPass {}` 的玻璃。只经过 Unity 转换器时，玻璃消失，没有任何报错。*

![展台 G02：使用命名 GrabPass 的热浪扭曲](../../images/G02.jpg)

*展台 G02：使用命名 `GrabPass { "_HazeBackground" }` 的热浪扭曲。*

## 什么会损坏

URP 没有 GrabPass。抓取屏幕的 Pass 被跳过，纹理保持为空，物体消失或失去折射。没有粉色也没有错误：这是无提示的损坏。

发现：“GrabPass（...）”（`SHADER.GRABPASS`），严重程度“无报错地损坏”。

## Urpify 的做法（Pro）

移除 GrabPass，让它原本填充的纹理（`_GrabTexture` 或命名的纹理）改为读取屏幕的副本：

- **GrabPass 来源 = Opaque Texture**（默认）：URP 的 `_CameraOpaqueTexture`，在不透明物体和天空之后、透明物体之前拍下。
  Urpify 会在每个 URP Asset 中开启 Opaque Texture，且不降低分辨率。
- **GrabPass 来源 = 透明对象之后**：Urpify 的 Built-in Camera Hooks 渲染器功能在透明物体之后把屏幕复制到
  `_UrpifyGrabTexture`，再绘制带 GrabPass 的物体（标签 `UrpifyGrabPass`），与 Built-in 中 GrabPass 的做法一致。每帧多一次
  屏幕复制。

位于同一着色器一两个 Pass 之后的 GrabPass（先雾、再一个读取抓取结果的模糊）会读取在这些 Pass 之后拍下的副本，因此看到的
与 Built-in 中抓取的内容一致。

## 有多接近

使用 Opaque Texture 时为 `自动（近似）`：玻璃后面的透明物体不会出现在折射中。使用“透明对象之后”时，结果与 Built-in 一致。
一个着色器中有多个使用不同纹理的 GrabPass 暂不支持：URP 只有一张 Opaque Texture。

在 2022.3 上，Urpify 的自检用游戏相机渲染 GrabPass 着色器：URP 14 不为预览相机复制 Opaque Texture。
