# Опции surface-шейдеров

`#pragma surface surf Standard <опции>` меняет то, что генерирует Unity. Urpify читает каждую опцию и сохраняет её смысл в
проходах, которые пишет (Pro).

![Стенд S03: Lambert с функцией vertex: ветра](../../images/S03.jpg)

*S03: `vertex:vert` гнёт меш, как ветер. Вершинная функция работает в каждом проходе, поэтому тени гнутся тоже.*

![Стенд S08: растворение с clip() и addshadow](../../images/S08.jpg)

*S08: растворение с `clip()` и `addshadow`: тень обрезается так же.*

![Стенд S10: decal:add](../../images/S10.jpg)

*S10: `decal:add`, аддитивный слой поверх непрозрачных объектов.*

![Стенд S11: туман по высоте в finalcolor:](../../images/S11.jpg)

*S11: туман по высоте в `finalcolor:`, применяется после освещения, как в Built-in.*

## Опции и что из них получается

| Опция | В URP |
|---|---|
| `vertex:func` | Работает в каждом сгенерированном проходе: forward, тени, глубина, глубина-нормали, meta. Дополнительные поля `Input`, которые она заполняет, передаются в surface-функцию. |
| `finalcolor:func` | Вызывается для освещённого цвета, до тумана. |
| `finalgbuffer:func` | Не используется: работает только в отложенном проходе G-buffer встроенного конвейера. Заметка об этом есть. |
| `alpha`, `alpha:blend`, `alpha:fade`, `alpha:premul` | Прозрачная очередь и соответствующее смешивание; `premul` сохраняет premultiplied specular. |
| `alphatest:prop` | `clip()` по свойству, в каждом проходе. |
| `keepalpha` | Альфа surface-функции записывается и в непрозрачных проходах. |
| `decal:add`, `decal:blend` | Рисуется после непрозрачных объектов с аддитивным или альфа-смешиванием, без теней. |
| `addshadow` | Проход теней выполняет вершинную функцию и обрезку surface, как forward-проход. |
| `noshadow` | Нет прохода теней; forward-проход не принимает тени. |
| `noambient`, `novertexlights`, `nolightmap`, `nofog`, `nometa`, `noforwardadd` | Соответствующая часть убирается: рассеянный свет, повершинные источники, лайтмапы, туман, проход Meta, дополнительные источники. |
| `halfasview`, `interpolateview`, `approxview`, `dualforward` | Переключатели качества направления взгляда, URP они не нужны; игнорируются с заметкой. |
| `fullforwardshadows`, `nodynlightmap`, `nodirlightmap`, `nolppv`, `noshadowmask`, `noinstancing`, `exclude_path:*`, `dithercrossfade` | Распознаются; это решают собственные настройки URP (тени дополнительных источников, режимы лайтмапов, light probe proxy volume, LOD cross-fade), поэтому в переведённом шейдере ничего не меняют. |
| `tessellate:func`, `tessphong:` | См. [Тесселяция](surface-tessellation.md). |

Опция, которую Urpify не знает, перечисляется в находке «Неподдерживаемые опции surface» (`SHADER.SURFACE.UNKNOWN_OPTION`);
остальной шейдер всё равно переводится.

Настройка «Тени» (Project Settings > Urpify) может принудительно включить или выключить тени у всех переведённых
surface-шейдеров.
