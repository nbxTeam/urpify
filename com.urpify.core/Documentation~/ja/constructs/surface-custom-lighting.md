# 独自のライティング関数

![ブース S05：トゥーンランプのライティング関数。Unity のコンバーター後はピンク、Urpify の後は同じ](../../images/S05.jpg)

*ブース S05：独自のライティング関数（トゥーンランプ）を持つサーフェスシェーダー。*

## 何が壊れるか

サーフェスシェーダーは独自のライティングモデルを `#pragma surface surf Toon` と名付け、`LightingToon` を定義できます。
Built-in はこれをライトごとに 1 回呼びます：メインライトは ForwardBase で、各追加ライトは ForwardAdd で、`_LightColor0` と
ライトの減衰を使います。URP にはサーフェスシェーダーがないので、オブジェクトはピンクになります。

検出結果：「独自のライティング関数 ...」（`SHADER.SURFACE.CUSTOM_LIGHTING`）。

## Urpify の対処（Pro）

作者の関数は変更せずにコピーされます。生成されたフォワードパスは、URP のメインライト、続いて各追加ライトについて、URP の
影と減衰を使ってこの関数を呼び、結果を合計します。これは Built-in の 2 つのパスと同じです。関数の中では、`_LightColor0` は
呼び出し対象のライトの色です。

Built-in のすべてのシグネチャを認識します。

| シグネチャ | 例 |
|---|---|
| 視線方向なし | `half4 LightingToon(SurfaceOutput s, half3 lightDir, half atten)` |
| 視線方向あり | `half4 LightingToon(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)` |
| グローバルイルミネーションあり | `half4 LightingToon(SurfaceOutput s, half3 viewDir, UnityGI gi)` とその `LightingToon_GI` |
| ディファードとプリパスの版 | `_Deferred`、`_PrePass`：使いません。URP はフォワードで描画します |

関数は、コンパイラーが見つける場所であればどこでも見つかります：プログラム内、`CGINCLUDE` ブロック、任意の深さのプロジェクト
の include ファイル、関数を宣言する `#define` マクロ。

## どこまで近いか

`自動（近似）`。Built-in は追加ライトを 1 つずつ加算ブレンドの別パスで足し、URP は 1 つのパスでライティングします。不透明
オブジェクトでは合計は一致し、透明シェーダーではライトが重なる部分で違いが出ることがあります。環境光は Built-in と同じく
URP の球面調和から得ます。

関数がシェーダーにもスキャンした include ファイルにもない場合、検出結果がその名前を示し、その include ファイルがスキャン
範囲に入るまでシェーダーは変換されません。
