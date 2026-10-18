# Urpify

Urpify は、Unity プロジェクト、シェーダー、Asset Store パッケージを Built-in Render Pipeline から URP に移行します。
何かを変更する前に、切り替えで壊れるものをすべて表示します。エラーを出さずに壊れるものも含みます。そのうえで変換できる
ものを変換し、触れるファイルはすべてバックアップし、各シーンを移行前と移行後のキャプチャで比較します。どのステップも
ロールバックできます。

Urpify は 2 つのパッケージで提供されます。

| | Urpify（無料、MIT） | Urpify Pro |
|---|---|---|
| プロジェクト、フォルダー、ファイル、`.unitypackage` のスキャン | あり | あり |
| キャプチャしたフレームによる移行前後のシーン比較 | あり | あり |
| Unity の Render Pipeline Converter の確実な実行（ウィンドウまたはコマンドラインから） | あり | あり |
| Unity のコンバーター後の修正（ガラス、Self-Illumin、組み込みマテリアル、Post-processing v2 のブルーム、URP Asset の設定、パッケージのマテリアル） | あり | あり |
| バックアップとロールバック | あり | あり |
| シェーダーパネル：コード、Built-in と URP での現状のプレビュー | あり | あり |
| 独自シェーダー、画像エフェクト、カメラスクリプト、コマンドバッファー、プロジェクターの変換 | なし | あり |
| 出力：両方のパイプラインで動く 1 つのシェーダーファイル（Built-in 対応を残す） | なし | あり |
| 変換済み `.unitypackage` のエクスポート | レポートのみ | あり |
| コマンドライン | スキャン、キャプチャ、比較、Unity のコンバーター、無料の修正 | すべて |

## 目次

まずはここから：
- [クイックスタート](quick-start.md)：デモシーンまたは自分のプロジェクトで、全体の流れを 5 分で。
- [ゲームを移行する](scenario-game.md)
- [シェーダーを変換する](scenario-shader.md)
- [Asset Store パッケージを両方のパイプライン向けに変換する](scenario-asset.md)

Urpify が Built-in Render Pipeline の各構成要素をどう扱うか：
- [構成要素の概要](constructs/index.md)：構成要素ごとに 1 ページ、移行前後の画像付き。
- [変換されないものとその理由](not-converted.md)
- [URP で見た目が変わる点](urp-differences.md)

リファレンス：
- [Urpify ウィンドウ](window.md)：アイコン、カウント、判定。
- [設定](settings.md)
- [コマンドライン](cli.md)
- [レポートスキーマ](../report-schema.md)（英語）
- [Unity と URP のバージョン](urp-version-matrix.md)
- [よくある質問](faq.md)
- [変換したシェーダーの再配布](../license-redistribution.md)（英語）

## 要件

- Unity 2022.3 LTS（URP 14）、または Unity 6000.0 LTS、6000.3 LTS、6000.5、6000.6（URP 17）。テスト済みのバージョンです。[Unity と URP のバージョン](urp-version-matrix.md) を参照して
  ください。
- Built-in Render Pipeline のプロジェクト、またはそれ向けに作られた `.unitypackage`。移行に必要なときは Urpify が URP を
  インストールします。
- ほかのパッケージは不要です。Urpify がネットワークに送るのは、`レポートを送信` で送ると決めたシェーダーのアーカイブだけ
  です（[よくある質問](faq.md) を参照）。

## 言語

ウィンドウは English、简体中文、日本語、한국어、Русский、Español、Português (Brasil)、Deutsch に対応しています。既定では
エディターの言語（Preferences > Languages）に従い、Urpify にその言語がない場合は英語を使います。変更するには Project
Settings > Urpify > 個人用 > 言語 を開きます。レポート内のコンバーターの注記、コマンドライン、HTML レポートの技術的な詳細
は英語のままです。
