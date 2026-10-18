# Unity のコンバーター後の修正

Unity の Render Pipeline Converter は移行で最初に実行されるもので、たいていは正しく動きます。ここでは、その結果が測定上
誤っているケースと、その後に無料パッケージが行うことを説明します。すべて無料パッケージの「Urpify のコンバーター」ステップ
に含まれます。

## Transparent モードの Standard

![ブース STD04：Standard Transparent モードのガラス。Unity のコンバーター後は白く、Urpify の後は再びガラスになる](../../images/STD04.jpg)

*STD04：Standard の Transparent モードのガラス。*

Unity のコンバーターは Transparent を URP の Premultiply ブレンドに対応付けますが、これはアルファを掛けた色を前提にします。
ガラスはほぼ不透明で白くなります。Urpify はマテリアルを Preserve Specular Lighting 付きの Alpha ブレンドに切り替えます。
これが Built-in のブレンド方法です。検出結果：「...、Transparent モード」（`MATERIAL.STANDARD_TRANSPARENT`）。

## Legacy Self-Illumin

![ブース LEG03：Legacy Self-Illumin。Unity のコンバーター後は暗く、Urpify の後は光る](../../images/LEG03.jpg)

*LEG03：`Legacy Shaders/Self-Illumin/Diffuse`。*

Unity のコンバーターはこれらのマテリアルを Simple Lit に移しますが、Simple Lit は Illumin のアルファからエミッションを
取らないため、光が消えます。Urpify は Built-in と同じように光る、URP 用の独自の Self-Illumin シェーダーに移します。
検出結果：`MATERIAL.SELF_ILLUMIN`。

## エディターの組み込みマテリアル

エディターの組み込みの `Default-Material` や `Default-Diffuse` を使うレンダラー（新しい立方体、平面）を、Unity の
コンバーターは URP の既定のマテリアルに向けますが、それは白ではなく中間の灰色です。Urpify は組み込みマテリアルの値を持つ
URP のコピーに向けます。検出結果：`SCENE.BUILTIN_MATERIAL`。

## パッケージ内のマテリアル

パッケージのファイルは読み取り専用なので、Unity のコンバーターはパッケージ内のマテリアルを変更しません。そのシェーダーが
Built-in 専用だと、それを使うレンダラーはすべてピンクになります：ProBuilder 5 の壁は `ProBuilderDefault.mat` を使い、
そのシェーダー `ProBuilder/Standard Vertex Color` はサーフェスシェーダーです。Urpify は、シーンとプレハブ内のこうした
レンダラーを `Assets/Urpify/Materials/Packages/` の URP 用コピーに向けます：組み込みシェーダーなら Unity 自身の
アップグレーダーで（`自動`）、それ以外はマテリアルの色、テクスチャ、スムースネス、メタリックを持つ URP Lit で
（`自動（近似）`：頂点カラーなどシェーダー自身が加えるものは保たれません）。検出結果：`SCENE.PACKAGE_MATERIAL`。

## アップグレーダーのない組み込みシェーダー

一部の組み込みシェーダーには、Unity のコンバーターにアップグレーダーがありません（「No upgrader found」）：
`Legacy Shaders/Decal`、`Legacy Shaders/Transparent/VertexLit`、`Transparent/Parallax Diffuse` と `Specular`、さらに
URP 14 では古い名前で登録された 2 つのアップグレーダー。Urpify はこれらのマテリアルを、プロパティを引き継いだうえで、
自身の表で最も近い URP のシェーダーに移します。検出結果：`MATERIAL.BUILTIN_NO_UPGRADER`。

## Post-processing Stack v2

Unity のコンバーターは PPv2 のボリュームとプロファイルを URP の Volume に変換しますが、バッチモードでは必要なアセット検索を
待たないため、どれも変換されません：ブルームはエラーなしで消えます。Urpify の「Unity のコンバーターを実行」ステップは
検索を待ち、そのうえでブルームのパラメーターを、光が Built-in に近くなるよう調整（threshold と scatter）して引き継ぎます。
残りの違いは URP のブルームそのものです。[URP で見た目が変わる点](../urp-differences.md) を参照してください。検出結果：
`SCENE.PPV2`。

## URP Asset の設定

- **Depth Texture と Opaque Texture**：URP がそのまま描画するシェーダーがこれらを読む場合、すべての URP Asset でオンに
  します（[深度テクスチャ](depth-texture.md) を参照）。
- **URP Asset のテクスチャ**（URP 14）：Unity のコンバーターは URP Asset を blue noise と Bayer のテクスチャが空のまま
  保存します。エディターはこれを埋めますが、ビルドしたプレイヤーは埋めません：毎フレーム `NullReferenceException` を
  出して黒く描画します。Urpify は URP パッケージからこれを埋めます。検出結果：`SETTINGS.URP_ASSET_TEXTURES`。
- **メインライト**：Pixel Light Count が 0 の品質レベルでも、Built-in はメインのディレクショナルライトをピクセル単位で
  照らしていました。Urpify はそこで URP の Main Light を Per Pixel にします。

## Material Variant

Unity のコンバーターは Material Variant を親の URP シェーダーに移しますが、バリアント独自の値を捨てます：バリアントは親の
黙ったコピーになります。Urpify は「Unity のコンバーターを実行」ステップのバックアップからバリアント独自の値を読み、URP の
プロパティのオーバーライドとして戻します。検出結果：`MATERIAL.VARIANT`。

## モデル内の読み取り専用マテリアル

FBX ファイルに埋め込まれたマテリアルは読み取り専用で、それを扱う Unity のコンバーターのモジュールはバッチモードで確実に
動きませんでした。Urpify は、見つけたレンダラーとテレインについて、そのモジュールをオブジェクトごとに実行します。Unity の
両バージョンで行います。
