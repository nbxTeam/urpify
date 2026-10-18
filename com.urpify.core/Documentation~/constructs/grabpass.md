# GrabPass

![Lab booth G01: GrabPass refraction glass, invisible after Unity's converter, refracting again after Urpify](../images/G01.jpg)

*Lab booth G01: refraction glass with an anonymous `GrabPass {}`. After Unity's converter alone the glass is gone,
without an error.*

![Lab booth G02: named GrabPass heat haze](../images/G02.jpg)

*Lab booth G02: heat haze with a named `GrabPass { "_HazeBackground" }`.*

## What breaks

URP has no GrabPass. The pass that grabs the screen is skipped, the texture stays empty, and the object disappears or
loses its refraction. There is no pink and no error: a silent break.

Finding: "GrabPass (...)" (`SHADER.GRABPASS`), severity Breaks without error.

## What Urpify does (Pro)

The GrabPass is removed, and the texture it filled (`_GrabTexture` or the named one) reads a copy of the screen instead:

- **GrabPass source = Opaque Texture** (default): URP's `_CameraOpaqueTexture`, taken after opaque objects and the
  skybox, before transparent ones. Urpify turns Opaque Texture on in every URP Asset, with no downsampling.
- **GrabPass source = After transparents**: Urpify's Built-in Camera Hooks renderer feature copies the screen after the
  transparent objects into `_UrpifyGrabTexture`, then draws the GrabPass objects (tag `UrpifyGrabPass`), as Built-in's
  GrabPass did. One more screen copy per frame.

A GrabPass placed after one or two passes of the same shader (fog, then a blur that reads the grab) reads a copy taken
after those passes, so it sees what Built-in's grab saw.

## How close

`AutoApprox` with Opaque Texture: transparent objects behind the glass are missing from the refraction. With After
transparents, the result matches Built-in. Several GrabPasses with different textures in one shader are not supported yet:
URP has one Opaque Texture.

On 2022.3, Urpify's self-check renders GrabPass shaders with a game camera: URP 14 copies no Opaque Texture for a preview
camera.
