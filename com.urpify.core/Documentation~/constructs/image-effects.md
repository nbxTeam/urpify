# Image effects: OnRenderImage

![Lab booth C02: a grayscale and vignette image effect, gone after Unity's converter, back after Urpify](../images/C02.jpg)

*Lab booth C02: an `OnRenderImage` post effect, grayscale with a vignette. After Unity's converter alone the effect is
gone, without an error.*

## What breaks

URP never calls `OnRenderImage`, so every image effect written for Built-in stops running. There is no error and no pink:
the game simply looks different. Checked on URP 14 and 17.

Finding: "OnRenderImage (N Blit calls)" (`SCRIPT.ONRENDERIMAGE`), severity Breaks without error. The shader of the effect
gets "Image effect shader".

## What Urpify does (Pro)

The script stays a MonoBehaviour on the camera and keeps working as a remote control: its fields, the values it sets
every frame and its materials are unchanged. Urpify's Built-in Camera Hooks renderer feature calls the script's
`OnRenderImage` at the point of URP's frame chosen in Settings (Screen effects inject at: after post-processing by
default), with the camera's colour as the source. The script's `Graphics.Blit` calls go through `ImageEffectGraphics`,
which records them into URP's frame with the same materials, passes and temporary render textures, in the same order.
`[ImageEffectOpaque]` effects run after the skybox, as in Built-in. Several effects on one camera keep their order.
`GL` drawing inside `OnRenderImage` (quads, matrices, clears) is recorded too.

The effect's shader stays as it is: `vert_img`, `v2f_img` and `_MainTex` work, because the effect still blits the screen
into `_MainTex` as Built-in did.

## How close

`Auto` for one Blit, `AutoApprox` for chains and command buffers. An effect that
draws immediately with `Graphics.DrawMeshNow` or similar cannot be recorded into URP's frame: the finding says how to move
it to a command buffer. An effect that renders a camera (`Camera.Render`) is not run under URP either: inside its own
camera's frame no other camera can render. Render that camera in `OnPreRender` instead.

The `Graphics.Blit` calls of other scripts the effect calls (a ScriptableObject effect behind the camera script, a manager
that runs a chain) are recorded the same way. An effect whose chain renders a camera, dispatches a compute shader or reads
Built-in's G-buffer (`_CameraGBufferTexture0-3`) is left as it is (Not supported yet): under URP it is missing and the rest
of the frame is drawn. If a scene's frame turns almost one flat colour after the migration, the effects the scene runs are
marked Review: turn them off on the camera to find the cause, or roll back.

Built players keep the effect: everything Urpify calls by name is preserved from code stripping, checked in Mono and
IL2CPP builds.
