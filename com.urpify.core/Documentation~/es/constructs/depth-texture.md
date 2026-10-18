# Textura de profundidad

![Puesto D01: agua con espuma en las intersecciones, blanca tras el conversor de Unity, como en Built-in tras Urpify](../../images/D01.jpg)

*Puesto D01: agua transparente que lee `_CameraDepthTexture` para el color de profundidad y la espuma alrededor de una
roca. Solo tras el conversor de Unity, el agua se vuelve blanca.*

## Qué se rompe

Un shader que lee `_CameraDepthTexture` recibe profundidad solo cuando Depth Texture está activada en el URP Asset. El
conversor de Unity solo la activa en los niveles de calidad que tenían Soft Particles. Un shader transparente tiene un
segundo problema: URP copia la profundidad por defecto tras los objetos transparentes, y el conversor de Unity lo deja
así. En Unity 6 ese shader no recibe profundidad en absoluto y el agua se vuelve blanca; en 2022.3 lee la profundidad del
fotograma anterior, así que los fotogramas quietos se ven bien pero los objetos en movimiento dejan rastro.

Hallazgos: «Lee _CameraDepthTexture» (`SHADER.DEPTH_TEXTURE`), «Transparente, lee ...» (`SHADER.DEPTH_TEXTURE_TRANSPARENT`).

## Qué hace Urpify

- **Free y Pro:** activa Depth Texture en cada URP Asset y, para un lector transparente, pone el Depth Texture Mode del
  renderer en After Opaques. Un shader que URP dibuja tal cual funciona entonces sin cambiar el código: el puesto D01 lo
  arregla solo el paquete gratuito.
- **Pro, cuando el shader se convierte:** la declaración `sampler2D _CameraDepthTexture` del autor se reemplaza por
  `DeclareDepthTexture.hlsl` de URP, y las lecturas (`tex2D`, `tex2Dproj`, `SAMPLE_DEPTH_TEXTURE`,
  `SAMPLE_DEPTH_TEXTURE_PROJ`) pasan a `SampleSceneDepth`. Así el shader también funciona con XR single-pass instanced,
  donde la textura de profundidad es un array. `LinearEyeDepth`, `Linear01Depth` y `COMPUTE_EYEDEPTH` vienen de la
  biblioteca de Urpify.

## Cuánto se acerca

`Auto`. Si una lectura no se puede reescribir, la declaración del autor se queda y el hallazgo dice que XR single-pass
instanced no es compatible con ese shader.
