# Qué hace el conversor de Unity

El Render Pipeline Converter de Unity actualiza los materiales que usan los shaders integrados de Unity. Urpify lo ejecuta
como el paso Ejecutar el conversor de Unity de la migración, con los arreglos que necesita para funcionar de forma fiable
desde un script, e informa de su log.

![Puesto STD01: Standard metálico con normal map y metallic map](../../images/STD01.jpg)

*STD01: Standard, flujo metálico, normal map y metallic map. El conversor de Unity lo actualiza a URP Lit.*

![Puesto TER01: un terreno con dos capas](../../images/TER01.jpg)

*TER01: un terreno con dos capas pasa al shader de terreno de URP.*

Lo que cubre el conversor, medido en los puestos STD01 a STD06, LEG01 a LEG05, PAR01, PAR02, FBX01, FBX02 y TER01:

- Standard y Standard (Specular setup) en los modos Opaque, Cutout y Fade, con normal maps, emisión y mapas de detalle.
  El modo Transparent es la excepción, consulta [Correcciones tras el conversor de Unity](after-unity-converter.md).
- Legacy Shaders (Diffuse, Specular, Bumped, Reflective, Transparent, Cutout, Lightmapped, Parallax, VertexLit); la
  excepción es Self-Illumin.
- Shaders Mobile, Unlit/Texture, Particles/Standard Surface y Unlit, partículas antiguas.
- Nature/Terrain/Standard y el material de terreno por defecto; SpeedTree.
- Materiales incrustados en modelos importados.
- Clips de animación que animan propiedades de materiales de Built-in (el análisis los lista; Urpify aún no comprueba el
  resultado).
- Post-processing Stack v2, consulta [Correcciones tras el conversor de Unity](after-unity-converter.md).

El análisis de Urpify lista estos materiales como arreglados por el conversor de Unity, así que el resumen los cuenta
aparte de lo que necesita Urpify.

## Ejecutarlo de forma fiable

En Unity 6.5, el comando batch documentado de Unity lanza `NullReferenceException` sin `-typesFilter`, y el Editor suele
fallar al salir tras un trabajo correcto (códigos de salida 21, 139). Urpify llama al conversor mediante su API, juzga el
resultado por el log del conversor y por un nuevo análisis en lugar de por el código de salida, e incluye el log en el
informe.
