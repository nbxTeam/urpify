# 图像效果：OnRenderImage

![展台 C02：去饱和加暗角的图像效果，经 Unity 转换器后消失，经 Urpify 后恢复](../../images/C02.jpg)

*展台 C02：使用 `OnRenderImage` 的后期效果，去饱和并加暗角。只经过 Unity 转换器时，效果消失，没有任何报错。*

## 什么会损坏

URP 从不调用 `OnRenderImage`，因此为 Built-in 编写的每个图像效果都会失效。没有错误，也没有粉色：游戏只是看起来不同了。已在
URP 14 和 17 上验证。

发现：“OnRenderImage（{0} 次 Blit 调用）”（`SCRIPT.ONRENDERIMAGE`），严重程度“无报错地损坏”。效果的着色器会得到发现“图像
效果着色器”。

## Urpify 的做法（Pro）

脚本仍是相机上的 MonoBehaviour，继续充当控制面板：它的字段、每帧设置的数值和材质都不变。Urpify 的 Built-in Camera Hooks
渲染器功能在设置所选的 URP 帧位置（“屏幕效果插入位置”：默认在后期处理之后），以相机颜色为源调用脚本的 `OnRenderImage`。
脚本的 `Graphics.Blit` 调用经过 `ImageEffectGraphics`，以相同的材质、Pass 和临时渲染纹理、按相同的顺序记录到 URP 的帧中。
带 `[ImageEffectOpaque]` 的效果与 Built-in 一样在天空之后运行。一台相机上的多个效果保持其顺序。`OnRenderImage` 中用 `GL`
进行的绘制（四边形、矩阵、清除）也会被记录。

效果的着色器保持不变：`vert_img`、`v2f_img` 和 `_MainTex` 都能工作，因为效果仍像在 Built-in 中一样把屏幕传入 `_MainTex`。

## 有多接近

单个 Blit 为 `自动`，链式调用和命令缓冲区为 `自动（近似）`。用 `Graphics.DrawMeshNow` 等立即绘制、或渲染相机
（`Camera.Render`）的效果无法记录到 URP 的帧中：发现会说明如何重新组织。

效果所调用的其他脚本中的 `Graphics.Blit` 调用（相机脚本背后的 ScriptableObject 效果、执行一条效果链的管理脚本）
也会以同样方式录制。如果效果链会渲染相机、派发计算着色器，或读取内置管线的 G-buffer（`_CameraGBufferTexture0-3`），
该效果会保持不变（暂不支持）：在 URP 下它缺失，而帧的其余部分照常绘制。如果迁移后某个场景的画面几乎变成单一颜色，
该场景运行的效果会被标为待检查：在相机上关闭它们以找出原因，或回滚。

构建出的播放器会保留该效果：Urpify 按名称调用的所有内容都受到保护，不会被代码剥离，已在 Mono 和 IL2CPP 构建中验证。
