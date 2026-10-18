# サーフェスシェーダー

![ブース S01：Standard のサーフェスシェーダー。Unity のコンバーター後はピンク、Urpify の後は Built-in と同じ](../../images/S01.jpg)

*ブース S01：Standard ライティングモデル、アルベド、ノーマルマップ、オクルージョンを使うサーフェスシェーダー。*

## 何が壊れるか

`#pragma surface` は組み込みパイプラインの機能です：Unity はサーフェス関数からシェーダーのパス（ForwardBase、ForwardAdd、
ShadowCaster、Deferred、Meta）を生成します。URP にはサーフェスシェーダーがなく、これらのパスはエラーシェーダーで描画される
ため、そのシェーダーを使うオブジェクトはすべてピンクになります。Unity のコンバーターは独自シェーダーを扱いません。

検出結果：「サーフェスシェーダー、ライティング ...」（`SHADER.SURFACE`）、深刻度「ピンクになる」。

## Urpify の対処（Pro）

コンバーターは、作者のサーフェス関数をそのままコピーし、その周りに URP が必要とするパスを書きます。

| パス | 元にするもの |
|---|---|
| `UniversalForward` | サーフェス関数、続いてライティングモデルに応じた URP のライティング |
| `ShadowCaster` | 同じ頂点コードに URP のシャドウバイアス |
| `DepthOnly`、`DepthNormals` | 同じ頂点コード。深度テクスチャ、深度プリパス、SSAO 用 |
| `Meta` | サーフェス関数のアルベドとエミッション。ライトマップのベイク用 |

| ライティングモデル | URP でのライティング |
|---|---|
| `Standard`、`StandardSpecular` | `UniversalFragmentPBR`、メタリックまたはスペキュラーのワークフロー |
| `Lambert`、`BlinnPhong` | Urpify のライブラリにある Built-in の `LightingLambert` と `LightingBlinnPhong`。メインライトと各追加ライトに適用 |
| 独自の `Lighting<Name>` | その関数。[独自のライティング](surface-custom-lighting.md) を参照 |

`Input` 構造体は Built-in と同じように埋められます：テクスチャのタイリング付きの `uv_` と `uv2_` 座標、`worldPos`、
`worldNormal`、`worldRefl`、`viewDir`、`screenPos`、`color : COLOR`、`facing : VFACE`、`WorldNormalVector` を伴う
`INTERNAL_DATA`、そして `vertex:` 関数が埋めるフィールド。マテリアルプロパティは SRP Batcher のために `UnityPerMaterial`
に移り、シェーダー名とプロパティ名はそのままなので、マテリアルの値は保たれます。

複数の `SubShader` ブロックと LOD は意味を保ちます。組み込みシェーダーへの `Fallback` は、対応する URP のシェーダーを指す
ようになります。

## どこまで近いか

Standard、StandardSpecular、Lambert、BlinnPhong は `自動`：URP 自身のライティングの違い
（[URP で見た目が変わる点](../urp-differences.md)）を除き、結果は Built-in と一致します。変換したシェーダーはすべて、球を
使って Built-in と URP でも描画され、球の 25 % を超える部分が変わった場合は `要確認` になります。

![ブース S02：StandardSpecular](../../images/S02.jpg)

*S02：StandardSpecular。*

![ブース S04：alpha:fade の BlinnPhong](../../images/S04.jpg)

*S04：`alpha:fade` の BlinnPhong。透明な球。*
