# Освещённые vert/frag-шейдеры

![Стенд V02: освещённый vert/frag-шейдер с ForwardBase, ForwardAdd и тенями](../../images/V02.jpg)

*Стенд V02: проходы ForwardBase и ForwardAdd, `SHADOW_ATTENUATION` и проход отбрасывания теней.*

## Что ломается

URP рисует проходы с тегами `ForwardBase`, `ForwardAdd`, `Vertex`, `VertexLM` или `PrepassBase` шейдером ошибки, поэтому
объект розовый. У переменных света Built-in (`_LightColor0`, `_WorldSpaceLightPos0`), макросов теней (`SHADOW_COORDS`,
`TRANSFER_SHADOW`, `SHADOW_ATTENUATION`, `UNITY_LIGHT_ATTENUATION`) и `UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"`
нет аналога в URP.

Находки: «Проход ForwardBase», «Проход ForwardAdd», «Проход ShadowCaster из Built-in», «LightMode ...», «UsePass ...».

## Что делает Urpify (Pro)

| Built-in | URP |
|---|---|
| `ForwardBase` | `UniversalForward`; освещение, тени и рассеянный свет приходят из URP через имена Built-in |
| `ForwardAdd` | Цикл по дополнительным источникам URP внутри forward-прохода: фрагментный код автора вызывается по разу на источник, результаты складываются |
| `ShadowCaster` | `ShadowCaster` URP с `ApplyShadowBias`, вершинный код автора сохраняется; копия `DepthOnly` рисует предпроход глубины |
| `UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"` | Сгенерированный проход теней |
| `UsePass` из другого шейдера проекта | Проход копируется и переводится вместе с остальным |
| `Vertex`, `VertexLM` (только vertex-lit) | `UniversalForward`, освещается повершинно основным и дополнительными источниками URP |
| `_LightColor0`, `_WorldSpaceLightPos0` | Источник, для которого рисуется проход |
| `UNITY_LIGHT_ATTENUATION`, `SHADOW_ATTENUATION`, `LIGHT_ATTENUATION` | Затухание по теням и расстоянию из URP |
| `ShadeSH9`, `UNITY_FOG_*`, `UnityObjectToClipPos` и остальное из `UnityCG.cginc` | Библиотека Urpify, `BirpCompat.hlsl` |

`vert` и `frag` автора копируются без изменений. Блоки CG становятся блоками HLSL, `fixed` становится `half`, а
`sampler2D`/`tex2D` остаются как есть: они компилируются в URP 14 и 17.

## Насколько близко

`Авто` для одного прохода ForwardBase, `Авто, приблизительно` с ForwardAdd: Built-in смешивает каждый дополнительный
источник своим проходом, URP складывает их в одном. Повершинные источники, которые Built-in отдаёт ForwardBase
(`Shade4PointLights`), вместо этого рисуются попиксельно в цикле.
