# GrabPass

![ブース G01：GrabPass で屈折するガラス。Unity のコンバーター後は消え、Urpify の後は再び屈折する](../../images/G01.jpg)

*ブース G01：名前なしの `GrabPass {}` で屈折するガラス。Unity のコンバーターだけを通すと、ガラスはエラーなしで消えます。*

![ブース G02：名前付き GrabPass を使う陽炎](../../images/G02.jpg)

*ブース G02：名前付きの `GrabPass { "_HazeBackground" }` を使う熱の陽炎。*

## 何が壊れるか

URP には GrabPass がありません。画面を取り込むパスは飛ばされ、テクスチャは空のままで、オブジェクトは消えるか屈折を失い
ます。ピンクにもエラーにもならない、サイレントな破損です。

検出結果：「GrabPass（...）」（`SHADER.GRABPASS`）、深刻度「エラーなしで壊れる」。

## Urpify の対処（Pro）

GrabPass を取り除き、それが埋めていたテクスチャ（`_GrabTexture` または名前付きのもの）が代わりに画面のコピーを読むように
します。

- **GrabPass のソース = Opaque Texture**（既定）：URP の `_CameraOpaqueTexture`。不透明オブジェクトとスカイの後、透明
  オブジェクトの前に取られます。Urpify はすべての URP Asset で Opaque Texture を縮小なしでオンにします。
- **GrabPass のソース = 透明オブジェクトの後**：Urpify の Built-in Camera Hooks レンダラー機能が、透明オブジェクトの後に
  画面を `_UrpifyGrabTexture` にコピーし、その後で GrabPass を持つオブジェクト（タグ `UrpifyGrabPass`）を描画します。
  Built-in の GrabPass と同じです。1 フレームあたり画面コピーが 1 回増えます。

同じシェーダーの 1 つまたは 2 つのパスの後にある GrabPass（フォグの後に、取り込みを読むぼかしがあるなど）は、それらのパスの
後に取ったコピーを読むため、Built-in の取り込みと同じものを見ます。

## どこまで近いか

Opaque Texture では `自動（近似）`：ガラスの向こうの透明オブジェクトは屈折に映りません。「透明オブジェクトの後」では
結果は Built-in と一致します。1 つのシェーダー内でテクスチャの異なる複数の GrabPass はまだ対応していません：URP の
Opaque Texture は 1 つだけです。

2022.3 では、Urpify の自己チェックは GrabPass シェーダーをゲームカメラで描画します：URP 14 はプレビューカメラに Opaque
Texture をコピーしないためです。
