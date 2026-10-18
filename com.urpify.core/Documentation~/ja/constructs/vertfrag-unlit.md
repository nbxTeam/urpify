# ライティングなしの vert/frag シェーダー

![ブース V01：フォグと UV スクロールのあるライティングなしの CG シェーダー。3 つとも同じ](../../images/V01.jpg)

*ブース V01：フォグと UV スクロールのあるライティングなしの CG シェーダー。URP はそのまま描画します。*

## 何が起こるか

LightMode タグのない、または `Always` の vert/frag シェーダーは、多くの場合 URP でそのまま描画されます：URP は LightMode
タグのないパスを `SRPDefaultUnlit` として描画し、`UnityCG.cginc` を使う CG コードもそのままコンパイルできます。Urpify は
シェーダーが URP でコンパイルできることを確認し、できればそのままにします。

検出結果：なし、または URP がそのまま描画するという注記。

それでも壊れることがあり、それぞれ専用のページがあるもの：
- LightMode のないパスが 2 つ以上：[追加パス](extra-passes.md)
- `_CameraDepthTexture` を読む：[深度テクスチャ](depth-texture.md)
- `GrabPass`：[GrabPass](grabpass.md)
- ライティングなしのパスで使う `_LightColor0` などの Built-in のライト変数：URP では 0 になります。Pro はこれを URP の
  メインライトに対応付けます。
