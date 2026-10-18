# Unity のコンバーターが扱うもの

Unity の Render Pipeline Converter は、Unity 自身の組み込みシェーダーを使うマテリアルを更新します。Urpify はこれを移行の
「Unity のコンバーターを実行」ステップとして、スクリプトから確実に動かすための回避策付きで実行し、そのログを報告します。

![ブース STD01：ノーマルマップとメタリックマップを使う Standard メタリック](../../images/STD01.jpg)

*STD01：Standard、メタリックワークフロー、ノーマルマップとメタリックマップ。Unity のコンバーターが URP Lit に更新します。*

![ブース TER01：2 レイヤーのテレイン](../../images/TER01.jpg)

*TER01：2 レイヤーのテレインが URP のテレインシェーダーに移ります。*

ブース STD01〜STD06、LEG01〜LEG05、PAR01、PAR02、FBX01、FBX02、TER01 で測定した、コンバーターが扱う範囲：

- Opaque、Cutout、Fade モードの Standard と Standard (Specular setup)。ノーマルマップ、エミッション、ディテールマップ
  付き。Transparent モードは例外です。[Unity のコンバーター後の修正](after-unity-converter.md) を参照してください。
- Legacy Shaders（Diffuse、Specular、Bumped、Reflective、Transparent、Cutout、Lightmapped、Parallax、VertexLit）。
  Self-Illumin は例外です。
- Mobile シェーダー、Unlit/Texture、Particles/Standard Surface と Unlit、レガシーのパーティクル。
- Nature/Terrain/Standard と既定のテレインマテリアル、SpeedTree。
- インポートしたモデルに埋め込まれたマテリアル。
- Built-in のマテリアルプロパティをアニメーションするアニメーションクリップ（スキャンは一覧にします。Urpify はまだ結果を
  確認しません）。
- Post-processing Stack v2。[Unity のコンバーター後の修正](after-unity-converter.md) を参照してください。

Urpify のスキャンはこれらのマテリアルを Unity のコンバーターが直すものとして一覧にするため、概要では Urpify が必要なもの
とは別に数えます。

## 確実に実行する

Unity 6.5 では、Unity のドキュメントにあるバッチコマンドは `-typesFilter` がないと `NullReferenceException` を出し、
成功した後の終了時にエディターがよくクラッシュします（終了コード 21、139）。Urpify はコンバーターを API から呼び、終了
コードではなくコンバーターのログと再スキャンで結果を判断し、ログをレポートに含めます。
