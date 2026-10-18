# コマンドライン

ウィンドウでできることはすべてバッチモードでも動くので、ビルドサーバーでブランチをスキャン・変換し、シーンがピンクに
なったらビルドを失敗させられます。無料パッケージは Scan、Capture、Verify、Rollback、Migrate の全ステップ（その「Urpify の
コンバーター」ステップは無料の修正を実行します）、パッケージの分析を実行します。シェーダー、スクリプト、
コンポーネントの変換と、変換済みパッケージのエクスポートには Pro が必要です。

```
Unity -batchmode -projectPath <プロジェクト> -executeMethod Urpify.Cli.Commands.<コマンド> [引数]
```

**`-quit` は渡さないでください。** 各コマンドは終了コードをファイルに書いたうえで、そのコードでエディターを終了します。
古いプロジェクトの中には作業完了後の終了時に Unity がクラッシュするものがあるため、CI はプロセスの終了コードだけに頼らず、
結果ファイルを読んでください。

| 終了コード | 意味 |
|---|---|
| 0 | 完了。 |
| 1 | 失敗：引数の誤り、ファイルの欠落、ステップの失敗。理由は JSON の結果にあります。 |
| 2 | 完了し、`-urpifyFailOn` 以上の検出結果がありました。 |

どのコマンドも、成功時とあらゆるエラー時に JSON の結果（`command`、`step`、`status`、`exitCode`、`error`、`reports`）と
テキストの結果を書き込みます。テキストは終了コード、Migrate のステップではステップとその状態（例：`KitConverters Done`。
`Done`、`Skipped`、`Failed`、`TimedOut`）です。ファイル：

| コマンド | JSON の結果（`Urpify/Reports/`） | テキストの結果 |
|---|---|---|
| Scan | `urpify-scan-result.json` | `urpify-scan-result.txt` （どちらも `-urpifyOutput`、既定は `Urpify/Reports`） |
| Migrate | `urpify-step-result.json` | `Urpify/urpify-step-result.txt` |
| Capture | | `Urpify/Captures/<label>/urpify-capture-result.txt` |
| Verify | `urpify-verify-result.json` | `Urpify/urpify-verify-result.txt` |
| Rollback | `urpify-rollback-result.json` | `Urpify/urpify-rollback-result.txt` |
| Package | `urpify-package-result.json` | `Urpify/urpify-package-result.txt` |
| BakeEnvironment | `urpify-bake-result.json` | `Urpify/urpify-bake-result.txt` |
| 引数が誤っている Capture、Migrate、Package | `urpify-command-result.json` | `Urpify/urpify-command-result.txt` |

## コマンド

### Scan

```
-executeMethod Urpify.Cli.Commands.Scan
    [-urpifyOutput <フォルダー>]          既定 Urpify/Reports
    [-urpifyScope <パス>[;<パス>...]]     既定 Assets
    [-urpifyFormat json,html]             既定 両方
    [-urpifyFailOn pink|silent|degraded]
    [-urpifyNoCache]
```

読み取り専用です。[レポートスキーマ](../report-schema.md)（英語）で説明する JSON レポートと、単体で開ける HTML レポート
を書きます。`urp-migration-report-latest.json` はタイムスタンプ付きのファイルの隣に常に書かれます。スキャンはファイル
ごとの結果を `Library/Urpify/cache` にキャッシュします。`-urpifyNoCache` はキャッシュを使いません。

### Capture と Verify

```
-executeMethod Urpify.Cli.Commands.Capture -urpifyLabel <ラベル>
    [-urpifyScenes build|all|<a.unity>;<b.unity>]   既定：Project Settings > Urpify > 比較 > シーン
    [-urpifyWidth 960 -urpifyHeight 540 -urpifyFrames 30]
-executeMethod Urpify.Cli.Commands.Verify [-urpifyBefore before] [-urpifyAfter after] [-urpifyFailOn pink|changed]
```

Capture は各シーンを Play Mode で再生し、メインカメラとすべての Migration Viewpoint のフレームを
`Urpify/Captures/<ラベル>/` に保存します。Verify は 2 つのラベルを比較し、シーンごとの観測判定を含むレポートを書きます。

### Migrate

```
-executeMethod Urpify.Cli.Commands.Migrate -urpifyStep <ステップ>
    [-urpifyShaderOutput replace|write-copy|keep-builtin]       kit-converters；既定はプロジェクトの設定
    [-urpifyGrabPass opaque-texture|after-transparents]          kit-converters；既定はプロジェクトの設定
    [-urpifyScenes ...] [-urpifyWidth ...] [-urpifyHeight ...] [-urpifyFrames ...]   キャプチャのステップ
    [-urpifyStepTimeout 20]                                      進行がないままステップを止めるまでの分数。0 = 止めない
```

