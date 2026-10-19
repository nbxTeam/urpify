# 快速入门

用 5 分钟走完整个流程：扫描、迁移、对比。先在演示场景上试，再在你的项目副本上试。

## 1. 安装

在仍使用 Built-in Render Pipeline 的项目中安装：

- **Urpify Pro：** 只有一个文件 `Urpify-Pro-1.0.0.unitypackage`（来自 Asset Store：Package Manager > My Assets > Import）。Assets > Import Package > Custom Package，全部导入：Urpify、Urpify Pro、演示和这份文档都在里面。
  已经从 `.unitypackage` 安装了免费版 Urpify：直接在上面导入 Pro，不用删除任何东西。用 Package Manager 安装的：先在那里
  移除它（Package Manager > Urpify > Remove），因为 Urpify Pro 已经包含它。
- **Urpify（免费版）：** 从 [GitHub Releases](https://github.com/nbxTeam/urpify/releases/latest) 下载
  `Urpify-1.0.0.unitypackage`，用同样的方式导入；或者 Package Manager > `+` > Add package from git URL：
  `https://github.com/nbxTeam/urpify.git?path=com.urpify.core#v1.0.0`。

迁移前请把项目提交到版本控制。Urpify 会备份它改动的每个文件，但提交才是最安全的恢复点。

## 2. 打开演示（可选）

选择 Help > Urpify > **Import Demo**，或在空的 Urpify 窗口中点击 **导入演示**。演示会导入到 `Assets/Samples/Urpify/1.0.0/Demo`，并打开它的场景。
用 Package Manager 安装时，Package Manager > Urpify > Samples > **Demo** > Import 也能做到同样的事。场景里为每种在 URP 中会
损坏的结构都设置了一个展台：表面着色器、带光照的 vert/frag 着色器、GrabPass 玻璃、读取深度的水、投影器、由脚本渲染的
镜子、在代码中创建的材质，以及 Standard Transparent 模式的玻璃。每个展台都有一个 Migration Viewpoint，因此对比时可以
近距离查看每一个。

## 3. 扫描

Window > Rendering > **Urpify**，然后点击 **扫描项目**。

扫描只读取文件。左侧列表按类型分组，显示在 URP 中会损坏的每个着色器、脚本、材质、场景和设置。每一行都有一个图标：

- 实心方块：渲染为粉色；
- 实心三角：无报错地损坏（传送门变成墙、水失去泡沫、效果消失）；
- 实心菱形：外观不同；
- 圆环：无可见变化。

选中一行可查看哪里损坏、原因以及由谁修复。着色器会在着色器面板中打开：显示转换前后的代码，以及 Built-in 和 URP 的并排
预览。

## 4. 迁移

点击 **迁移...**。窗口会列出各个步骤：

1. **扫描**：同样的只读扫描。
2. **备份**：将项目的资源、设置和包列表复制到 `Urpify/Backup`。
3. **迁移前捕获**：在 Play Mode 中运行每个场景，保存主相机和每个 Migration Viewpoint 的画面。
4. **安装 URP**：添加与你的编辑器配套的 URP 版本。
5. **运行 Unity 转换器**：运行 Unity 的 Render Pipeline Converter，并附带 Urpify 为可靠运行所需的变通处理。
6. **Urpify 转换器**：修复 Unity 转换器遗留的问题。使用 Pro 时，还会转换着色器、脚本和组件。
7. **迁移后捕获并比较**：在 URP 中拍摄同样的画面，并与迁移前的画面比较。
8. **总结**：最终报告。

点击 **全部运行**。每个步骤也可以单独运行，并且都可以回滚：在某一步上点 **回滚到此处**，或点 **全部回滚**。

## 5. 对比

点击 **比较场景**。每个场景都会显示迁移前后的画面，中间有一个滑块。**显示差异** 会标出变化的像素：越红，变化越大。
**仅显示有变化的** 会隐藏看起来相同的场景。

在主窗口中，每个场景有两个判定：来自扫描的 **静态**，以及来自画面的 **观测**。粉色物体可能不在任何捕获的画面中，因此
静态判定可能比观测判定更差。在你想仔细查看的位置添加一个 Migration Viewpoint（带 `MigrationViewpoint` 组件的空
GameObject），然后重新捕获。

## 6. 完成，或回滚

打开 **报告** > 导出 HTML，可得到一份能发给团队的报告。如果你不满意结果，**全部回滚** 会还原迁移改动过的每个文件，并把
迁移创建的文件移入备份。在步骤之间出现的文件永远不会被动到，因此你自己的工作会保留下来。
在窗口中回滚后，Unity 会立即重启，按磁盘上的状态重新加载项目。

下一步：[迁移游戏](scenario-game.md)、[转换着色器](scenario-shader.md)、
[转换 Asset Store 包](scenario-asset.md)。
