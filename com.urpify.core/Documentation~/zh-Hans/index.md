# Urpify

Urpify 将 Unity 项目、着色器或 Asset Store 包从 Built-in Render Pipeline 迁移到 URP。在任何改动之前，它会先列出切换后会
损坏的所有内容，包括那些不报错就损坏的内容。然后转换它能转换的部分，备份它改动的每个文件，并用捕获的画面逐个对比每个场景
迁移前后的样子。每一步都可以回滚。

Urpify 分为两个包：

| | Urpify（免费，MIT） | Urpify Pro |
|---|---|---|
| 扫描项目、文件夹、文件或 `.unitypackage` | 是 | 是 |
| 用捕获的画面对比迁移前后的场景 | 是 | 是 |
| 从窗口或命令行可靠地运行 Unity 的 Render Pipeline Converter | 是 | 是 |
| Unity 转换器之后的修复（玻璃、Self-Illumin、内置材质、Post-processing v2 泛光、URP Asset 设置、包中的材质） | 是 | 是 |
| 备份与回滚 | 是 | 是 |
| 着色器面板：代码，以及 Built-in 和 URP 下的现状预览 | 是 | 是 |
| 转换自定义着色器、图像效果、相机脚本、命令缓冲区和投影器 | 否 | 是 |
| 输出：两种管线通用的单个着色器文件（保留 Built-in 支持） | 否 | 是 |
| 导出转换后的 `.unitypackage` | 仅报告 | 是 |
| 命令行 | 扫描、捕获、对比、Unity 转换器和免费修复 | 全部 |

## 目录

从这里开始：
- [快速入门](quick-start.md)：用演示场景或你的项目，在 5 分钟内走完整个流程。
- [迁移游戏](scenario-game.md)
- [转换着色器](scenario-shader.md)
- [将 Asset Store 包转换为两种管线通用](scenario-asset.md)

Urpify 如何处理 Built-in Render Pipeline 的每种结构：
- [结构概览](constructs/index.md)：每种结构一页，附迁移前后的图片。
- [哪些内容不会被转换，以及原因](not-converted.md)
- [URP 外观上的差异](urp-differences.md)

参考：
- [Urpify 窗口](window.md)：图标、计数、判定。
- [设置](settings.md)
- [命令行](cli.md)
- [报告结构](../report-schema.md)（英文）
- [Unity 与 URP 版本](urp-version-matrix.md)
- [常见问题](faq.md)
- [再分发转换后的着色器](../license-redistribution.md)（英文）

## 要求

- Unity 2022.3 LTS（URP 14），或 Unity 6000.0 LTS、6000.3 LTS、6000.5、6000.6（URP 17），即已测试的版本。参见 [Unity 与 URP 版本](urp-version-matrix.md)。
- 使用 Built-in Render Pipeline 的项目，或为其制作的 `.unitypackage`。迁移需要时，Urpify 会自行安装 URP。
- 无需其他包。除了你主动通过 `发送报告` 发送的着色器压缩包之外，Urpify 不会通过网络发送任何内容（参见
  [常见问题](faq.md)）。

## 语言

窗口支持 English、简体中文、日本語、한국어、Русский、Español、Português (Brasil) 和 Deutsch。默认跟随编辑器语言
（Preferences > Languages），若 Urpify 没有该语言则使用英语。可在 Project Settings > Urpify > 个人 > 语言 中更改。报告中的
转换器说明、命令行以及 HTML 报告的技术细节保持英文。