ステップはこの順で、エディターのプロセスごとに 1 つずつ実行します：`scan`、`backup`、`capture-before`、`install-urp`、
`unity-converter`、`kit-converters`、`capture-after`、`review`。Install URP は次のプロセスがコンパイルするパッケージを
追加するため、各ステップはそれぞれ別のプロセスで実行します。Backup は Capture before の前に実行し、Capture before は
バックアップなしでは実行しません：シーンを再生すると Unity が古いアセットを保存し直し、パッケージがファイルを作るため、
バックアップは元の状態のプロジェクトを保つ必要があります。

ファイルを変更するステップは、git の作業ツリーに未コミットの変更があるとログで警告します。

### Rollback

```
-executeMethod Urpify.Cli.Commands.Rollback [-urpifyToStep backup]
```

`-urpifyToStep` を付けないと、すべてのステップをロールバックします。移行後に変更されたファイルと、ステップの間に現れた
ファイルはそのまま残り、ログに一覧表示されます。

### Package

```
-executeMethod Urpify.Cli.Commands.Package -urpifyPackage <ファイル.unitypackage> [-urpifyOutput <フォルダー>]
    [-urpifyExport <変換済み.unitypackage>] [-urpifyShaderOutput replace|keep-builtin] [-urpifyKeepScope]
```

パッケージを一時フォルダーにインポートしてスキャンし、`-urpifyExport`（Pro）を付けると、
[Asset Store パッケージを変換する](scenario-asset.md) のとおりに変換済みパッケージを書き出します。分析レポートは、
エクスポートが失敗した場合や拒否された場合も含めて常に書かれます。その場合の終了コードは 1 です。エクスポートは変換した
シェーダーを URP 向けにコンパイルするため、プロジェクトに URP がインストールされている必要があります。
`-urpifyKeepScope` は確認用に一時フォルダーを残します。

### Bake environment

```
-executeMethod Urpify.Cli.Commands.BakeEnvironment -urpifyScene <Assets/a.unity> [-urpifyUndo]
```

スカイボックスの環境光と反射がないシーンに、バックアップを取ったうえでそれらをベイクします。`-urpifyUndo` でベイクを
ロールバックします。検出結果「スカイボックスの環境光がベイクされていない」を参照してください。

## 中断したステップとタイムアウト

途中で止まったステップ（エディターが終了またはクラッシュした、ステップが失敗した）は、変更内容の完全な記録を残します：
次にエディターを起動したときに中断として閉じられ、その変更はロールバックの対象になります。ステップの実行中、
`Urpify/progress.json` は最後に行ったこと（`Step`、`What`、`Done`、`Total`、`UpdatedUtc`）を示します。バッチモードでは、
`-urpifyStepTimeout` 分のあいだ進行しないステップは、エディターが終了した場合と同じように止められ、理由は
`progress.json` とステップのメッセージに記されます。進行とはステップが報告する内容と書き出すファイルのことで、Unity の
ログではありません。ウィンドウ付きのエディターが止められることはありません。

## スクリプトがコンパイルできないプロジェクト

Unity は、どのコマンドも始まる前にバッチ実行を中止します（「Scripts have compiler errors」）。`-ignorecompilererrors` を
追加してください：スキャンがコンパイルエラーを自分で報告し（`PROJECT.SCRIPT_COMPILATION_FAILED`）、スクリプトを変更する
ステップはそれらに触れません。

## 例：シーンがピンクになったらビルドを失敗させる

```
U="/path/to/Unity"; P=/path/to/project
for step in scan backup capture-before install-urp unity-converter kit-converters capture-after review; do
  "$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Migrate -urpifyStep $step -logFile -
  grep -q " Done\| Skipped" "$P/Urpify/urpify-step-result.txt" || { echo "step $step failed"; exit 1; }
done
"$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Verify -urpifyFailOn pink -logFile -
exit $(cat "$P/Urpify/urpify-verify-result.txt")
```

## ステップが書くファイル

- `Urpify/state.json`：移行の状態。`Assets` の外にあります。
- `Urpify/Backup/<id>/`：ファイルを変更するステップごとのバックアップ。各ファイルの変更前後のハッシュを記したマニフェスト
  付き。
- `Urpify/Captures/<ラベル>/`：キャプチャしたフレームと `captures.json`。
- `Urpify/Reports/`：スキャン、比較、まとめのレポート。`kit-converters.json` は「Urpify のコンバーター」ステップ自身の
  レポート（ファイルごとの `Path`、`Converter`、`Outcome`、`Notes`、URP Asset の変更、変換したファイルに必要な URP の
  設定）。`self-check/` は変換した各シェーダーの Built-in と URP での描画です。

移行の履歴を残したい場合は `Urpify/` をバージョン管理に追加してください。ビルドはこれを読みません。
