# 将 Asset Store 包转换为两种管线通用

你在 Asset Store 上发布着色器或效果，希望一个包同时适用于使用 Built-in 和 URP 的买家。主要操作是 **导出为 Built-in 和
URP 通用**（Pro）。

## 你需要什么

- 一个安装了 URP 的项目。转换后的着色器在写入前会针对 URP 编译，因此没有 URP 时导出会拒绝运行。你的包本身可以仍是
  Built-in 包；项目只用来承载导出。
- 你现在发布的 `.unitypackage`。

## 分析包

在 Urpify 窗口中：**更改**（范围）> **Unity 包（.unitypackage）...**，然后选择你的文件。Urpify 会把它导入到一个临时文件夹
并进行扫描，当你关闭窗口或更改范围时删除该文件夹。你的项目保持不变。

概览会以与项目相同的方式显示对 URP 买家来说会损坏的内容。免费包到此为止：报告（报告 > 导出 HTML）可以用来展示或附上。

## 导出

在概览中点击 **导出为 Built-in 和 URP 通用...**，或 报告 > **导出转换后的包...**，然后选择保存位置。新的 `.unitypackage`
保留原包的每个 GUID 和路径，并包含：

- 转换后的着色器。使用输出 **保留 Built-in 支持**（Project Settings > Urpify > 转换 > 输出）时，每个文件都会原样保留其
  Built-in SubShader，并在其前面加上一个 URP SubShader，因此一个文件可在两种管线中使用；Unity 会选择与当前管线匹配的
  SubShader；
- 原本使用 Unity 内置着色器的材质，按 Unity 转换器在买家项目中的方式升级，之后再应用 Urpify 的修复；
- 像在项目中一样转换的脚本，每个脚本在写入前都会针对项目编译。重写后无法编译的脚本保持原样并被列出；
- Urpify 的着色器库及其小型运行时部分，放在包的每个根文件夹下的 `UrpCompat` 文件夹中，每次导出都使用新的 GUID，运行时
  位于你的包的命名空间中。不同作者的两个包永远不会共享或覆盖彼此的副本；
- 一个以你的包命名的小型编辑器检查。当买家的项目使用 URP 时，它会检查 URP Asset 是否满足你的着色器所需（Depth Texture、
  Opaque Texture、Built-in Camera Hooks 渲染器功能），并在开启任何内容前询问一次。在 Built-in 下它什么也不做。

包旁边会保存一份兼容性报告：每个着色器及其结果，以及买家需要的 URP Asset 设置。

## 以买家身份检查

把导出的包分别导入一个干净的 Built-in 项目和一个干净的 URP 项目。在 Built-in 中，“保留 Built-in 支持”的输出与原版渲染
完全一致：原始 SubShader 不会被改动。在 URP 中，打开包的演示场景并与你的 Built-in 截图对比，或在其上运行迁移的场景比较。

## 再分发

你可以在付费包中分发转换后的着色器和 `UrpCompat` 文件夹。参见
[再分发转换后的着色器](../license-redistribution.md)（英文）。
