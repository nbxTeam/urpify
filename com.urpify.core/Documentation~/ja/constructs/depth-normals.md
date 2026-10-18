# depth-normals テクスチャと置換シェーダー

これらの構成要素にはラボのブースがありません。両方に依存する水の泡を持つ ToonWaterShader（IronWarrior、Unlicense）で確認
しました。

## 何が壊れるか

- URP には `_CameraDepthNormalsTexture` がありません。これを読むシェーダーは何も受け取れず、法線によるアウトラインや岸辺の
  泡が消えます。
- `Camera.SetReplacementShader` と `Camera.RenderWithShader` は URP では使えません。置換シェーダーでシーンをテクスチャに
  描画するスクリプト（エフェクト用の法線や深度を得るよくある方法）は、何の知らせもなく役に立つものを描画しなくなります。
  ToonWaterShader ではこれで泡が失われました。

検出結果：「_CameraDepthNormalsTexture を読む」（`SHADER.DEPTHNORMALS_TEXTURE`）、置換シェーダーの呼び出し
（`SCRIPT.REPLACEMENT_SHADER`）。

## Urpify の対処（Pro）

- Urpify の Built-in Camera Hooks レンダラー機能が、URP の深度テクスチャと法線テクスチャから Built-in のエンコード
  （`EncodeDepthNormal`）で `_CameraDepthNormalsTexture` を書き出すので、シェーダーはこれまでどおり読めます。この機能は、
  シェーダーが必要とするときだけ URP に法線テクスチャを要求します。
- 置換シェーダーを使うカメラは、同じレンダラー機能のパスになります。このパスはカメラの不透明オブジェクトを置換シェーダーで
  同じ出力先に描画し、タグの対応（`RenderType`）も同じです。

## どこまで近いか

`自動（近似）`。URP の法線は URP 自身の depth-normals パスから得るため、エッジが Built-in と少し異なることがあります。
