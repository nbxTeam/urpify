# GrabPass

![Puesto G01: vidrio con refracción por GrabPass, desaparece tras el conversor de Unity, vuelve a refractar tras Urpify](../../images/G01.jpg)

*Puesto G01: vidrio con refracción y un `GrabPass {}` sin nombre. Solo tras el conversor de Unity, el vidrio desaparece,
sin ningún error.*

![Puesto G02: calima de calor con un GrabPass con nombre](../../images/G02.jpg)

*Puesto G02: calima de calor con un `GrabPass { "_HazeBackground" }` con nombre.*

## Qué se rompe

URP no tiene GrabPass. La pasada que captura la pantalla se omite, la textura queda vacía y el objeto desaparece o pierde
su refracción. Ni rosa ni error: una rotura silenciosa.

Hallazgo: «GrabPass (...)» (`SHADER.GRABPASS`), gravedad «Se rompe sin error».

## Qué hace Urpify (Pro)

El GrabPass se quita, y la textura que rellenaba (`_GrabTexture` o la que tiene nombre) lee en su lugar una copia de la
pantalla:

- **Origen de GrabPass = Opaque Texture** (por defecto): `_CameraOpaqueTexture` de URP, tomada tras los objetos opacos y
  el cielo, antes de los transparentes. Urpify activa Opaque Texture en cada URP Asset, sin reducir la resolución.
- **Origen de GrabPass = Tras los transparentes**: el Renderer Feature Built-in Camera Hooks de Urpify copia la pantalla
  tras los objetos transparentes en `_UrpifyGrabTexture` y después dibuja los objetos con GrabPass (etiqueta
  `UrpifyGrabPass`), como hacía GrabPass en Built-in. Una copia de pantalla más por fotograma.

Un GrabPass que va después de una o dos pasadas del mismo shader (niebla y luego un desenfoque que lee la captura) lee una
copia tomada tras esas pasadas, así que ve lo que veía la captura en Built-in.

## Cuánto se acerca

`Automático, aproximado` con Opaque Texture: los objetos transparentes detrás del vidrio no aparecen en la refracción. Con
Tras los transparentes, el resultado coincide con Built-in. Varios GrabPass con texturas distintas en un shader aún no son
compatibles: URP tiene una sola Opaque Texture.

En 2022.3, la autocomprobación de Urpify renderiza los shaders con GrabPass con una cámara de juego: URP 14 no copia la
Opaque Texture para una cámara de vista previa.
