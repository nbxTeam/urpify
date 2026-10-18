# Settings

Project Settings > **Urpify**, or **Settings** in the Urpify window.

Team settings are saved in `ProjectSettings/UrpifySettings.asset`: commit it, and everyone on the team migrates the same
way. Personal settings are saved in `UserSettings/Urpify.json` and stay on your machine.

## Conversion (team)

These settings shape what the Pro converters write.

| Setting | Values | Default | What it does |
|---|---|---|---|
| Output | Replace original, Write copy, Keep Built-in support | Replace original | **Replace original**: the shader file becomes URP-only; the original is in the backup. **Write copy**: the URP shader is written next to the original as `Name_URP.shader`, named `Name (URP)`, and the materials move to it; the original stays. **Keep Built-in support**: one file for both pipelines; the original SubShaders stay as they are for Built-in and a URP SubShader is added before them. |
| GrabPass source | Opaque Texture, After transparents | Opaque Texture | **Opaque Texture**: a converted GrabPass reads URP's copy of the screen, taken before transparent objects, so they are missing from refractions. **After transparents**: Urpify's renderer feature copies the screen after transparent objects, as Built-in's GrabPass did, and draws the GrabPass objects after it. Costs one more screen copy per frame. |
| Screen effects inject at | After post-processing, Before post-processing, After opaques | After post-processing | Where converted `OnRenderImage` effects run in URP's frame. `[ImageEffectOpaque]` effects always run after the skybox, as in Built-in. |
| Shadows | Same as original, Always, Never | Same as original | Whether converted surface shaders cast and receive shadows. |
| SRP Batcher | On, Off | On | Puts material properties in the `UnityPerMaterial` buffer, so URP's SRP Batcher can batch converted shaders. A shader that declares its own `UnityPerMaterial` keeps it; a property the converter cannot move safely stays outside, with a note. |

## Compare (team)

| Setting | Default | What it does |
|---|---|---|
| Capture size | Game view | The size of each captured frame: the Game view's, so effects that depend on the resolution look as you see them, or Custom. The command line uses `-urpifyWidth` and `-urpifyHeight`. |
| Capture width, Capture height | 960 × 540 | The size with Capture size set to Custom, and when there is no Game view. |
| Frames before capture | 30 | Frames played with a fixed time step before each capture, so animations and particles are in the same state before and after. |
| Scenes | Build Settings | Which scenes Capture before and Capture after take: those in Build Settings, every scene under `Assets`, or a list. |
| Scene paths | empty | With Scenes set to Selected scenes: paths separated by semicolons, such as `Assets/Scenes/Main.unity;Assets/Scenes/Menu.unity`. |

The thresholds for New pink, Changed and Same are calibrated on Unity's own Standard to URP Lit conversion and apply to
every project: a new pink area larger than 0.02 % of the frame is New pink; more changed pixels than Standard to URP Lit
produces, or one large changed area, is Changed.

Each scene can have **Migration Viewpoints**: add the `MigrationViewpoint` component to an empty GameObject to capture
from its position and rotation, besides the main camera. A viewpoint can set its own field of view and far clip plane.
It is tagged EditorOnly and never reaches a build.

## Personal

| Setting | Values | Default | What it does |
|---|---|---|---|
| Language | Editor language, or one of 8 languages | Editor language | The language of Urpify's windows. Editor language follows Preferences > Languages and falls back to English. |
| Built-in preview | Live, From captures | Live | **Live** switches the render pipeline off for the moment of rendering a Built-in preview and back, inside one call; it never runs in Play Mode or while Unity imports or compiles. **From captures** shows the scene captures instead. If a live preview fails, or the pipeline settings do not come back exactly as they were, Urpify switches to From captures by itself and says so. |
| Project window overlays | On, Off | On | A small mark on the assets the last scan found broken. |

## Overrides for one run

The command line can override Output (`-urpifyShaderOutput`) and GrabPass source (`-urpifyGrabPass`) for one run, see
[Command line](cli.md).
