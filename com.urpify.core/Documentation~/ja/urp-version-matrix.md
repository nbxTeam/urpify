# Unity と URP のバージョン

| Unity | URP | 状態 | 確認環境 |
|---|---|---|---|
| 6000.6 | 17.6 | 対応 | 6000.6.5f1 と URP 17.6.0：ユニットテスト、37 のブース、デモ、実際のプロジェクト 4 つ |
| 6000.5 | 17.5 | 対応 | 6000.5.9f1 と URP 17.5.0：ユニットテスト、37 のブース、実際のプロジェクト、シェーダーコーパス |
| 6000.4 | 17.4 | 未テスト | |
| 6000.3 LTS | 17.3 | 対応 | 6000.3.26f1 と URP 17.3.0：ユニットテスト、37 のブース、デモ、実際のプロジェクト 4 つ。このバージョンでは Unity 自身の一括コンバーターが失敗しますが、Urpify が回避します（下記） |
| 6000.1、6000.2 | 17.1、17.2 | 未テスト | |
| 6000.0 LTS | 17.0 | 対応 | 6000.0.84f1 と URP 17.0.4：ユニットテスト、37 のブース、デモ、実際のプロジェクト 4 つ |
| 2022.3 LTS | 14 | 対応 | 2022.3.62f3 と URP 14.0.12：6000.5 と同じ一式 |
| 2021.3 LTS | 12 | 1.0 では非対応 | 要望があれば予定 |

出荷に使うエディターのバージョンで移行してください：移行前後のキャプチャは、同じ Unity バージョンの中でしか比較できま
せん。

## 6000.0 と 6000.3 での Unity のコンバーターと終了コード

- URP 17.0 と 17.3 では Unity のコンバーターが古い一括 API（`ConverterContainerId`）です。URP 17.3.0（6000.3.26f1）では、どのプロジェクトでも
  何も変換しないうちに抽象クラス `Base2DMaterialUpgrader` について `MissingMethodException` を投げます。その場合 Urpify が Built-in から URP への
  コンバーターを自分で列挙し、同じ方法で実行します。ステップの「詳細」にそのことが表示されます。
- Unity 6000.0.84f1 と 6000.3.26f1 はしばしば、6000.5.9f1 はときどき、作業が終わった後、一括モードの終了時にクラッシュします（終了コード 139）。
  Urpify の有無に関係ありません。コマンドラインでは `Urpify/urpify-<command>-result.txt` を読んでください（[コマンドライン](cli.md) を参照）。

## URP 14 と URP 17 の違い

Urpify のシェーダーライブラリ（`BirpCompat*.hlsl`）とそのレンダラー機能は以下のすべての違いを考慮しているので、変換した
シェーダーはどちらのバージョンでもコンパイル・描画できます。各行は URP 14.0.12（2022.3.62f3）と URP 17.5.0（6000.5.9f1）
のパッケージのソースで確認しています。

| 項目 | URP 14.0.12 | URP 17.5.0 | 確認箇所 |
|---|---|---|---|
| メインライト | `GetMainLight()`、`(shadowCoord)`、`(shadowCoord, positionWS, shadowMask)`、`(inputData, shadowMask, aoFactor)` | 同じ 4 つのオーバーロード | `ShaderLibrary/RealtimeLights.hlsl` |
| 追加ライト | `GetAdditionalLight(i, positionWS)`、`(i, positionWS, shadowMask)`、`(i, inputData, shadowMask, aoFactor)` | 同じ 3 つのオーバーロード | `ShaderLibrary/RealtimeLights.hlsl` |
| Forward+ のライトループ | `USE_FORWARD_PLUS` | `USE_CLUSTER_LIGHT_LOOP`。`USE_FORWARD_PLUS` は `ForwardPlusKeyword.deprecated.hlsl` で別名として残る | `ShaderLibrary/RealtimeLights.hlsl`、`Core.hlsl` |
| 追加ライトの影 | `AdditionalLightRealtimeShadow(lightIndex, positionWS, lightDirection)` | 同じものに加えて、影のパラメーターとサンプリングデータを取るオーバーロード | `ShaderLibrary/Shadows.hlsl` |
| 球面調和 | URP パッケージの `GlobalIllumination.hlsl` にある `SampleSH(normalWS)` | Core パッケージの `AmbientProbe.hlsl` にある `SampleSH(normalWS)` | URP と Core の `ShaderLibrary` |
| ライトマップ | `LIGHTMAP_ON`、`DIRLIGHTMAP_COMBINED` | 同じキーワード | `ShaderLibrary/GlobalIllumination.hlsl` |
| LOD クロスフェード | `LODCrossFade.hlsl` | `LODCrossFade.hlsl` | `ShaderLibrary/` |
| デカール | `DBuffer.hlsl` | `DBuffer.hlsl` | `ShaderLibrary/` |
| 深度・法線・不透明テクスチャ | `DeclareDepthTexture.hlsl`、`DeclareNormalsTexture.hlsl`、`DeclareOpaqueTexture.hlsl` | 同じファイル | `ShaderLibrary/` |
| レンダラー機能 | `ScriptableRenderPass.Execute`。Render Graph は内部 | Render Graph（`RecordRenderGraph`）。Unity 6.4 以降は互換モードがない：`RenderGraphSettings.enableRenderCompatibilityMode` は常に false を返す | `Runtime/Passes/ScriptableRenderPass.cs`、`Runtime/Settings/RenderGraphSettings.cs` |
| プレビューカメラの Opaque Texture | プレビューカメラではコピーされない（`copyColorPass &= !isPreviewCamera`） | コピーされる | `Runtime/UniversalRenderer.cs`。このため 2022.3 での Urpify の自己チェックは、GrabPass シェーダーをゲームカメラで描画します。 |
| URP Asset のテクスチャ | URP Asset の `m_Textures`（blue noise、Bayer）が、Unity のコンバーターが保存するとおり空のままだと、ビルドしたプレイヤーは毎フレーム `NullReferenceException` を出して黒く描画する | 同じプロジェクトの 6000.5 のプレイヤーでは見られない | `UniversalRenderPipeline.cs`（`SetupPerFrameShaderConstants`）。無料パッケージが Unity のコンバーターの後でこれを埋めます。 |

## Unity のコンバーターの違い

| 項目 | 2022.3 / URP 14 | 6000.5 / URP 17.5 |
|---|---|---|
| `Legacy Shaders/Particles/VertexLit Blended` と `Mobile/Bumped Specular (1 Directional Realtime Light)` のマテリアル変換ルール | 古いシェーダー名で登録されているため Unity のコンバーターはスキップする。Urpify が自身の表でこれらのマテリアルを変換する | Unity 自身が変換する |
| `-typesFilter` なしのバッチモード | 未測定：Urpify はこちらでも Unity のバッチコマンドを使わない | `NullReferenceException`（`Converters.cs:134`）を出す。Urpify は API からコンバーターを呼ぶ |
| 成功後の Unity のバッチコマンドの終了コード | 未測定 | 実際の 4 プロジェクトのうち 3 つが終了コード 21 または 139 で終了（終了時にプロセスがクラッシュ）。Urpify はどちらのバージョンでも、コンバーターのログと再スキャンで結果を判断する |
