# 设置

Project Settings > **Urpify**，或 Urpify 窗口中的 **设置** 按钮。

团队设置保存在 `ProjectSettings/UrpifySettings.asset` 中：提交这个文件，整个团队的转换方式就会一致。个人设置保存在
`UserSettings/Urpify.json` 中，只留在你的电脑上。

## 转换（团队）

这些设置决定 Pro 转换器写出什么。

| 设置 | 取值 | 默认 | 作用 |
|---|---|---|---|
| 输出 | 替换原文件、写入副本、保留 Built-in 支持 | 替换原文件 | **替换原文件**：着色器文件变为仅支持 URP；原文件保存在备份中。**写入副本**：URP 着色器写在原文件旁边，文件名为 `Name_URP.shader`，着色器名为 `Name (URP)`，材质改用它；原文件保留。**保留 Built-in 支持**：两种管线通用的单个文件；原有 SubShader 保留给 Built-in，并在其前面添加一个 URP SubShader。 |
| GrabPass 来源 | Opaque Texture、透明对象之后 | Opaque Texture | **Opaque Texture**：转换后的 GrabPass 读取 URP 在透明对象之前拍下的屏幕副本，因此折射中看不到透明对象。**透明对象之后**：Urpify 的渲染器功能在透明对象之后复制屏幕（与 Built-in 中 GrabPass 的做法一致），再绘制使用 GrabPass 的对象。每帧多一次屏幕复制。 |
| 屏幕效果插入位置 | 后期处理之后、后期处理之前、不透明对象之后 | 后期处理之后 | 转换后的 `OnRenderImage` 效果在 URP 帧中的运行位置。带 `[ImageEffectOpaque]` 的效果始终在天空之后运行，与 Built-in 一致。 |
| 阴影 | 与原文件相同、始终、从不 | 与原文件相同 | 转换后的表面着色器是否投射和接收阴影。 |
| SRP Batcher | 开启、关闭 | 开启 | 把材质属性放入 `UnityPerMaterial` 缓冲区，让 URP 的 SRP Batcher 合批转换后的着色器。自己声明了 `UnityPerMaterial` 的着色器会保留它；无法安全移动的属性会留在外面，并附上说明。 |

## 比较（团队）

| 设置 | 默认 | 作用 |
|---|---|---|
| 捕获尺寸 | Game 视图 | 每帧捕获画面的尺寸：Game 视图的尺寸（依赖分辨率的效果会和你看到的一样），或自定义。命令行使用 `-urpifyWidth` 和 `-urpifyHeight`。 |
| 捕获宽度、捕获高度 | 960 × 540 | 捕获尺寸为自定义时以及没有 Game 视图时使用的尺寸。 |
| 捕获前的帧数 | 30 | 每次捕获前以固定时间步长运行的帧数，使动画和粒子在迁移前后处于相同状态。 |
| 场景 | Build Settings | “迁移前捕获”和“迁移后捕获”使用的场景：Build Settings 中的场景、`Assets` 下的所有场景，或一个列表。 |
| 场景路径 | 空 | 用于“选定的场景”：以分号分隔的路径，例如 `Assets/Scenes/Main.unity;Assets/Scenes/Menu.unity`。 |

“新的粉色”“有变化”“相同”的阈值以 Unity 自己的 Standard 到 URP Lit 转换为基准校准，适用于任何项目：新增的粉色区域超过画面
的 0.02 % 即为“新的粉色”；变化的像素多于 Standard → URP Lit 所产生的，或有一个大的变化区域，即为“有变化”。

每个场景都可以有 **Migration Viewpoint**：把 `MigrationViewpoint` 组件添加到一个空 GameObject 上，除主相机外，还会从它的
位置和朝向进行捕获。视点可以有自己的视野和远裁剪面。它带有 EditorOnly 标签，永远不会进入构建。

## 个人

| 设置 | 取值 | 默认 | 作用 |
|---|---|---|---|
| 语言 | 编辑器语言，或 8 种语言之一 | 编辑器语言 | Urpify 窗口的语言。“编辑器语言”跟随 Preferences > Languages，若没有该语言则使用英语。 |
| Built-in 预览 | 实时、来自捕获 | 实时 | **实时** 在渲染 Built-in 预览时关闭渲染管线并再开启，在一次调用内完成；不会在 Play Mode 中、导入或编译期间进行。**来自捕获** 改为显示场景捕获。如果实时预览失败，或管线设置未能精确恢复，Urpify 会自动切换到“来自捕获”并告知你。 |
| Project 窗口标记 | 开启、关闭 | 开启 | 在上次扫描发现损坏的资源上显示一个小标记。 |

## 单次运行的覆盖

命令行可以在单次运行中覆盖“输出”（`-urpifyShaderOutput`）和“GrabPass 来源”（`-urpifyGrabPass`），参见
[命令行](cli.md)。
