# Scripts that render cameras: mirrors, portals, water reflections

![Lab booth M01: a planar mirror, black after Unity's converter, reflecting again after Urpify](../images/M01.jpg)

*Lab booth M01: a planar mirror written the way MirrorReflection and Standard Assets Water do it:
`OnWillRenderObject`, `Camera.current`, a reflection camera with an oblique near plane and `Camera.Render`. After Unity's
converter alone the mirror is black.*

## What breaks

URP never calls `OnPreCull`, `OnPreRender` or `OnPostRender` on a camera, and calls `OnWillRenderObject` while it culls,
where no other camera can render. Portals and mirrors that render a second camera in those methods show a flat or black
surface. There is no error. Checked on URP 14 and 17.

Findings: "OnPreCull on a camera" and the others (`SCRIPT.CAMERA_CALLBACKS`), "OnWillRenderObject renders a camera"
(`SCRIPT.WILLRENDER_CAMERA`).

## What Urpify does (Pro)

The script keeps its methods and its logic. Urpify registers it with URP's camera events:

- `OnPreCull` and `OnPreRender` run from `RenderPipelineManager.beginCameraRendering` of the camera they belong to,
  `OnPostRender` from `endCameraRendering`.
- `OnWillRenderObject` runs before URP culls the camera, for the renderers the camera sees, with `Camera.current` set to
  that camera.
- `Camera.Render()` inside them renders into URP's frame through a render request, so the reflection or portal texture
  is filled before the camera that shows it draws.
- `OnRenderObject` runs after the camera's opaque and transparent objects.

Known scripts are recognized by Urpify's catalog: SebLague's Portals, MirrorReflection, Standard Assets Water and
ToonWaterShader's normals camera are converted with recipes checked on those projects. Others use the same mechanism
and are `AutoApprox`.

## How close

`Auto` for the catalog's checked recipes, `AutoApprox` for the rest: check those scenes in Compare scenes. One difference
from Built-in: `OnWillRenderObject` is called for renderers inside the camera's frustum without occlusion culling, so a
mirror hidden behind a wall still renders its reflection.

Code added to the `Camera.onPreCull`, `Camera.onPreRender` and `Camera.onPostRender` delegates is not converted yet; the
finding says how to move it.
