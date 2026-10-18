# カメラを描画するスクリプト：鏡、ポータル、水の反射

![ブース M01：平面鏡。Unity のコンバーター後は黒く、Urpify の後は再び映る](../../images/M01.jpg)

*ブース M01：MirrorReflection や Standard Assets Water と同じ書き方の平面鏡：`OnWillRenderObject`、`Camera.current`、
斜めのニアクリップ面を持つ反射カメラ、`Camera.Render`。Unity のコンバーターだけを通すと、鏡は黒くなります。*

## 何が壊れるか

URP はカメラの `OnPreCull`、`OnPreRender`、`OnPostRender` を一度も呼ばず、`OnWillRenderObject` は別のカメラを描画できない
カリング中に呼びます。これらのメソッドで 2 台目のカメラを描画するポータルや鏡は、平らな面か黒い面になります。エラーは
出ません。URP 14 と 17 で確認済み。

検出結果：「カメラの OnPreCull」など（`SCRIPT.CAMERA_CALLBACKS`）、「OnWillRenderObject がカメラを描画する」
（`SCRIPT.WILLRENDER_CAMERA`）。

## Urpify の対処（Pro）

スクリプトはメソッドとロジックを保ちます。Urpify はそれを URP のカメライベントに登録します。

- `OnPreCull` と `OnPreRender` は、そのカメラの `RenderPipelineManager.beginCameraRendering` から、`OnPostRender` は
  `endCameraRendering` から呼ばれます。
- `OnWillRenderObject` は、URP のカメラのカリングの前に、そのカメラから見えるレンダラーについて呼ばれ、`Camera.current`
  はそのカメラを指します。
- その中の `Camera.Render()` はレンダーリクエストで URP のフレームに描画するため、反射やポータルのテクスチャは、それを表示
  するカメラが描画する前に用意されます。
- `OnRenderObject` は、カメラの不透明・透明オブジェクトの後に呼ばれます。

Urpify のカタログは既知のスクリプトを認識します：SebLague の Portals、MirrorReflection、Standard Assets Water、
ToonWaterShader の法線カメラは、これらのプロジェクトで確認したレシピで変換されます。それ以外は同じ仕組みを使い、
「自動（近似）」になります。

## どこまで近いか

カタログで確認済みのレシピは `自動`、それ以外は `自動（近似）`：それらのシーンはシーン比較で確認してください。Built-in との
違いが 1 つあります：`OnWillRenderObject` はオクルージョンカリングなしでカメラの視錐台内のレンダラーについて呼ばれるため、
壁の向こうに隠れた鏡も反射を描画します。

`Camera.onPreCull`、`Camera.onPreRender`、`Camera.onPostRender` のデリゲートに登録したコードはまだ変換されません。移し方は
検出結果に書かれています。カメラを描画する `OnRenderImage` は URP では実行されません。
[変換されないもの](../not-converted.md) を参照してください。
