# 命令行

窗口能做的一切在批处理模式下同样可用，因此构建服务器可以扫描、转换一个分支，并在场景变成粉色时让构建失败。免费包可运行
Scan、Capture、Verify、Rollback、所有 Migrate 步骤（其中的“Urpify 转换器”步骤运行免费修复）以及包分析。转换着色器、脚本和
组件，以及导出转换后的包，需要 Pro。

```
Unity -batchmode -projectPath <项目> -executeMethod Urpify.Cli.Commands.<命令> [参数]
```

**不要传入 `-quit`。** 每个命令都会先把退出码写入文件，然后自行以该退出码关闭编辑器：有些旧项目在工作完成后退出时会让
Unity 崩溃，因此 CI 应读取结果文件，而不要只依赖进程的退出码。

| 退出码 | 含义 |
|---|---|
| 0 | 完成。 |
| 1 | 失败：参数错误、文件缺失、步骤失败。原因写在 JSON 结果中。 |
| 2 | 完成，且发现了达到或超过 `-urpifyFailOn` 级别的问题。 |

每个命令在成功和出现任何错误时都会写入 JSON 结果（`command`、`step`、`status`、`exitCode`、`error`、`reports`）和文本
结果：退出码；对于 Migrate 步骤则是步骤及其状态，例如 `KitConverters Done`（`Done`、`Skipped`、`Failed`、`TimedOut`）。
文件如下：

| 命令 | JSON 结果（`Urpify/Reports/`） | 文本结果 |
|---|---|---|
| Scan | `urpify-scan-result.json` | `urpify-scan-result.txt` （都在 `-urpifyOutput`，默认 `Urpify/Reports`） |
| Migrate | `urpify-step-result.json` | `Urpify/urpify-step-result.txt` |
| Capture | | `Urpify/Captures/<label>/urpify-capture-result.txt` |
| Verify | `urpify-verify-result.json` | `Urpify/urpify-verify-result.txt` |
| Rollback | `urpify-rollback-result.json` | `Urpify/urpify-rollback-result.txt` |
| Package | `urpify-package-result.json` | `Urpify/urpify-package-result.txt` |
| BakeEnvironment | `urpify-bake-result.json` | `Urpify/urpify-bake-result.txt` |
| 参数错误的 Capture、Migrate 或 Package | `urpify-command-result.json` | `Urpify/urpify-command-result.txt` |

## 命令

### Scan

```
-executeMethod Urpify.Cli.Commands.Scan
    [-urpifyOutput <文件夹>]              默认 Urpify/Reports
    [-urpifyScope <路径>[;<路径>...]]     默认 Assets
    [-urpifyFormat json,html]             默认 两者
    [-urpifyFailOn pink|silent|degraded]
    [-urpifyNoCache]
```

只读。写入[报告结构](../report-schema.md)（英文）中描述的 JSON 报告，以及一份独立的 HTML 报告。
`urp-migration-report-latest.json` 总会写在带时间戳的文件旁边。扫描会把每个文件的结果缓存到 `Library/Urpify/cache`；
`-urpifyNoCache` 跳过缓存。

### Capture 与 Verify

```
-executeMethod Urpify.Cli.Commands.Capture -urpifyLabel <标签>
    [-urpifyScenes build|all|<a.unity>;<b.unity>]   默认：Project Settings > Urpify > 比较 > 场景
    [-urpifyWidth 960 -urpifyHeight 540 -urpifyFrames 30]
-executeMethod Urpify.Cli.Commands.Verify [-urpifyBefore before] [-urpifyAfter after] [-urpifyFailOn pink|changed]
```

Capture 在 Play Mode 中运行每个场景，并把主相机和每个 Migration Viewpoint 的画面保存到 `Urpify/Captures/<标签>/`。Verify
比较两个标签，并写入一份包含每个场景观测判定的报告。

### Migrate

```
-executeMethod Urpify.Cli.Commands.Migrate -urpifyStep <步骤>
    [-urpifyShaderOutput replace|write-copy|keep-builtin]       kit-converters；默认为项目设置
    [-urpifyGrabPass opaque-texture|after-transparents]          kit-converters；默认为项目设置
    [-urpifyScenes ...] [-urpifyWidth ...] [-urpifyHeight ...] [-urpifyFrames ...]   捕获步骤
    [-urpifyStepTimeout 20]                                      无进展多少分钟后停止步骤；0 = 从不
```

