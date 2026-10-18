# サーフェスシェーダーのオプション

`#pragma surface surf Standard <オプション>` は Unity が生成するものを変えます。Urpify は各オプションを読み、書き出すパスで
その意味を保ちます（Pro）。

![ブース S03：vertex: の風関数を持つ Lambert](../../images/S03.jpg)

*S03：`vertex:vert` が風のようにメッシュを曲げます。頂点関数はすべてのパスで実行されるので、影も曲がります。*

![ブース S08：clip() と addshadow によるディゾルブ](../../images/S08.jpg)

*S08：`clip()` と `addshadow` によるディゾルブ：影も同じように切り抜かれます。*

![ブース S10：decal:add](../../images/S10.jpg)

*S10：`decal:add`、不透明オブジェクトの上に重ねる加算レイヤー。*

![ブース S11：finalcolor: による高さフォグ](../../images/S11.jpg)

*S11：`finalcolor:` による高さフォグ。Built-in と同じくライティングの後に適用されます。*

## オプションとその結果

| オプション | URP での扱い |
|---|---|
| `vertex:func` | 生成するすべてのパス（フォワード、影、深度、depth-normals、meta）で実行されます。関数が埋める `Input` の追加フィールドはサーフェス関数に渡ります。 |
| `finalcolor:func` | ライティング後の色を使い、フォグの前に呼ばれます。 |
| `finalgbuffer:func` | 使いません：Built-in のディファード G バッファーパスでのみ実行されます。その旨の注記が付きます。 |
| `alpha`、`alpha:blend`、`alpha:fade`、`alpha:premul` | 透明キューと対応するブレンド。`premul` は乗算済みスペキュラーを保ちます。 |
| `alphatest:prop` | すべてのパスでプロパティに対する `clip()`。 |
| `keepalpha` | サーフェス関数のアルファを不透明パスでも書き込みます。 |
| `decal:add`、`decal:blend` | 不透明オブジェクトの後に、加算またはアルファブレンドで、影なしで描画します。 |
| `addshadow` | 影のパスがフォワードパスと同じく頂点関数とサーフェスの切り抜きを実行します。 |
| `noshadow` | 影のパスなし。フォワードパスは影を受けません。 |
| `noambient`、`novertexlights`、`nolightmap`、`nofog`、`nometa`、`noforwardadd` | 対応する部分（環境光、頂点ライト、ライトマップ、フォグ、Meta パス、追加ライト）を外します。 |
| `halfasview`、`interpolateview`、`approxview`、`dualforward` | URP に不要な視線方向の品質スイッチ。注記付きで無視します。 |
| `fullforwardshadows`、`nodynlightmap`、`nodirlightmap`、`nolppv`、`noshadowmask`、`noinstancing`、`exclude_path:*`、`dithercrossfade` | 認識します。これらは URP 自身の設定（追加ライトの影、ライトマップのモード、ライトプローブプロキシボリューム、LOD クロスフェード）が決めるため、変換後のシェーダーでは何も変えません。 |
| `tessellate:func`、`tessphong:` | [テッセレーション](surface-tessellation.md) を参照。 |

Urpify が知らないオプションは検出結果「未対応のサーフェスオプション」（`SHADER.SURFACE.UNKNOWN_OPTION`）に一覧され、
シェーダーの残りはそれでも変換されます。

設定の「影」（Project Settings > Urpify）で、変換したすべてのサーフェスシェーダーの影を強制的にオンまたはオフにできます。
