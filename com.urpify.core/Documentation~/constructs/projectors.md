# Projectors

![Lab booth C01: a projector blob shadow, gone after Unity's converter, back after Urpify](../images/C01.jpg)

*Lab booth C01: a `Projector` component casting a blob shadow under the cube. After Unity's converter alone the shadow
is gone.*

## What breaks

URP does not render `Projector` components. Blob shadows, selection rings and projected decals disappear, silently.

Findings: "Projector on ..." (`SCENE.PROJECTOR`), "Projector shader" (`SHADER.PROJECTOR`).

## What Urpify does (Pro)

The `Projector` stays a projector and its shader stays as it is. Urpify's Built-in Camera Hooks renderer feature draws
each enabled projector's material on the renderers inside its frustum and layers, with `unity_Projector` and
`unity_ProjectorClip` set per object as Built-in sets them. Size, orientation, range, ignored layers and the material
come from the component, so you keep editing projectors the way you did.

The feature keeps a cache of the projectors and the renderers they can reach instead of searching the scene every frame:
projectors are refreshed every 30 frames, renderers every 60, and both when a scene loads or unloads. Renderers are culled
by each projector's frustum and by the camera's. Measured on Unity 6.5 with one projector: 0.02 ms per frame with 88
renderers; with 5,000 more renderers about 2.2 ms when all of them are in front of the camera and 0.09 ms when they are
behind it.

Because nothing changes in the projector or its shader, a package exported with Keep Built-in support keeps working in
both pipelines.

## How close

`AutoApprox`: the same shader on the same objects. URP's own Decal Projector is not used, so the look stays Built-in's.
