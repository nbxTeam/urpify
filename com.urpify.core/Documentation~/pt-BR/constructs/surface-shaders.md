# Surface shaders

![Estande S01: um surface shader Standard, rosa depois do conversor da Unity, como no Built-in depois do Urpify](../../images/S01.jpg)

*Estande S01: um surface shader com o modelo de iluminação Standard, albedo, normal map e oclusão.*

## O que quebra

`#pragma surface` é um recurso da pipeline embutida: a Unity gera as passadas do shader (ForwardBase, ForwardAdd,
ShadowCaster, Deferred, Meta) a partir da função surface. O URP não tem surface shaders, e essas passadas são desenhadas
com o shader de erro, então cada objeto com esse shader fica rosa. O conversor da Unity não mexe em shaders próprios.

Achado: «Surface shader, iluminação ...» (`SHADER.SURFACE`), gravidade «Fica rosa».

## O que o Urpify faz (Pro)

O conversor escreve as passadas de que o URP precisa em volta da função surface do autor, que ele copia como está:

| Passada | A partir de |
|---|---|
| `UniversalForward` | a função surface e depois a iluminação do URP para o modelo de iluminação |
| `ShadowCaster` | o mesmo código de vértices com o bias de sombra do URP |
| `DepthOnly`, `DepthNormals` | o mesmo código de vértices; para a textura de profundidade, depth priming e SSAO |
| `Meta` | o albedo e a emissão da função surface, para assar lightmaps |

| Modelo de iluminação | Iluminação no URP |
|---|---|
| `Standard`, `StandardSpecular` | `UniversalFragmentPBR`, fluxo metálico ou especular |
| `Lambert`, `BlinnPhong` | `LightingLambert` e `LightingBlinnPhong` do Built-in, da biblioteca do Urpify, para a luz principal e cada luz adicional |
| A sua `Lighting<Name>` | a sua função, veja [Iluminação própria](surface-custom-lighting.md) |

A estrutura `Input` é preenchida como no Built-in: coordenadas `uv_` e `uv2_` com o tiling da textura, `worldPos`,
`worldNormal`, `worldRefl`, `viewDir`, `screenPos`, `color : COLOR`, `facing : VFACE`, `INTERNAL_DATA` com
`WorldNormalVector` e os campos que a sua função `vertex:` preenche. As propriedades do material vão para
`UnityPerMaterial` para o SRP Batcher, e o nome do shader e os nomes das propriedades ficam, então os materiais mantêm os
valores.

Vários blocos `SubShader` e o LOD mantêm o significado. Um `Fallback` para um shader embutido aponta para o shader do URP
correspondente.

## Quão perto

`Auto` para Standard, StandardSpecular, Lambert e BlinnPhong: o resultado bate com o Built-in, a não ser pelas diferenças
da própria iluminação do URP (veja [Onde o URP fica diferente](../urp-differences.md)). Cada shader convertido também é
renderizado numa esfera no Built-in e no URP; se mais de 25 % da esfera mudar, ele é marcado como `Revisar`.

![Estande S02: StandardSpecular](../../images/S02.jpg)

*S02: StandardSpecular.*

![Estande S04: BlinnPhong com alpha:fade](../../images/S04.jpg)

*S04: BlinnPhong com `alpha:fade`, uma esfera transparente.*
