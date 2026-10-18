# Shaders vert/frag iluminados

![Puesto V02: un shader vert/frag iluminado con ForwardBase, ForwardAdd y sombras](../../images/V02.jpg)

*Puesto V02: pasadas ForwardBase y ForwardAdd, `SHADOW_ATTENUATION` y una pasada que proyecta sombras.*

## Qué se rompe

URP dibuja con el shader de error las pasadas etiquetadas `ForwardBase`, `ForwardAdd`, `Vertex`, `VertexLM` o
`PrepassBase`, así que el objeto es rosa. Las variables de luz de Built-in (`_LightColor0`, `_WorldSpaceLightPos0`), las
macros de sombras (`SHADOW_COORDS`, `TRANSFER_SHADOW`, `SHADOW_ATTENUATION`, `UNITY_LIGHT_ATTENUATION`) y
`UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"` no tienen equivalente en URP.

Hallazgos: «Pasada ForwardBase», «Pasada ForwardAdd», «Pasada ShadowCaster de Built-in», «LightMode ...», «UsePass ...».

## Qué hace Urpify (Pro)

| Built-in | URP |
|---|---|
| `ForwardBase` | `UniversalForward`; iluminación, sombras y luz ambiental llegan de URP a través de los nombres de Built-in |
| `ForwardAdd` | Un bucle sobre las luces adicionales de URP dentro de la pasada forward: el código de fragmentos del autor se llama una vez por luz y los resultados se suman |
| `ShadowCaster` | `ShadowCaster` de URP con `ApplyShadowBias`, conservando el código de vértices del autor; una copia `DepthOnly` dibuja la prepasada de profundidad |
| `UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"` | Una pasada de sombras generada |
| `UsePass` de otro shader del proyecto | La pasada se copia y se convierte con el resto |
| `Vertex`, `VertexLM` (solo vertex-lit) | `UniversalForward`, iluminado por vértice con la luz principal y las adicionales de URP |
| `_LightColor0`, `_WorldSpaceLightPos0` | La luz para la que se dibuja la pasada |
| `UNITY_LIGHT_ATTENUATION`, `SHADOW_ATTENUATION`, `LIGHT_ATTENUATION` | Atenuación por sombras y distancia de URP |
| `ShadeSH9`, `UNITY_FOG_*`, `UnityObjectToClipPos` y el resto de `UnityCG.cginc` | La biblioteca de Urpify, `BirpCompat.hlsl` |

`vert` y `frag` del autor se copian sin cambios. Los bloques CG pasan a ser bloques HLSL, `fixed` pasa a `half`, y
`sampler2D`/`tex2D` se quedan como están: compilan en URP 14 y 17.

## Cuánto se acerca

`Auto` para una sola pasada ForwardBase, `Automático, aproximado` con ForwardAdd: Built-in mezcla cada luz adicional en su
propia pasada, URP las suma en una. Las luces por vértice que Built-in da a ForwardBase (`Shade4PointLights`) se dibujan,
en cambio, por píxel en el bucle.
