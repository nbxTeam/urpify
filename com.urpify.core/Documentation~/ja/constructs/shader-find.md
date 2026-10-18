# 組み込みシェーダー名を使う Shader.Find

![ブース C04：Shader.Find("Standard") でコードから作るマテリアル。Unity のコンバーター後はピンク、Urpify が修正](../../images/C04.jpg)

*ブース C04：スクリプトが実行時に `new Material(Shader.Find("Standard"))` でマテリアルを作り、`_Color` を設定します。*

## 何が壊れるか

Unity のコンバーターが更新するのはマテリアルファイルで、コードではありません。`Shader.Find("Standard")`、`"Diffuse"`、
`"Legacy Shaders/..."` などの組み込みシェーダー名でマテリアルを作るスクリプトは、URP が描画できないシェーダーを受け取り、
オブジェクトはピンクになります。スクリプトが設定するプロパティ名（`_MainTex`、`_Color`、`_Glossiness`）は URP Lit では
隠しプロパティなので、手作業でシェーダーを置き換えても、これらの呼び出しは何も変えません。

検出結果：`Shader.Find("...")`（`SCRIPT.SHADER_FIND_BUILTIN`）、「コード内の Built-in のプロパティ名」
（`SCRIPT.BUILTIN_PROPERTY_NAMES`）。

## Urpify の対処（Free）

これは無料パッケージに含まれる、Unity のコンバーター後の修正の 1 つです。同じスクリプト内で：

- シェーダー名を、Unity のコンバーターがその組み込みシェーダーに選ぶ URP のシェーダー（`Standard` なら
  `Universal Render Pipeline/Lit`）に、または Unity のルールがないシェーダーなら Urpify の表で最も近い URP のシェーダーに
  変えます。
- 同じメソッドが設定するプロパティ名を URP の名前（`_MainTex` は `_BaseMap`、`_Color` は `_BaseColor`）に変えます。

出力が「Built-in 対応を残す」の場合、各名前は実行時にアクティブなパイプラインに応じて選ばれるので、スクリプトは Built-in
でも動き続けます。

書き換えたスクリプトは保存前にコンパイルされます。コンパイルできなければ元に戻され、報告されます。

## どこまで近いか

`自動`。
