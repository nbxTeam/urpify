# Shaders vert/frag iluminados

![Estande V02: um shader vert/frag iluminado com ForwardBase, ForwardAdd e sombras](../../images/V02.jpg)

*Estande V02: passadas ForwardBase e ForwardAdd, `SHADOW_ATTENUATION` e uma passada que projeta sombras.*

## O que quebra

O URP desenha com o shader de erro as passadas com as tags `ForwardBase`, `ForwardAdd`, `Vertex`, `VertexLM` ou
`PrepassBase`, então o objeto fica rosa. As variáveis de luz do Built-in (`_LightColor0`, `_WorldSpaceLightPos0`), as
macros de sombra (`SHADOW_COORDS`, `TRANSFER_SHADOW`, `SHADOW_ATTENUATION`, `UNITY_LIGHT_ATTENUATION`) e
`UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"` não têm equivalente no URP.

Achados: «Passada ForwardBase», «Passada ForwardAdd», «Passada ShadowCaster do Built-in», «LightMode ...», «UsePass ...».

## O que o Urpify faz (Pro)

| Built-in | URP |
|---|---|
| `ForwardBase` | `UniversalForward`; iluminação, sombras e luz ambiente vêm do URP pelos nomes do Built-in |
| `ForwardAdd` | Um loop sobre as luzes adicionais do URP dentro da passada forward: o código de fragmentos do autor é chamado uma vez por luz e os resultados são somados |
| `ShadowCaster` | `ShadowCaster` do URP com `ApplyShadowBias`, mantendo o código de vértices do autor; uma cópia `DepthOnly` desenha o pré-passe de profundidade |
| `UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"` | Uma passada de sombra gerada |
| `UsePass` de outro shader do projeto | A passada é copiada e convertida com o resto |
| `Vertex`, `VertexLM` (só vertex-lit) | `UniversalForward`, iluminado por vértice pela luz principal e pelas adicionais do URP |
| `_LightColor0`, `_WorldSpaceLightPos0` | A luz para a qual a passada está sendo desenhada |
| `UNITY_LIGHT_ATTENUATION`, `SHADOW_ATTENUATION`, `LIGHT_ATTENUATION` | Atenuação por sombra e distância do URP |
| `ShadeSH9`, `UNITY_FOG_*`, `UnityObjectToClipPos` e o resto do `UnityCG.cginc` | A biblioteca do Urpify, `BirpCompat.hlsl` |

`vert` e `frag` do autor são copiados sem mudanças. Blocos CG viram blocos HLSL, `fixed` vira `half`, e
`sampler2D`/`tex2D` ficam como estão: compilam no URP 14 e 17.

## Quão perto

`Auto` para uma única passada ForwardBase, `Automático, aproximado` com ForwardAdd: o Built-in mistura cada luz adicional
numa passada própria, o URP as soma numa. As luzes por vértice que o Built-in dá ao ForwardBase (`Shade4PointLights`) são
desenhadas por pixel no loop.
