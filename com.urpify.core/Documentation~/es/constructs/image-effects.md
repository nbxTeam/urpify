# Efectos de imagen: OnRenderImage

![Puesto C02: un efecto de imagen de desaturación con viñeta, desaparece tras el conversor de Unity, vuelve tras Urpify](../../images/C02.jpg)

*Puesto C02: un efecto de posprocesado con `OnRenderImage`, desaturación con viñeta. Solo tras el conversor de Unity, el
efecto desaparece, sin ningún error.*

## Qué se rompe

URP nunca llama a `OnRenderImage`, así que todo efecto de imagen escrito para Built-in deja de funcionar. Ni error ni rosa:
el juego simplemente se ve distinto. Comprobado en URP 14 y 17.

Hallazgo: «OnRenderImage ({0} llamadas a Blit)» (`SCRIPT.ONRENDERIMAGE`), gravedad «Se rompe sin error». El shader del
efecto recibe el hallazgo «Shader de efecto de imagen».

## Qué hace Urpify (Pro)

El script sigue siendo un MonoBehaviour en la cámara y sigue haciendo de mando: sus campos, los valores que establece en
cada fotograma y sus materiales no cambian. El Renderer Feature Built-in Camera Hooks de Urpify llama al `OnRenderImage`
del script en el punto del fotograma de URP elegido en la configuración («Insertar efectos de pantalla en»: por defecto,
tras el posprocesado), con el color de la cámara como origen. Las llamadas a `Graphics.Blit` del script pasan por
`ImageEffectGraphics`, que las graba en el fotograma de URP con los mismos materiales, pasadas y render textures
temporales, en el mismo orden. Los efectos con `[ImageEffectOpaque]` se ejecutan tras el cielo, como en Built-in. Varios
efectos en una cámara conservan su orden. El dibujo con `GL` dentro de `OnRenderImage` (quads, matrices, borrados) también
se graba.

El shader del efecto se queda tal cual: `vert_img`, `v2f_img` y `_MainTex` funcionan, porque el efecto sigue pasando la
pantalla en `_MainTex`, como en Built-in.

## Cuánto se acerca

`Auto` para un solo Blit, `Automático, aproximado` para cadenas y command buffers. Un efecto que dibuja de inmediato con
`Graphics.DrawMeshNow` o similares, o que renderiza una cámara (`Camera.Render`), no se puede grabar en el fotograma de
URP: el hallazgo dice cómo reorganizarlo.

Las llamadas a `Graphics.Blit` de otros scripts que el efecto llama (un efecto ScriptableObject detrás del script de la
cámara, un gestor que ejecuta una cadena) se graban del mismo modo. Un efecto cuya cadena renderiza una cámara, lanza un
compute shader o lee el G-buffer de Built-in (`_CameraGBufferTexture0-3`) queda como está (aún no compatible): en URP
falta y se dibuja el resto del fotograma. Si el fotograma de una escena queda casi de un solo color tras la migración, los
efectos de esa escena se marcan para revisar: desactívalos en la cámara para encontrar la causa, o revierte.

Los juegos compilados conservan el efecto: todo lo que Urpify llama por nombre está protegido contra el stripping de
código, comprobado en builds Mono e IL2CPP.
