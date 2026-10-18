# Depth texture

![Lab booth D01: water with intersection foam, white after Unity's converter, the same as Built-in after Urpify](../images/D01.jpg)

*Lab booth D01: transparent water that reads `_CameraDepthTexture` for its depth colour and the foam around the rock.
After Unity's converter alone the water turns white.*

## What breaks

A shader that samples `_CameraDepthTexture` gets the depth only when Depth Texture is on in the URP Asset. Unity's
converter turns it on only for quality levels that had Soft Particles. A transparent shader has a second problem: URP
copies depth after the transparent objects by default, and Unity's converter keeps that. In Unity 6 such a shader then
reads no depth at all, so the water turns white; in 2022.3 it reads the previous frame's depth, so still frames look
right and moving objects leave a trail.

Findings: "Reads _CameraDepthTexture" (`SHADER.DEPTH_TEXTURE`), "Transparent, reads ..." (`SHADER.DEPTH_TEXTURE_TRANSPARENT`).

## What Urpify does

- **Free and Pro:** turns Depth Texture on in every URP Asset, and for a transparent reader sets the renderer's Depth
  Texture Mode to After Opaques. A shader URP draws as it is then works without any change to its code: booth D01 is
  fixed by the free package alone.
- **Pro, when the shader is converted:** the author's `sampler2D _CameraDepthTexture` declaration is replaced by URP's
  `DeclareDepthTexture.hlsl`, and reads (`tex2D`, `tex2Dproj`, `SAMPLE_DEPTH_TEXTURE`, `SAMPLE_DEPTH_TEXTURE_PROJ`) become
  `SampleSceneDepth`. This also makes the shader work with single-pass instanced XR, where the depth texture is an array.
  `LinearEyeDepth`, `Linear01Depth` and `COMPUTE_EYEDEPTH` come from Urpify's library.

## How close

`Auto`. Where a read cannot be rewritten, the author's declaration stays and the finding says that single-pass instanced
XR is not supported for that shader.
