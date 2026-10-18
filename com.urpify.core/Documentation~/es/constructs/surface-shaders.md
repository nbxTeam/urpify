# Surface shaders

![Puesto S01: un surface shader Standard, rosa tras el conversor de Unity, como en Built-in tras Urpify](../../images/S01.jpg)

*Puesto S01: un surface shader con el modelo de iluminación Standard, albedo, normal map y oclusión.*

## Qué se rompe

`#pragma surface` es una función de la pipeline integrada: Unity genera las pasadas del shader (ForwardBase, ForwardAdd,
ShadowCaster, Deferred, Meta) a partir de la función surface. URP no tiene surface shaders, y esas pasadas se dibujan con
el shader de error, así que cada objeto con ese shader se vuelve rosa. El conversor de Unity no toca los shaders propios.

Hallazgo: «Surface shader, iluminación ...» (`SHADER.SURFACE`), gravedad «Se ve rosa».

## Qué hace Urpify (Pro)

El conversor escribe las pasadas que URP necesita alrededor de la función surface del autor, que copia tal cual:

| Pasada | A partir de |
|---|---|
| `UniversalForward` | la función surface y después la iluminación de URP para el modelo de iluminación |
| `ShadowCaster` | el mismo código de vértices con el sesgo de sombras de URP |
| `DepthOnly`, `DepthNormals` | el mismo código de vértices; para la textura de profundidad, depth priming y SSAO |
| `Meta` | el albedo y la emisión de la función surface, para hornear lightmaps |

| Modelo de iluminación | Iluminación en URP |
|---|---|
| `Standard`, `StandardSpecular` | `UniversalFragmentPBR`, flujo metálico o especular |
| `Lambert`, `BlinnPhong` | `LightingLambert` y `LightingBlinnPhong` de Built-in desde la biblioteca de Urpify, para la luz principal y cada luz adicional |
| Tu `Lighting<Name>` | tu función, consulta [Iluminación propia](surface-custom-lighting.md) |

La estructura `Input` se rellena como en Built-in: coordenadas `uv_` y `uv2_` con el mosaico de la textura, `worldPos`,
`worldNormal`, `worldRefl`, `viewDir`, `screenPos`, `color : COLOR`, `facing : VFACE`, `INTERNAL_DATA` con
`WorldNormalVector` y los campos que rellena tu función `vertex:`. Las propiedades del material pasan a `UnityPerMaterial`
para el SRP Batcher, y el nombre del shader y los nombres de las propiedades se quedan, así que los materiales conservan
sus valores.

Varios bloques `SubShader` y el LOD mantienen su significado. Un `Fallback` a un shader integrado apunta al shader de URP
correspondiente.

## Cuánto se acerca

`Auto` para Standard, StandardSpecular, Lambert y BlinnPhong: el resultado coincide con Built-in salvo las diferencias de
la propia iluminación de URP (consulta [En qué se ve distinto URP](../urp-differences.md)). Cada shader convertido también
se renderiza en una esfera en Built-in y en URP; si cambia más del 25 % de la esfera, se marca como `Revisar`.

![Puesto S02: StandardSpecular](../../images/S02.jpg)

*S02: StandardSpecular.*

![Puesto S04: BlinnPhong con alpha:fade](../../images/S04.jpg)

*S04: BlinnPhong con `alpha:fade`, una esfera transparente.*
