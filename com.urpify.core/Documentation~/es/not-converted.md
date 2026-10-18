# Qué no se convierte y por qué

Urpify lista todo lo que no convierte, con el motivo, antes de que empiece la migración. Cada caso de abajo pertenece a
uno de tres grupos:

- **A mano: el proyecto ya estaba roto antes de la migración.** Urpify no arregla errores del propio proyecto; dice qué
  está roto y qué hacer primero.
- **A mano: no hay nada que convertir.** URP no tiene equivalente, comprobado en el código fuente de URP.
- **Aún no compatible.** Es posible, pero esta versión de Urpify no lo hace. Es en lo que trabajamos a continuación; el
  hallazgo dice qué hacer a mano mientras tanto.

La proporción de shaders que Urpify convierte, medida en una prueba independiente con shaders que sus desarrolladores
no vieron, está en la página del producto. Esta página enumera lo que no se convierte y por qué.

## El proyecto ya estaba roto antes de la migración

| Caso | Qué verás | Qué hacer |
|---|---|---|
| Un shader no compila en la pipeline integrada | «El shader ya no compila» | Arregla primero los errores de compilación en Built-in; los objetos ya son rosas. |
| Los scripts del proyecto no compilan | «Los scripts del proyecto no compilan» | Arréglalos primero: bloquean el Play Mode, las capturas y los pasos que reescriben scripts. Los proyectos antiguos suelen necesitar actualizar llamadas a API obsoletas. |
| Un shader incluye un archivo que no está en el proyecto | «Include no encontrado» | Añade el archivo o corrige la ruta y vuelve a analizar. |
| `UsePass` nombra un shader que no está en el proyecto | «UsePass ...: shader no encontrado» | Añade el shader o quita la línea `UsePass`. El objeto también es rosa en Built-in. |
| Un renderer usa un material que no está en el proyecto | «Falta el material en ... renderers» | Asigna un material o recupera el material o el paquete que falta. Ocurre cuando Unity actualiza un paquete (por ejemplo, ProBuilder) cuyo material por defecto antiguo ya no existe. |

## No hay nada que convertir

| Caso | Por qué |
|---|---|
| Shaders internos del renderer integrado: `Internal-DeferredShading`, `Internal-DeferredReflections`, `Internal-ScreenSpaceShadows`, `Internal-DepthNormalsTexture`, `Internal-MotionVectors`, `Internal-PrePassLighting`, `Internal-Flare`, `Internal-Halo` y sus copias | Built-in los ejecuta desde las ranuras de shaders integrados de Graphics Settings. URP nunca lee esas ranuras: el código de URP 14.0.12 y 17.5.0 nunca llama a `GraphicsSettings.GetCustomShader` ni usa `BuiltinShaderType`. URP renderiza la iluminación diferida, las sombras, depth-normals y los vectores de movimiento con sus propios shaders. |
| Pasadas con LightMode `Deferred`, `PrepassBase`, `PrepassFinal` | Los caminos diferido y diferido antiguo de Built-in. URP renderiza el objeto en su pasada forward, que Urpify construye a partir de las pasadas forward del shader. |
| Funciones `finalgbuffer:` de surface shaders | Solo se ejecutan en la pasada G-buffer diferida de Built-in; el shader forward convertido no las llama. |
| `Camera.depthTextureMode` | URP lo ignora. URP genera las texturas de profundidad y normales según los ajustes del URP Asset, y Urpify las activa cuando un shader las lee. |

## Aún no compatible

