# Версии Unity и URP

| Unity | URP | Статус | Проверено на |
|---|---|---|---|
| 6000.6 | 17.6 | Поддерживается | 6000.6.5f1 с URP 17.6.0: юнит-тесты, 37 стендов, демо, 4 реальных проекта |
| 6000.5 | 17.5 | Поддерживается | 6000.5.9f1 с URP 17.5.0: юнит-тесты, 37 стендов, реальные проекты, корпус шейдеров |
| 6000.4 | 17.4 | Не проверено | |
| 6000.3 LTS | 17.3 | Поддерживается | 6000.3.26f1 с URP 17.3.0: юнит-тесты, 37 стендов, демо, 4 реальных проекта. Пакетный конвертер самого Unity на этой версии не работает; Urpify обходит это (ниже) |
| 6000.1, 6000.2 | 17.1, 17.2 | Не проверено | |
| 6000.0 LTS | 17.0 | Поддерживается | 6000.0.84f1 с URP 17.0.4: юнит-тесты, 37 стендов, демо, 4 реальных проекта |
| 2022.3 LTS | 14 | Поддерживается | 2022.3.62f3 с URP 14.0.12: тот же набор, что для 6000.5 |
| 2021.3 LTS | 12 | Не поддерживается в 1.0 | Планируется по запросу |

Переводите проект в той версии редактора, с которой будете выпускаться: снимки «до» и «после» сравнимы только в пределах
одной версии Unity.

## Конвертер Unity и код выхода на 6000.0 и 6000.3

- В URP 17.0 и 17.3 у конвертера Unity старый пакетный API (`ConverterContainerId`). В URP 17.3.0 (6000.3.26f1) он в любом проекте
  бросает `MissingMethodException` для абстрактного `Base2DMaterialUpgrader`, ещё ничего не переведя. Тогда Urpify сам составляет
  список конвертеров Built-in → URP и запускает их так же; об этом сказано в «Подробностях» шага.
- Unity 6000.0.84f1 и 6000.3.26f1 часто, а 6000.5.9f1 изредка падают при выходе из пакетного режима (код 139) уже после того, как
  работа сделана, с Urpify и без него. В командной строке читайте `Urpify/urpify-<команда>-result.txt` (см. [Командная строка](cli.md)).

## Чем отличаются URP 14 и URP 17

Библиотека шейдеров Urpify (`BirpCompat*.hlsl`) и её Renderer Feature учитывают каждое отличие ниже, поэтому переведённый
шейдер компилируется и рисуется в обеих версиях. Каждая строка проверена по исходникам пакетов URP 14.0.12 (2022.3.62f3) и
URP 17.5.0 (6000.5.9f1).

| Тема | URP 14.0.12 | URP 17.5.0 | Где проверено |
|---|---|---|---|
| Основной свет | `GetMainLight()`, `(shadowCoord)`, `(shadowCoord, positionWS, shadowMask)`, `(inputData, shadowMask, aoFactor)` | Те же 4 перегрузки | `ShaderLibrary/RealtimeLights.hlsl` |
| Дополнительные источники | `GetAdditionalLight(i, positionWS)`, `(i, positionWS, shadowMask)`, `(i, inputData, shadowMask, aoFactor)` | Те же 3 перегрузки | `ShaderLibrary/RealtimeLights.hlsl` |
| Цикл источников Forward+ | `USE_FORWARD_PLUS` | `USE_CLUSTER_LIGHT_LOOP`; `USE_FORWARD_PLUS` остаётся псевдонимом в `ForwardPlusKeyword.deprecated.hlsl` | `ShaderLibrary/RealtimeLights.hlsl`, `Core.hlsl` |
| Тени дополнительных источников | `AdditionalLightRealtimeShadow(lightIndex, positionWS, lightDirection)` | То же плюс перегрузка с параметрами теней и данными выборки | `ShaderLibrary/Shadows.hlsl` |
| Сферические гармоники | `SampleSH(normalWS)` в `GlobalIllumination.hlsl` пакета URP | `SampleSH(normalWS)` в `AmbientProbe.hlsl` пакета Core | `ShaderLibrary` URP и Core |
| Лайтмапы | `LIGHTMAP_ON`, `DIRLIGHTMAP_COMBINED` | Те же ключевые слова | `ShaderLibrary/GlobalIllumination.hlsl` |
| LOD cross-fade | `LODCrossFade.hlsl` | `LODCrossFade.hlsl` | `ShaderLibrary/` |
| Декали | `DBuffer.hlsl` | `DBuffer.hlsl` | `ShaderLibrary/` |
| Текстуры глубины, нормалей, непрозрачных | `DeclareDepthTexture.hlsl`, `DeclareNormalsTexture.hlsl`, `DeclareOpaqueTexture.hlsl` | Те же файлы | `ShaderLibrary/` |
| Renderer Feature | `ScriptableRenderPass.Execute`; Render Graph внутренний | Render Graph (`RecordRenderGraph`); с Unity 6.4 режима совместимости нет: `RenderGraphSettings.enableRenderCompatibilityMode` всегда возвращает false | `Runtime/Passes/ScriptableRenderPass.cs`, `Runtime/Settings/RenderGraphSettings.cs` |
| Opaque Texture для превью-камер | Для превью-камеры не копируется (`copyColorPass &= !isPreviewCamera`) | Копируется | `Runtime/UniversalRenderer.cs`. Поэтому самопроверка Urpify на 2022.3 рисует шейдеры с GrabPass игровой камерой. |
| Текстуры URP Asset | Собранная игра бросает `NullReferenceException` каждый кадр и рисует чёрный экран, когда `m_Textures` URP Asset (blue noise, Bayer) пусты, как их сохраняет конвертер Unity | Не наблюдалось в играх на 6000.5 из тех же проектов | `UniversalRenderPipeline.cs` (`SetupPerFrameShaderConstants`). Бесплатный пакет заполняет их после конвертера Unity. |

## Чем отличается конвертер Unity

| Тема | 2022.3 / URP 14 | 6000.5 / URP 17.5 |
|---|---|---|
| Правила перевода материалов для `Legacy Shaders/Particles/VertexLit Blended` и `Mobile/Bumped Specular (1 Directional Realtime Light)` | Записаны под старыми именами шейдеров, поэтому конвертер Unity их пропускает; Urpify переводит эти материалы по своей таблице | Unity переводит их сам |
| Пакетный режим без `-typesFilter` | Не измерялось: Urpify и там не использует пакетную команду Unity | Бросает `NullReferenceException` (`Converters.cs:134`); Urpify вызывает конвертер через его API |
| Код выхода пакетной команды Unity после успешной работы | Не измерялось | 3 из 4 реальных проектов завершились с кодом 21 или 139 (процесс падает при выходе); Urpify судит о результате по логу конвертера и повторному сканированию, на обеих версиях |
