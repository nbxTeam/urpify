# Beleuchtete Vert/Frag-Shader

![Stand V02: ein beleuchteter Vert/Frag-Shader mit ForwardBase, ForwardAdd und Schatten](../../images/V02.jpg)

*Stand V02: die Passes ForwardBase und ForwardAdd, `SHADOW_ATTENUATION` und ein Schattenwurf-Pass.*

## Was kaputtgeht

URP zeichnet Passes mit den Tags `ForwardBase`, `ForwardAdd`, `Vertex`, `VertexLM` oder `PrepassBase` mit dem
Fehler-Shader, daher ist das Objekt pink. Die Lichtvariablen von Built-in (`_LightColor0`, `_WorldSpaceLightPos0`), die
Schattenmakros (`SHADOW_COORDS`, `TRANSFER_SHADOW`, `SHADOW_ATTENUATION`, `UNITY_LIGHT_ATTENUATION`) und
`UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"` haben in URP kein Gegenstück.

Befunde: „ForwardBase-Pass“, „ForwardAdd-Pass“, „ShadowCaster-Pass aus Built-in“, „LightMode ...“, „UsePass ...“.

## Was Urpify tut (Pro)

| Built-in | URP |
|---|---|
| `ForwardBase` | `UniversalForward`; Beleuchtung, Schatten und Umgebungslicht kommen über die Built-in-Namen von URP |
| `ForwardAdd` | Eine Schleife über die zusätzlichen Lichter von URP im Forward-Pass: Der Fragment-Code des Autors wird einmal pro Licht aufgerufen, die Ergebnisse werden addiert |
| `ShadowCaster` | `ShadowCaster` von URP mit `ApplyShadowBias`, der Vertex-Code des Autors bleibt; eine `DepthOnly`-Kopie zeichnet den Tiefen-Prepass |
| `UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"` | Ein erzeugter Schatten-Pass |
| `UsePass` aus einem anderen Shader des Projekts | Der Pass wird kopiert und mit dem Rest konvertiert |
| `Vertex`, `VertexLM` (nur Vertex-Lit) | `UniversalForward`, pro Vertex beleuchtet vom Haupt- und den zusätzlichen Lichtern von URP |
| `_LightColor0`, `_WorldSpaceLightPos0` | Das Licht, für das der Pass gerade gezeichnet wird |
| `UNITY_LIGHT_ATTENUATION`, `SHADOW_ATTENUATION`, `LIGHT_ATTENUATION` | Schatten- und Entfernungsabschwächung aus URP |
| `ShadeSH9`, `UNITY_FOG_*`, `UnityObjectToClipPos` und der Rest von `UnityCG.cginc` | Urpifys Bibliothek, `BirpCompat.hlsl` |

`vert` und `frag` des Autors werden unverändert übernommen. CG-Blöcke werden zu HLSL-Blöcken, `fixed` wird zu `half`, und
`sampler2D`/`tex2D` bleiben, wie sie sind: Sie kompilieren in URP 14 und 17.

## Wie nah

`Auto` für einen einzelnen ForwardBase-Pass, `Auto, angenähert` mit ForwardAdd: Built-in blendet jedes zusätzliche Licht
in einem eigenen Pass, URP addiert sie in einem. Lichter pro Vertex, die Built-in an ForwardBase gibt
(`Shade4PointLights`), werden stattdessen pro Pixel in der Schleife gezeichnet.