步骤按顺序进行，每个编辑器进程一个：`scan`、`backup`、`capture-before`、`install-urp`、`unity-converter`、
`kit-converters`、`capture-after`、`review`。Install URP 添加的包要由下一个进程编译，因此每个步骤都在自己的进程中运行。
Backup 在 Capture before 之前运行，没有备份时 Capture before 会拒绝运行：运行场景时 Unity 会重新保存旧资源，包也会创建
文件，而备份必须保留项目原来的样子。

会改动文件的步骤在 git 工作区有未提交改动时会在日志中警告。

### Rollback

```
-executeMethod Urpify.Cli.Commands.Rollback [-urpifyToStep backup]
```

不带 `-urpifyToStep` 时回滚所有步骤。迁移后被改动的文件，以及在步骤之间出现的文件，会保持原样并在日志中列出。

### Package

```
-executeMethod Urpify.Cli.Commands.Package -urpifyPackage <文件.unitypackage> [-urpifyOutput <文件夹>]
    [-urpifyExport <转换后.unitypackage>] [-urpifyShaderOutput replace|keep-builtin] [-urpifyKeepScope]
```

把包导入临时文件夹并扫描；带 `-urpifyExport`（Pro）时，按[转换 Asset Store 包](scenario-asset.md)中的说明写出转换后的包。
分析报告总会写出，包括导出失败或被拒绝时；此时退出码为 1。导出会针对 URP 编译转换后的着色器，因此项目中必须安装 URP。
`-urpifyKeepScope` 会保留临时文件夹以供查看。

### Bake environment

```
-executeMethod Urpify.Cli.Commands.BakeEnvironment -urpifyScene <Assets/a.unity> [-urpifyUndo]
```

为缺少天空盒环境光和反射的场景烘焙它们，并做备份；`-urpifyUndo` 回滚烘焙。参见发现“天空盒环境光未烘焙”。

## 中断的步骤与超时

中途停止的步骤（编辑器被结束或崩溃、步骤失败）会保留其改动的完整记录：下次启动编辑器时会将其作为“中断”收尾，其改动纳入
回滚。步骤运行期间，`Urpify/progress.json` 会说明它最后做了什么（`Step`、`What`、`Done`、`Total`、`UpdatedUtc`）。在批处理
模式下，连续 `-urpifyStepTimeout` 分钟没有进展的步骤会像编辑器被结束一样停止，原因写在 `progress.json` 和步骤消息中。进展
指的是步骤报告的内容和它写入的文件，而不是 Unity 日志。带窗口的编辑器永远不会被停止。

## 脚本无法编译的项目

Unity 会在任何命令开始之前中止批处理运行（“Scripts have compiler errors”）。请添加 `-ignorecompilererrors`：扫描会自行
报告编译错误（`PROJECT.SCRIPT_COMPILATION_FAILED`），改动脚本的步骤不会碰这些脚本。

## 示例：场景变成粉色时让构建失败

```
U="/path/to/Unity"; P=/path/to/project
for step in scan backup capture-before install-urp unity-converter kit-converters capture-after review; do
  "$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Migrate -urpifyStep $step -logFile -
  grep -q " Done\| Skipped" "$P/Urpify/urpify-step-result.txt" || { echo "step $step failed"; exit 1; }
done
"$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Verify -urpifyFailOn pink -logFile -
exit $(cat "$P/Urpify/urpify-verify-result.txt")
```

## 步骤写入的文件

- `Urpify/state.json`：迁移状态，位于 `Assets` 之外。
- `Urpify/Backup/<id>/`：每个改动文件的步骤一份备份，附带记录每个文件前后哈希的清单。
- `Urpify/Captures/<标签>/`：捕获的画面和 `captures.json`。
- `Urpify/Reports/`：扫描、比较和总结报告；`kit-converters.json` 是“Urpify 转换器”步骤自己的报告（每个文件的 `Path`、
  `Converter`、`Outcome`、`Notes`；URP Asset 的改动；转换后的文件所需的 URP 设置）；`self-check/` 是每个转换后的着色器在
  Built-in 和 URP 中的渲染。

如果想保留迁移历史，请把 `Urpify/` 加入版本控制；构建不会读取它。
