# Versiones de Unity y URP

| Unity | URP | Estado | Comprobado con |
|---|---|---|---|
| 6000.6 | 17.6 | Compatible | 6000.6.5f1 con URP 17.6.0: pruebas unitarias, 37 puestos, la demostración, 4 proyectos reales |
| 6000.5 | 17.5 | Compatible | 6000.5.9f1 con URP 17.5.0: pruebas unitarias, 37 puestos, proyectos reales, corpus de shaders |
| 6000.4 | 17.4 | No probado | |
| 6000.3 LTS | 17.3 | Compatible | 6000.3.26f1 con URP 17.3.0: pruebas unitarias, 37 puestos, la demostración, 4 proyectos reales. El conversor por lotes de Unity falla en esta versión; Urpify lo evita (abajo) |
| 6000.1, 6000.2 | 17.1, 17.2 | No probado | |
| 6000.0 LTS | 17.0 | Compatible | 6000.0.84f1 con URP 17.0.4: pruebas unitarias, 37 puestos, la demostración, 4 proyectos reales |
| 2022.3 LTS | 14 | Compatible | 2022.3.62f3 con URP 14.0.12: el mismo conjunto que para 6000.5 |
| 2021.3 LTS | 12 | No compatible en 1.0 | Previsto si se pide |

Migra con la versión del Editor con la que vas a publicar: las capturas de antes y después solo son comparables dentro de
una misma versión de Unity.

## El conversor de Unity y el código de salida en 6000.0 y 6000.3

- URP 17.0 y 17.3 tienen la API por lotes antigua del conversor de Unity (`ConverterContainerId`). En URP 17.3.0 (6000.3.26f1)
  lanza `MissingMethodException` para el abstracto `Base2DMaterialUpgrader` antes de convertir nada, en cualquier proyecto. Urpify
  entonces lista él mismo los conversores de Built-in a URP y los ejecuta igual; los Detalles del paso lo indican.
- Unity 6000.0.84f1 y 6000.3.26f1 a menudo, y 6000.5.9f1 a veces, fallan al salir del modo por lotes (código de salida 139) cuando el
  trabajo ya está hecho, con o sin Urpify. En la línea de comandos, lee `Urpify/urpify-<command>-result.txt` (mira [Línea de comandos](cli.md)).

## Qué cambia entre URP 14 y URP 17

La biblioteca de shaders de Urpify (`BirpCompat*.hlsl`) y su Renderer Feature tienen en cuenta cada diferencia de abajo,
así que un shader convertido compila y se renderiza en ambas versiones. Cada fila está comprobada con el código de los
paquetes URP 14.0.12 (2022.3.62f3) y URP 17.5.0 (6000.5.9f1).

| Tema | URP 14.0.12 | URP 17.5.0 | Dónde se comprobó |
|---|---|---|---|
| Luz principal | `GetMainLight()`, `(shadowCoord)`, `(shadowCoord, positionWS, shadowMask)`, `(inputData, shadowMask, aoFactor)` | Las mismas 4 sobrecargas | `ShaderLibrary/RealtimeLights.hlsl` |
| Luces adicionales | `GetAdditionalLight(i, positionWS)`, `(i, positionWS, shadowMask)`, `(i, inputData, shadowMask, aoFactor)` | Las mismas 3 sobrecargas | `ShaderLibrary/RealtimeLights.hlsl` |
| Bucle de luces Forward+ | `USE_FORWARD_PLUS` | `USE_CLUSTER_LIGHT_LOOP`; `USE_FORWARD_PLUS` sigue como alias en `ForwardPlusKeyword.deprecated.hlsl` | `ShaderLibrary/RealtimeLights.hlsl`, `Core.hlsl` |
| Sombras de luces adicionales | `AdditionalLightRealtimeShadow(lightIndex, positionWS, lightDirection)` | Lo mismo más una sobrecarga con parámetros de sombra y datos de muestreo | `ShaderLibrary/Shadows.hlsl` |
| Armónicos esféricos | `SampleSH(normalWS)` en `GlobalIllumination.hlsl` del paquete URP | `SampleSH(normalWS)` en `AmbientProbe.hlsl` del paquete Core | `ShaderLibrary` de URP y Core |
| Lightmaps | `LIGHTMAP_ON`, `DIRLIGHTMAP_COMBINED` | Las mismas keywords | `ShaderLibrary/GlobalIllumination.hlsl` |
| Fundido LOD | `LODCrossFade.hlsl` | `LODCrossFade.hlsl` | `ShaderLibrary/` |
| Decals | `DBuffer.hlsl` | `DBuffer.hlsl` | `ShaderLibrary/` |
| Texturas de profundidad, normales y opaca | `DeclareDepthTexture.hlsl`, `DeclareNormalsTexture.hlsl`, `DeclareOpaqueTexture.hlsl` | Los mismos archivos | `ShaderLibrary/` |
| Renderer Feature | `ScriptableRenderPass.Execute`; Render Graph interno | Render Graph (`RecordRenderGraph`); desde Unity 6.4 no hay modo de compatibilidad: `RenderGraphSettings.enableRenderCompatibilityMode` siempre devuelve false | `Runtime/Passes/ScriptableRenderPass.cs`, `Runtime/Settings/RenderGraphSettings.cs` |
| Opaque Texture para cámaras de vista previa | No se copia para una cámara de vista previa (`copyColorPass &= !isPreviewCamera`) | Se copia | `Runtime/UniversalRenderer.cs`. Por eso la autocomprobación de Urpify en 2022.3 renderiza los shaders con GrabPass con una cámara de juego. |
| Texturas del URP Asset | Un juego compilado lanza `NullReferenceException` en cada fotograma y se ve negro cuando las `m_Textures` del URP Asset (blue noise, Bayer) están vacías, como las guarda el conversor de Unity | No se observó en juegos de 6000.5 de los mismos proyectos | `UniversalRenderPipeline.cs` (`SetupPerFrameShaderConstants`). El paquete gratuito las rellena después del conversor de Unity. |

## Qué cambia en el conversor de Unity

| Tema | 2022.3 / URP 14 | 6000.5 / URP 17.5 |
|---|---|---|
| Reglas de materiales para `Legacy Shaders/Particles/VertexLit Blended` y `Mobile/Bumped Specular (1 Directional Realtime Light)` | Registradas con nombres de shader antiguos, así que el conversor de Unity se las salta; Urpify actualiza esos materiales con su propia tabla | Unity los actualiza por sí mismo |
| Modo batch sin `-typesFilter` | No medido: Urpify tampoco usa ahí el comando batch de Unity | Lanza `NullReferenceException` (`Converters.cs:134`); Urpify llama al conversor mediante su API |
| Código de salida del comando batch de Unity tras un trabajo correcto | No medido | 3 de 4 proyectos reales terminaron con el código 21 o 139 (el proceso falla al salir); Urpify juzga el resultado por el log del conversor y un nuevo análisis, en ambas versiones |
