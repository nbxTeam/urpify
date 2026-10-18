# Конструкции

Urpify поддерживает конструкции, а не конкретные шейдеры: шейдер переводится, если поддержана каждая конструкция, которую
он использует; иначе сканирование называет недостающую. Каждая страница ниже объясняет одну конструкцию встроенного
конвейера: что ломается в URP, что с этим делает Urpify и насколько близок результат.

Картинки — с тестового стенда Urpify, сцены с отдельным стендом на каждую конструкцию. На каждой один и тот же стенд
трижды: в Built-in, в URP только после Render Pipeline Converter от Unity и в URP после конвертера Unity и Urpify. Это
кадры, которые сняли собственные шаги снимков Urpify, обрезанные до стенда.

| Конструкция | Страница | Стенд | Кто исправляет |
|---|---|---|---|
| Surface-шейдеры со встроенными моделями освещения | [Surface-шейдеры](surface-shaders.md) | S01–S04 | Pro |
| Свои функции освещения | [Своё освещение](surface-custom-lighting.md) | S05 | Pro |
| Опции `#pragma surface`: `vertex:`, `finalcolor:`, `decal:`, `alphatest:`, `addshadow` и другие | [Опции surface](surface-options.md) | S03, S06–S08, S10, S11 | Pro |
| Тесселяция surface-шейдеров | [Тесселяция](surface-tessellation.md) | S09 | Pro |
| Дополнительные проходы: обводки, слои меха, 3 и больше проходов | [Дополнительные проходы](extra-passes.md) | S12 | Pro |
| Неосвещённые vert/frag-шейдеры | [Неосвещённые vert/frag](vertfrag-unlit.md) | V01 | Делать нечего |
| Освещённые vert/frag-шейдеры: ForwardBase, ForwardAdd, тени, vertex-lit | [Освещённые vert/frag](vertfrag-lit.md) | V02 | Pro |
| GrabPass | [GrabPass](grabpass.md) | G01, G02 | Pro |
| `_CameraDepthTexture` | [Текстура глубины](depth-texture.md) | D01 | Free и Pro |
| `_CameraDepthNormalsTexture`, replacement-шейдеры | [Глубина-нормали и replacement-шейдеры](depth-normals.md) | нет | Pro |
| Экранные эффекты `OnRenderImage` | [Экранные эффекты](image-effects.md) | C02 | Pro |
| `CommandBuffer` на событиях камеры | [Командные буферы](command-buffers.md) | C03 | Pro |
| Скрипты, рендерящие камеры: зеркала, порталы, отражения воды | [Скрипты камер](camera-scripts.md) | M01 | Pro |
| Компоненты `Projector` | [Проекторы](projectors.md) | C01 | Pro |
| `Shader.Find` с именами встроенных шейдеров | [Shader.Find в коде](shader-find.md) | C04 | Free |
| Что ломает конвертер Unity: Standard Transparent, Self-Illumin, встроенные материалы, Post-processing v2, текстуры URP Asset, Material Variants | [Исправления после конвертера Unity](after-unity-converter.md) | STD04, LEG03 | Free |
| Что делает конвертер Unity: Standard, Legacy, Mobile, частицы, террейн, импортированные модели | [Конвертер Unity](unity-converter.md) | STD01–STD06, LEG01–LEG05, PAR01, PAR02, FBX01, FBX02, TER01 | Unity |
| Деревья Tree Creator и soft occlusion | [Деревья](trees.md) | нет | Pro |
| Один файл шейдера для Built-in и URP | [Сохранить поддержку Built-in](keep-builtin-support.md) | все стенды с шейдерами | Pro |

Конструкции, которые Urpify не переводит, перечислены с причинами в [Что не переводится](../not-converted.md).
