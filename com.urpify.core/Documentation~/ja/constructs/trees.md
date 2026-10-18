# Tree Creator とソフトオクルージョンの木

ラボには木のブースがありません。ShaderTutorials（ronja-tutorials）と Nature Starter Kit の木で確認しました。

## 何が壊れるか

URP には Tree Creator のシェーダー（`Nature/Tree Creator Bark`、`Leaves`、`Leaves Fast`、`Optimized`）も、ソフト
オクルージョンの木のシェーダーもありません。これらで作った木はピンクになります。テレインの草とディテールメッシュは影響を
受けません：URP は独自のディテールシェーダーで描画します。

検出結果：「...、URP 版なし」（`MATERIAL.BUILTIN_TREE`）。

## Urpify の対処（Pro）

Unity は組み込みシェーダーのソースを MIT ライセンスで公開しています。Urpify Pro は Unity 自身の木のシェーダー 8 つを通常の
コンバーターで変換し、それらが必要とする `TerrainEngine.cginc` の関数（風、曲げ、ビルボード）をライブラリに加え、プレハブ
内のものも含めて幹と葉のマテリアルを変換後のシェーダーに移します。ライセンス文は Pro の `Third Party Notices.md` に
あります。

## どこまで近いか

`自動（近似）`。木はピンクでなくなり、マテリアルも保たれます。URP での風のアニメーションとビルボードは、数秒分のフレームで
目視確認しただけで、ピクセル単位の測定はしていません。自分のシーンはシーン比較で比べてください。SpeedTree のアセットは
Urpify ではなく Unity のコンバーターが更新します。
