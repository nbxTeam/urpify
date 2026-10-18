# Surface-шейдеры

![Стенд S01: surface-шейдер Standard, розовый после конвертера Unity, как в Built-in после Urpify](../../images/S01.jpg)

*Стенд S01: surface-шейдер с моделью освещения Standard, альбедо, картой нормалей и затенением.*

## Что ломается

`#pragma surface` — возможность встроенного конвейера: Unity генерирует проходы шейдера (ForwardBase, ForwardAdd,
ShadowCaster, Deferred, Meta) из surface-функции. В URP surface-шейдеров нет, и эти проходы рисуются шейдером ошибки,
поэтому каждый объект с таким шейдером становится розовым. Конвертер Unity свои шейдеры не трогает.

Находка: «Surface-шейдер, освещение ...» (`SHADER.SURFACE`), серьёзность «Станет розовым».

## Что делает Urpify (Pro)

Конвертер пишет нужные URP проходы вокруг surface-функции автора, которую копирует как есть:

| Проход | Из чего |
|---|---|
| `UniversalForward` | surface-функция, затем освещение URP для модели освещения |
| `ShadowCaster` | тот же вершинный код со смещением теней URP |
| `DepthOnly`, `DepthNormals` | тот же вершинный код; для текстуры глубины, depth priming и SSAO |
| `Meta` | альбедо и свечение surface-функции для запекания лайтмапов |

| Модель освещения | Освещение в URP |
|---|---|
| `Standard`, `StandardSpecular` | `UniversalFragmentPBR`, металлический или зеркальный процесс |
| `Lambert`, `BlinnPhong` | `LightingLambert` и `LightingBlinnPhong` встроенного конвейера из библиотеки Urpify, для основного и каждого дополнительного источника |
| Ваша `Lighting<Name>` | ваша функция, см. [Своё освещение](surface-custom-lighting.md) |

Структура `Input` заполняется так же, как в Built-in: координаты `uv_` и `uv2_` с тайлингом текстуры, `worldPos`,
`worldNormal`, `worldRefl`, `viewDir`, `screenPos`, `color : COLOR`, `facing : VFACE`, `INTERNAL_DATA` с
`WorldNormalVector` и поля, которые заполняет ваша функция `vertex:`. Свойства материала переносятся в `UnityPerMaterial`
для SRP Batcher, а имя шейдера и имена свойств остаются, поэтому материалы сохраняют значения.

Несколько блоков `SubShader` и LOD сохраняют смысл. `Fallback` на встроенный шейдер указывает на соответствующий шейдер
URP.

## Насколько близко

`Авто` для Standard, StandardSpecular, Lambert и BlinnPhong: результат совпадает с Built-in в пределах отличий
собственного освещения URP (см. [Чем URP отличается на вид](../urp-differences.md)). Каждый переведённый шейдер также
рендерится на сфере в Built-in и URP; если изменилось больше 25 % сферы, он помечается `Проверить`.

![Стенд S02: StandardSpecular](../../images/S02.jpg)

*S02: StandardSpecular.*

![Стенд S04: BlinnPhong с alpha:fade](../../images/S04.jpg)

*S04: BlinnPhong с `alpha:fade`, прозрачная сфера.*
