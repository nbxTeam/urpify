# Built-in 対応を残す：両方のパイプライン用の 1 つのシェーダー

出力 = **Built-in 対応を残す**（Project Settings > Urpify > 変換、Pro）は、変換した各シェーダーを Built-in でも URP でも
動く 1 つのファイルとして書き出します。どちらのパイプラインの購入者にも販売する Asset Store の作者や、プラットフォームに
よってパイプラインを切り替えるプロジェクト向けです。

## ファイルの構成

```
Shader "Author/Water" {
    Properties { ... }                      // 変更なし
    SubShader {                             // Urpify が先頭に追加
        Tags { "RenderPipeline" = "UniversalPipeline" }
        PackageRequirements { "com.unity.render-pipelines.universal" }
        ...                                 // 変換したパス
    }
    SubShader { ... }                       // 元の Built-in の SubShader（バイト単位で同一）
    Fallback "..."
}
```

Unity はアクティブなパイプラインに合う最初の SubShader を選びます。URP がアクティブなら URP の SubShader を使い、
Built-in ではタグによってそれを飛ばし、元のものを使います。`PackageRequirements` により、URP のない Built-in プロジェクト
では URP 側をコンパイルすること自体がありません。Unity 6000.5 と 2022.3 で確認済み。

元の SubShader には手を加えないので、Built-in でのマテリアルの描画はこれまでとまったく同じです。これはバイト単位で確認
しています：「Built-in 対応を残す」で変換したラボのすべてのシェーダーは、Built-in で元と同じ画像を描画します。

## この出力で他に変わること

- マテリアルは元のシェーダーのままなので、同じマテリアルが両方のパイプラインで動きます。
- スクリプト内の Built-in 名による `Shader.Find` は、実行時にアクティブなパイプラインに応じて名前を選びます。
- 変換したスクリプトとレンダラー機能は URP でのみ動き、Built-in では何もしません。

## コスト

SubShader が 2 つあるシェーダーは両方をコンパイルするため、それを含むビルドはバリアントが増えます。両パイプライン用
シェーダーでのビルドサイズと時間はラボで測定しました：[よくある質問](../faq.md) を参照してください。
