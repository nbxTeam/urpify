# ライティングありの vert/frag シェーダー

![ブース V02：ForwardBase、ForwardAdd、影を持つライティングありの vert/frag シェーダー](../../images/V02.jpg)

*ブース V02：ForwardBase と ForwardAdd のパス、`SHADOW_ATTENUATION`、影を落とすパス。*

## 何が壊れるか

URP は `ForwardBase`、`ForwardAdd`、`Vertex`、`VertexLM`、`PrepassBase` のタグを持つパスをエラーシェーダーで描画するため、
オブジェクトはピンクになります。Built-in のライト変数（`_LightColor0`、`_WorldSpaceLightPos0`）、影のマクロ
（`SHADOW_COORDS`、`TRANSFER_SHADOW`、`SHADOW_ATTENUATION`、`UNITY_LIGHT_ATTENUATION`）、
`UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"` には URP に対応するものがありません。

検出結果：「ForwardBase パス」「ForwardAdd パス」「Built-in の ShadowCaster パス」「LightMode ...」「UsePass ...」。

## Urpify の対処（Pro）

| Built-in | URP |
|---|---|
| `ForwardBase` | `UniversalForward`。ライティング、影、環境光は Built-in の名前を通じて URP から取得 |
| `ForwardAdd` | フォワードパス内での URP の追加ライトのループ：作者のフラグメントコードをライトごとに 1 回呼び、結果を合計 |
| `ShadowCaster` | `ApplyShadowBias` を使う URP の `ShadowCaster`。作者の頂点コードは保持。`DepthOnly` のコピーが深度プリパスを描画 |
| `UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"` | 生成した影のパス |
| プロジェクト内の別のシェーダーからの `UsePass` | パスをコピーし、残りと一緒に変換 |
| `Vertex`、`VertexLM`（頂点ライティングのみ） | `UniversalForward`。URP のメインライトと追加ライトで頂点単位にライティング |
| `_LightColor0`、`_WorldSpaceLightPos0` | パスを描画している対象のライト |
| `UNITY_LIGHT_ATTENUATION`、`SHADOW_ATTENUATION`、`LIGHT_ATTENUATION` | URP の影と距離による減衰 |
| `ShadeSH9`、`UNITY_FOG_*`、`UnityObjectToClipPos` などの `UnityCG.cginc` の内容 | Urpify のライブラリ `BirpCompat.hlsl` |

作者の `vert` と `frag` は変更せずにコピーされます。CG ブロックは HLSL ブロックになり、`fixed` は `half` になり、
`sampler2D`/`tex2D` はそのまま残ります：URP 14 と 17 でコンパイルできます。

## どこまで近いか

ForwardBase パスが 1 つだけなら `自動`、ForwardAdd があれば `自動（近似）`：Built-in は追加ライトを 1 つずつ別のパスで
ブレンドし、URP は 1 つのパスで合計します。Built-in が ForwardBase に渡す頂点ライト（`Shade4PointLights`）は、ループ内で
ピクセル単位に描画されます。