| Construcción | Qué pasa ahora | Qué hacer a mano |
|---|---|---|
| Efectos alimentados por `Light.AddCommandBuffer` (luz volumétrica y similares) | El efecto falta en URP; el resto del fotograma se renderiza. | Mueve los comandos de la luz a un Renderer Feature en `BeforeRenderingShadows` o `AfterRenderingShadows`; URP renderiza los shadow maps de otra forma, así que revisa qué esperan los comandos. |
| Efectos de imagen cuyo `OnRenderImage` llega a un renderizado de cámara, un dispatch de compute u otro dibujo inmediato | El efecto falta en URP; se dibuja el resto del fotograma. | Saca esa llamada del efecto: a `OnPreRender`, o a un command buffer que ejecute `ImageEffectGraphics.ExecuteCommandBuffer`. |
| Efectos de imagen cuyos shaders leen el G-buffer de Built-in (`_CameraGBufferTexture0-3`: reflejos en espacio de pantalla y similares) | El efecto falta en URP; se dibuja el resto del fotograma. | Reconstruye las entradas con las texturas de profundidad, normales y opaca de URP, o usa el renderer Deferred de URP con su G-buffer. |
| Delegados `Camera.onPreCull`, `Camera.onPreRender`, `Camera.onPostRender` | El código añadido a ellos deja de ejecutarse. | Muévelo a `RenderPipelineManager.beginCameraRendering` o `endCameraRendering`. |
| `OnRenderImage` que dibuja de inmediato (`Graphics.DrawMeshNow` y similares) | Falta el efecto. | Dibuja con un command buffer (`CommandBuffer.Blit`, `DrawMesh`) y ejecútalo con `ImageEffectGraphics.ExecuteCommandBuffer`. |
| `OnRenderImage` que renderiza una cámara (`Camera.Render`, también a través de un método auxiliar) | El efecto no se ejecuta en URP: dentro del fotograma de su propia cámara ninguna otra cámara puede renderizar, y el fotograma saldría negro. | Renderiza la otra cámara en `OnPreRender` u `OnPreCull` y deja en `OnRenderImage` solo las llamadas a Blit. Un Blit que prueba el stencil buffer de la cámara tampoco lo ve en URP. |
| Command buffers en los eventos de iluminación diferida de Built-in de una luz | Faltan. | Un Renderer Feature en el evento de URP correspondiente. |
| Shaders que leen el G-buffer de Built-in (`_CameraGBufferTexture0` a `3`), como la GI en espacio de pantalla | El efecto pierde su entrada sin ningún error: el renderer Forward de URP no tiene G-buffer, y el camino Deferred de URP empaqueta el suyo de otra forma (`_GBuffer0` a `_GBuffer6`). | Reconstruye las entradas con las texturas de profundidad, normales y opaca de URP, o cambia el renderer de URP a Deferred y reescribe las lecturas para `UnityGBuffer.hlsl`. |
| Varios GrabPass con texturas distintas en un shader | El shader no se convierte. | URP tiene una Opaque Texture: combina las capturas o deja una y léela dos veces. |
| Más de 32 pasadas dibujadas en un SubShader | No se convierte. | Combina pasadas o mueve algunas a su propio SubShader o material. |
| `UsePass` de un shader cuya pasada está en un bloque `CGINCLUDE` | No se convierte. | Copia la pasada en el shader. |
| Un include integrado para el que Urpify aún no tiene equivalente | «Stopped: ... includes ...», el shader no se convierte. | Porta a mano las funciones del include o envía el shader con Enviar informe. |
| Una pasada `ForwardAdd` que lee directamente algunas variables de luz de Built-in, ilumina por vértice o no devuelve un color | No se convierte. | Reescribe la pasada con `UNITY_LIGHT_ATTENUATION` o muévela al bucle de luces adicionales de URP. |
| Una función de iluminación con una firma que el conversor no conoce | No se convierte. | Usa una de las firmas `Lighting<Name>` documentadas de Built-in. |
| Pasadas de la pipeline de función fija (sin programa de shader) | No se convierten: URP solo dibuja pasadas con programa. | Reescribe la pasada como un programa vert/frag. |
| Un Material Variant cuyo archivo no está en la copia de seguridad antes del conversor de Unity | Sus valores propios se pierden en el conversor de Unity. | Migra con los pasos de Urpify, que guardan ese archivo, o vuelve a poner los valores. |
| Un shader o script convertido que no compila | Se restaura el original, con el mensaje del compilador en el hallazgo. | Corrige el resultado en el panel del shader y guárdalo con Guardar como, o envía el shader con Enviar informe. |

## Convertido, pero no idéntico

Esto se convierte y funciona, con una diferencia que es propia de URP, no de la conversión. Imágenes en
[En qué se ve distinto URP](urp-differences.md).

- **GrabPass con Opaque Texture** (por defecto): URP copia la pantalla antes de los objetos transparentes, así que no
  aparecen en la refracción. Elige el Origen de GrabPass «Tras los transparentes» para copiar después.
- **Funciones de iluminación propias y pasadas ForwardAdd:** la función del autor se ejecuta para la luz principal y cada
  luz adicional en una sola pasada, con las sombras y la atenuación de URP.
- **Bloom de Post-processing Stack v2:** pasa al bloom de URP, cuyo brillo es más suave y ancho; la pirámide de bloom de
  URP reparte de otra forma los puntos brillantes pequeños.
- **Standard a URP Lit** (la conversión propia de Unity): difiere hasta aproximadamente un 7 % del fotograma en nuestras
  escenas de prueba.
- **Reflejos en `OnWillRenderObject`** se llaman antes del culling de URP para los renderers que la cámara ve, sin
  occlusion culling, así que un reflejo detrás de una pared se renderiza igualmente.
