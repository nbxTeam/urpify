# Configuración

Project Settings > **Urpify**, o el botón **Configuración** de la ventana de Urpify.

Los ajustes del equipo se guardan en `ProjectSettings/UrpifySettings.asset`: haz commit del archivo y todo el equipo
convertirá igual. Los ajustes personales se guardan en `UserSettings/Urpify.json` y se quedan en tu equipo.

## Conversión (equipo)

Estos ajustes deciden lo que escriben los conversores de Pro.

| Ajuste | Valores | Por defecto | Qué hace |
|---|---|---|---|
| Salida | Reemplazar el original, Escribir una copia, Mantener compatibilidad con Built-in | Reemplazar el original | **Reemplazar el original**: el archivo del shader pasa a ser solo de URP; el original queda en la copia de seguridad. **Escribir una copia**: el shader de URP se escribe junto al original como `Name_URP.shader` con el nombre `Name (URP)`, y los materiales pasan a usarlo; el original se queda. **Mantener compatibilidad con Built-in**: un archivo para ambas pipelines; los SubShaders originales se quedan para Built-in y delante se añade un SubShader de URP. |
| Origen de GrabPass | Opaque Texture, Tras los transparentes | Opaque Texture | **Opaque Texture**: un GrabPass convertido lee la copia de pantalla de URP tomada antes de los objetos transparentes, así que no aparecen en la refracción. **Tras los transparentes**: el Renderer Feature de Urpify copia la pantalla después de los objetos transparentes, como hacía GrabPass en Built-in, y dibuja después los objetos con GrabPass. Cuesta una copia de pantalla más por fotograma. |
| Insertar efectos de pantalla en | Tras el posprocesado, Antes del posprocesado, Tras los opacos | Tras el posprocesado | Dónde se ejecutan en el fotograma de URP los efectos `OnRenderImage` convertidos. Los efectos con `[ImageEffectOpaque]` siempre se ejecutan después del cielo, como en Built-in. |
| Sombras | Igual que el original, Siempre, Nunca | Igual que el original | Si los surface shaders convertidos proyectan y reciben sombras. |
| SRP Batcher | Activado, Desactivado | Activado | Coloca las propiedades del material en un buffer `UnityPerMaterial` para que el SRP Batcher de URP agrupe los shaders convertidos. Un shader que declara su propio `UnityPerMaterial` lo conserva; una propiedad que no se puede mover con seguridad se queda fuera, con una nota. |

## Comparación (equipo)

| Ajuste | Por defecto | Qué hace |
|---|---|---|
| Tamaño de captura | Vista Game | El tamaño de cada fotograma capturado: el de la vista Game, para que los efectos que dependen de la resolución se vean como los ves, o uno personalizado. La línea de comandos usa `-urpifyWidth` y `-urpifyHeight`. |
| Ancho de captura, Alto de captura | 960 × 540 | El tamaño con Tamaño de captura en Personalizado, y cuando no hay vista Game. |
| Fotogramas antes de capturar | 30 | Cuántos fotogramas se reproducen con paso de tiempo fijo antes de cada captura, para que las animaciones y las partículas estén en el mismo estado antes y después. |
| Escenas | Build Settings | Qué escenas usan Capturar antes y Capturar después: las de Build Settings, todas las escenas de `Assets` o una lista. |
| Rutas de escenas | vacío | Para Escenas seleccionadas: rutas separadas por punto y coma, por ejemplo `Assets/Scenes/Main.unity;Assets/Scenes/Menu.unity`. |

Los umbrales de «Rosa nuevo», «Cambió» e «Igual» están calibrados con la conversión propia de Unity de Standard a URP Lit
y valen para cualquier proyecto: una zona rosa nueva de más del 0,02 % del fotograma es «Rosa nuevo»; más píxeles
cambiados de los que produce Standard → URP Lit, o una zona grande cambiada, es «Cambió».

Cada escena puede tener **Migration Viewpoints**: añade el componente `MigrationViewpoint` a un GameObject vacío para
capturar desde su posición y dirección además de la cámara principal. Un viewpoint puede tener su propio campo de visión
y su propio plano de recorte lejano. Está marcado como EditorOnly y nunca llega a una build.

## Personal

| Ajuste | Valores | Por defecto | Qué hace |
|---|---|---|---|
| Idioma | Idioma del Editor, o uno de 8 idiomas | Idioma del Editor | Idioma de las ventanas de Urpify. «Idioma del Editor» sigue Preferences > Languages y usa inglés si ese idioma no está disponible. |
| Vista previa de Built-in | En vivo, Desde capturas | En vivo | **En vivo** apaga la render pipeline durante el render de la vista previa de Built-in y la vuelve a encender, en una llamada; nunca en Play Mode ni durante una importación o compilación. **Desde capturas** muestra en su lugar las capturas de las escenas. Si la vista previa en vivo falla o los ajustes de la pipeline no vuelven exactamente, Urpify cambia solo a Desde capturas y lo indica. |
| Marcas en la ventana Project | Activado, Desactivado | Activado | Una pequeña marca en los assets que el último análisis encontró rotos. |

## Cambios para una sola ejecución

La línea de comandos puede cambiar la Salida (`-urpifyShaderOutput`) y el Origen de GrabPass (`-urpifyGrabPass`) para una
ejecución, consulta [Línea de comandos](cli.md).
