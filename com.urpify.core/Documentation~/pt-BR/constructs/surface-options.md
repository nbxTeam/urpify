# Opções dos surface shaders

`#pragma surface surf Standard <opções>` muda o que a Unity gera. O Urpify lê cada opção e mantém o significado dela nas
passadas que escreve (Pro).

![Estande S03: Lambert com uma função de vento vertex:](../../images/S03.jpg)

*S03: `vertex:vert` dobra a malha como o vento. A função de vértices roda em cada passada, então a sombra também dobra.*

![Estande S08: dissolução com clip() e addshadow](../../images/S08.jpg)

*S08: dissolução com `clip()` e `addshadow`: a sombra é recortada do mesmo jeito.*

![Estande S10: decal:add](../../images/S10.jpg)

*S10: `decal:add`, uma camada aditiva sobre objetos opacos.*

![Estande S11: neblina por altura em finalcolor:](../../images/S11.jpg)

*S11: neblina por altura em `finalcolor:`, aplicada depois da iluminação, como no Built-in.*

## Opções e no que elas viram

| Opção | No URP |
|---|---|
| `vertex:func` | Roda em cada passada gerada: forward, sombras, profundidade, depth-normals, meta. Os campos extras de `Input` que ela preenche chegam à função surface. |
| `finalcolor:func` | É chamada com a cor iluminada, antes da neblina. |
| `finalgbuffer:func` | Não é usada: só roda na passada G-buffer deferred do Built-in. Uma nota avisa isso. |
| `alpha`, `alpha:blend`, `alpha:fade`, `alpha:premul` | Fila transparente e a mistura correspondente; `premul` mantém o especular pré-multiplicado. |
| `alphatest:prop` | `clip()` contra a propriedade, em cada passada. |
| `keepalpha` | O alfa da função surface também é gravado nas passadas opacas. |
| `decal:add`, `decal:blend` | Desenhado depois dos objetos opacos com mistura aditiva ou alfa, sem sombras. |
| `addshadow` | A passada de sombras roda a função de vértices e o recorte da superfície, como a passada forward. |
| `noshadow` | Sem passada de sombras; a passada forward não recebe sombras. |
| `noambient`, `novertexlights`, `nolightmap`, `nofog`, `nometa`, `noforwardadd` | A parte correspondente sai: luz ambiente, luzes por vértice, lightmaps, neblina, a passada Meta, luzes adicionais. |
| `halfasview`, `interpolateview`, `approxview`, `dualforward` | Ajustes de qualidade da direção de visão de que o URP não precisa; ignorados com uma nota. |
| `fullforwardshadows`, `nodynlightmap`, `nodirlightmap`, `nolppv`, `noshadowmask`, `noinstancing`, `exclude_path:*`, `dithercrossfade` | Reconhecidas; as configurações do próprio URP decidem isso (sombras de luzes adicionais, modos de lightmap, light probe proxy volumes, crossfade de LOD), então não mudam nada no shader convertido. |
| `tessellate:func`, `tessphong:` | Veja [Tesselação](surface-tessellation.md). |

Uma opção que o Urpify não conhece aparece no achado «Opções de surface sem suporte» (`SHADER.SURFACE.UNKNOWN_OPTION`); o
resto do shader é convertido mesmo assim.

A configuração Sombras (Project Settings > Urpify) pode forçar ou desligar as sombras em todos os surface shaders
convertidos.
