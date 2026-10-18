# 構成要素

Urpify は個々のシェーダーではなく、構成要素に対応します：シェーダーが使うすべての構成要素に対応していれば変換され、
そうでなければスキャンが足りないものを示します。以下の各ページは Built-in Render Pipeline の構成要素を 1 つずつ説明
します：URP で何が壊れるか、Urpify がどう対処するか、結果がどこまで近いか。

画像は Urpify のテストラボのものです。ラボは構成要素ごとに専用のブースを持つシーンです。各画像は同じブースを 3 回
示します：Built-in、Unity の Render Pipeline Converter だけを通した URP、Unity のコンバーターと Urpify を通した URP。
Urpify 自身のキャプチャステップが撮ったフレームを、ブースに合わせて切り抜いたものです。

| 構成要素 | ページ | ブース | 直すのは |
|---|---|---|---|
| 組み込みライティングモデルのサーフェスシェーダー | [サーフェスシェーダー](surface-shaders.md) | S01–S04 | Pro |
| 独自のライティング関数 | [独自のライティング](surface-custom-lighting.md) | S05 | Pro |
| `#pragma surface` のオプション：`vertex:`、`finalcolor:`、`decal:`、`alphatest:`、`addshadow` など | [サーフェスのオプション](surface-options.md) | S03、S06–S08、S10、S11 | Pro |
| サーフェスシェーダーのテッセレーション | [テッセレーション](surface-tessellation.md) | S09 | Pro |
| 追加パス：アウトライン、毛のシェル、3 つ以上のパス | [追加パス](extra-passes.md) | S12 | Pro |
| ライティングなしの vert/frag シェーダー | [ライティングなしの vert/frag](vertfrag-unlit.md) | V01 | 対応不要 |
| ライティングありの vert/frag シェーダー：ForwardBase、ForwardAdd、影、頂点ライティング | [ライティングありの vert/frag](vertfrag-lit.md) | V02 | Pro |
| GrabPass | [GrabPass](grabpass.md) | G01、G02 | Pro |
| `_CameraDepthTexture` | [深度テクスチャ](depth-texture.md) | D01 | Free と Pro |
| `_CameraDepthNormalsTexture`、置換シェーダー | [depth-normals と置換シェーダー](depth-normals.md) | なし | Pro |
| `OnRenderImage` の画像エフェクト | [画像エフェクト](image-effects.md) | C02 | Pro |
| カメライベントの `CommandBuffer` | [コマンドバッファー](command-buffers.md) | C03 | Pro |
| カメラを描画するスクリプト：鏡、ポータル、水の反射 | [カメラスクリプト](camera-scripts.md) | M01 | Pro |
| `Projector` コンポーネント | [プロジェクター](projectors.md) | C01 | Pro |
| 組み込みシェーダー名を使う `Shader.Find` | [コード内の Shader.Find](shader-find.md) | C04 | Free |
| Unity のコンバーターが壊すもの：Standard Transparent、Self-Illumin、組み込みマテリアル、パッケージのマテリアル、Post-processing v2、URP Asset のテクスチャ、Material Variant | [Unity のコンバーター後の修正](after-unity-converter.md) | STD04、LEG03 | Free |
| Unity のコンバーターが扱うもの：Standard、Legacy、Mobile、パーティクル、テレイン、インポートしたモデル | [Unity のコンバーター](unity-converter.md) | STD01–STD06、LEG01–LEG05、PAR01、PAR02、FBX01、FBX02、TER01 | Unity |
| Tree Creator とソフトオクルージョンの木 | [木](trees.md) | なし | Pro |
| Built-in と URP の両方で使える 1 つのシェーダーファイル | [Built-in 対応を残す](keep-builtin-support.md) | シェーダーのすべてのブース | Pro |

Urpify が変換しない構成要素は、理由とともに [変換されないもの](../not-converted.md) にあります。
