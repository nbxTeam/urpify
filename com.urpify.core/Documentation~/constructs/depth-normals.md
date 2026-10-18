# Depth-normals texture and replacement shaders

The lab has no booth for these constructs; they were checked on ToonWaterShader (IronWarrior, Unlicense), whose water
foam depends on both.

## What breaks

- `_CameraDepthNormalsTexture` does not exist in URP. A shader that reads it gets nothing: outlines from normals and
  foam that follows the shore disappear.
- `Camera.SetReplacementShader` and `Camera.RenderWithShader` are not supported by URP. A script that renders the scene
  with a replacement shader into a texture (a common way to get normals or depth for an effect) renders nothing useful,
  silently. In ToonWaterShader this removed the water foam.

Findings: "Reads _CameraDepthNormalsTexture" (`SHADER.DEPTHNORMALS_TEXTURE`), the replacement shader call
(`SCRIPT.REPLACEMENT_SHADER`).

## What Urpify does (Pro)

- Urpify's Built-in Camera Hooks renderer feature writes `_CameraDepthNormalsTexture` from URP's depth and normals
  textures, in Built-in's encoding (`EncodeDepthNormal`), so the shader reads it as before. The feature asks URP for its
  normals texture only when a shader needs it.
- A replacement-shader camera is converted to a pass of the same renderer feature that draws the camera's opaque objects
  with the replacement shader into the same target texture, with the same tag matching (`RenderType`).

## How close

`AutoApprox`. URP's normals come from its own depth-normals pass, so edges can differ slightly from Built-in's.
