# Что делает конвертер Unity

Render Pipeline Converter от Unity переводит материалы на собственных встроенных шейдерах Unity. Urpify запускает его как
шаг миграции «Конвертер Unity», с обходными путями, которые нужны для надёжного запуска из скрипта, и показывает его лог.

![Стенд STD01: Standard metallic с картами нормалей и металличности](../../images/STD01.jpg)

*STD01: Standard, металлический процесс, карты нормалей и металличности. Конвертер Unity переводит его в URP Lit.*

![Стенд TER01: террейн с двумя слоями](../../images/TER01.jpg)

*TER01: террейн с двумя слоями переходит на шейдер террейна URP.*

Что покрывает конвертер, измерено на стендах STD01–STD06, LEG01–LEG05, PAR01, PAR02, FBX01, FBX02 и TER01:

- Standard и Standard (Specular setup) в режимах Opaque, Cutout и Fade, с картами нормалей, свечения и детализации.
  Исключение — режим Transparent, см. [Исправления после конвертера Unity](after-unity-converter.md).
- Legacy Shaders (Diffuse, Specular, Bumped, Reflective, Transparent, Cutout, Lightmapped, Parallax, VertexLit); исключение —
  Self-Illumin.
- Шейдеры Mobile, Unlit/Texture, Particles/Standard Surface и Unlit, устаревшие частицы.
- Nature/Terrain/Standard и материал террейна по умолчанию; SpeedTree.
- Материалы, встроенные в импортированные модели.
- Анимационные клипы, анимирующие свойства материалов Built-in (сканирование их перечисляет; результат Urpify пока не
  проверяет).
- Post-processing Stack v2, см. [Исправления после конвертера Unity](after-unity-converter.md).

Сканирование Urpify помечает такие материалы как исправляемые конвертером Unity, поэтому обзор считает их отдельно от того,
что требует Urpify.

## Надёжный запуск

На Unity 6.5 документированная пакетная команда Unity без `-typesFilter` бросает `NullReferenceException`, а редактор
часто падает при выходе после успешной работы (коды выхода 21, 139). Urpify вызывает конвертер через его API, судит о
результате по логу конвертера и повторному сканированию, а не по коду выхода, и включает лог в отчёт.
