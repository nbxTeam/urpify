# 深度テクスチャ

![ブース D01：交差部分に泡のある水。Unity のコンバーター後は白く、Urpify の後は Built-in と同じ](../../images/D01.jpg)

*ブース D01：深度の色と岩の周りの泡のために `_CameraDepthTexture` を読む透明な水。Unity のコンバーターだけを通すと、水は
白くなります。*

## 何が壊れるか

`_CameraDepthTexture` を読むシェーダーが深度を受け取れるのは、URP Asset で Depth Texture がオンのときだけです。Unity の
コンバーターは、Soft Particles を使っていた品質レベルでしかこれをオンにしません。透明シェーダーには 2 つ目の問題があります：
URP は既定で透明オブジェクトの後に深度をコピーし、Unity のコンバーターはそれを変えません。Unity 6 ではそのシェーダーは
深度をまったく受け取れず、水が白くなります。2022.3 では前のフレームの深度を読むため、静止したフレームは正しく見えても、
動くオブジェクトは跡を残します。

検出結果：「_CameraDepthTexture を読む」（`SHADER.DEPTH_TEXTURE`）、「透明、... を読む」
（`SHADER.DEPTH_TEXTURE_TRANSPARENT`）。

## Urpify の対処

- **Free と Pro：** すべての URP Asset で Depth Texture をオンにし、透明シェーダーが読む場合はレンダラーの Depth Texture
  Mode を After Opaques にします。URP がそのまま描画するシェーダーは、コードを変えずに動くようになります：ブース D01 は
  無料パッケージだけで直ります。
- **Pro（シェーダーを変換する場合）：** 作者の `sampler2D _CameraDepthTexture` の宣言を URP の `DeclareDepthTexture.hlsl`
  に置き換え、読み取り（`tex2D`、`tex2Dproj`、`SAMPLE_DEPTH_TEXTURE`、`SAMPLE_DEPTH_TEXTURE_PROJ`）を
  `SampleSceneDepth` にします。これにより、深度テクスチャが配列になるシングルパスインスタンシングの XR でも動きます。
  `LinearEyeDepth`、`Linear01Depth`、`COMPUTE_EYEDEPTH` は Urpify のライブラリから提供されます。

## どこまで近いか

`自動`。読み取りを書き換えられない場合は作者の宣言が残り、検出結果はそのシェーダーでシングルパスインスタンシングの XR に
対応しないことを伝えます。
