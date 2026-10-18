# Construcciones

Urpify admite construcciones, no shaders concretos: un shader se convierte cuando todas las construcciones que usa están
admitidas; si no, el análisis nombra la que falta. Cada página de abajo explica una construcción de la Built-in Render
Pipeline: qué se rompe en URP, qué hace Urpify al respecto y cuánto se acerca el resultado.

Las imágenes vienen del laboratorio de pruebas de Urpify, una escena con un puesto propio por construcción. Cada una
muestra el mismo puesto tres veces: en Built-in, en URP solo tras el Render Pipeline Converter de Unity, y en URP tras el
conversor de Unity y Urpify. Son los fotogramas que tomaron los propios pasos de captura de Urpify, recortados al puesto.

| Construcción | Página | Puesto | Quién lo arregla |
|---|---|---|---|
| Surface shaders con modelos de iluminación integrados | [Surface shaders](surface-shaders.md) | S01–S04 | Pro |
| Funciones de iluminación propias | [Iluminación propia](surface-custom-lighting.md) | S05 | Pro |
| Opciones de `#pragma surface`: `vertex:`, `finalcolor:`, `decal:`, `alphatest:`, `addshadow` y otras | [Opciones de surface](surface-options.md) | S03, S06–S08, S10, S11 | Pro |
| Teselación de surface shaders | [Teselación](surface-tessellation.md) | S09 | Pro |
| Pasadas extra: contornos, capas de pelo, 3 o más pasadas | [Pasadas extra](extra-passes.md) | S12 | Pro |
| Shaders vert/frag sin iluminación | [Vert/frag sin iluminación](vertfrag-unlit.md) | V01 | Nada que hacer |
| Shaders vert/frag iluminados: ForwardBase, ForwardAdd, sombras, vertex-lit | [Vert/frag iluminados](vertfrag-lit.md) | V02 | Pro |
| GrabPass | [GrabPass](grabpass.md) | G01, G02 | Pro |
| `_CameraDepthTexture` | [Textura de profundidad](depth-texture.md) | D01 | Free y Pro |
| `_CameraDepthNormalsTexture`, shaders de reemplazo | [Depth-normals y shaders de reemplazo](depth-normals.md) | ninguno | Pro |
| Efectos de imagen `OnRenderImage` | [Efectos de imagen](image-effects.md) | C02 | Pro |
| `CommandBuffer` en eventos de cámara | [Command buffers](command-buffers.md) | C03 | Pro |
| Scripts que renderizan cámaras: espejos, portales, reflejos del agua | [Scripts de cámara](camera-scripts.md) | M01 | Pro |
| Componentes `Projector` | [Proyectores](projectors.md) | C01 | Pro |
| `Shader.Find` con nombres de shaders integrados | [Shader.Find en código](shader-find.md) | C04 | Free |
| Lo que rompe el conversor de Unity: Standard Transparent, Self-Illumin, materiales integrados, materiales de paquetes, Post-processing v2, texturas del URP Asset, Material Variants | [Correcciones tras el conversor de Unity](after-unity-converter.md) | STD04, LEG03 | Free |
| Lo que hace el conversor de Unity: Standard, Legacy, Mobile, partículas, terreno, modelos importados | [El conversor de Unity](unity-converter.md) | STD01–STD06, LEG01–LEG05, PAR01, PAR02, FBX01, FBX02, TER01 | Unity |
| Árboles de Tree Creator y de soft occlusion | [Árboles](trees.md) | ninguno | Pro |
| Un archivo de shader para Built-in y URP | [Mantener compatibilidad con Built-in](keep-builtin-support.md) | todos los puestos con shaders | Pro |

Las construcciones que Urpify no convierte están, con sus motivos, en [Qué no se convierte](../not-converted.md).
