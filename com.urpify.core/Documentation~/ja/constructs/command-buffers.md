# カメライベントのコマンドバッファー

![ブース C03：コマンドバッファーで描画する光るシェル。Unity のコンバーター後は消え、Urpify の後は戻る](../../images/C03.jpg)

*ブース C03：`CameraEvent.AfterForwardAlpha` でカメラに追加した `CommandBuffer` が描画する光るシェル。*

## 何が壊れるか

URP は `Camera.AddCommandBuffer` を無視します：バッファーが描画するものは何の知らせもなく消えます。

検出結果：「... の CommandBuffer」（`SCRIPT.COMMANDBUFFER.CAMERA`）。

## Urpify の対処（Pro）

`camera.AddCommandBuffer(evt, cb)` は `camera.AddCommandBufferInUrp(evt, cb)` になります（`RemoveCommandBuffer` も同様）。
Urpify の Built-in Camera Hooks レンダラー機能が、Built-in のイベントに対応する URP のイベントでバッファーを実行します。

| Built-in の `CameraEvent` | URP の `RenderPassEvent` |
|---|---|
| `BeforeDepthTexture`、`AfterDepthTexture` | `BeforeRenderingPrePasses`、`AfterRenderingPrePasses` |
| `BeforeForwardOpaque`、`AfterForwardOpaque` | `BeforeRenderingOpaques`、`AfterRenderingOpaques` |
| `BeforeSkybox`、`AfterSkybox` | `BeforeRenderingSkybox`、`AfterRenderingSkybox` |
| `BeforeForwardAlpha`、`AfterForwardAlpha` | `BeforeRenderingTransparents`、`AfterRenderingTransparents` |
| `BeforeImageEffectsOpaque`、`AfterImageEffectsOpaque` | `AfterRenderingSkybox` |
| `BeforeImageEffects`、`AfterImageEffects`、`AfterEverything` | `BeforeRenderingPostProcessing`、ポストプロセスの後、`AfterRendering` |
| ディファード経路のイベント（`BeforeGBuffer`、`AfterLighting` など） | 不透明オブジェクトの前後で、URP のフォワードレンダラーにある最も近い位置 |

`DrawRenderer`、`DrawMesh`、`Blit`、`SetGlobal*`、一時レンダーテクスチャ、`SetRenderTarget`、`ClearRenderTarget` は記録
どおりに実行されます。

## どこまで近いか

`自動（近似）`。ディファード経路のイベントは性質上、近似になります。ライトのコマンドバッファー（`Light.AddCommandBuffer`）
はまだ対応していません。[変換されないもの](../not-converted.md) を参照してください。
