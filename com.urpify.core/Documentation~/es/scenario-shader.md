# Convertir un shader

Escribes o mantienes shaders y quieres uno de ellos en URP, sin tocar la fórmula.

## Abrir el shader

Cualquiera de estas opciones abre el panel del shader:
- seleccionar el shader en la lista de la ventana de Urpify después de un análisis;
- clic derecho sobre el shader en la ventana Project > Urpify > Analyze;
- arrastrar un archivo `.shader` a la ventana de Urpify, desde la ventana Project o directamente desde el disco. Un
  archivo del disco se queda donde está; no se importa nada.

## Leer el código

La pestaña **Código** muestra el original a la izquierda y el shader convertido a la derecha. Las líneas cambiadas están
marcadas; pasa el cursor por una para ver por qué cambió: una pasada con una etiqueta nueva para URP, un include de
Built-in reemplazado, propiedades del material movidas a `UnityPerMaterial` para el SRP Batcher.

Qué cambia Urpify y qué deja en paz:
- El nombre del shader y los nombres de sus propiedades se quedan, así que los materiales siguen funcionando y conservan
  sus valores.
- Las funciones del autor (`surf`, `vert`, `frag`, funciones de iluminación, auxiliares) se copian tal cual. Solo cambia
  el cableado: cómo obtiene el shader las luces, sombras, profundidad, la imagen de la pantalla y las matrices.
- Los nombres de Built-in como `UnityObjectToClipPos`, `_LightColor0` o `UNITY_LIGHT_ATTENUATION` siguen funcionando a
  través de la biblioteca de compatibilidad de Urpify, `BirpCompat.hlsl`, que los traduce a URP.

**Editar resultado** te deja cambiar el código convertido y **Compilar**lo con las keywords que usan sus materiales; los
errores aparecen en sus líneas. **Guardar como...** escribe el resultado en un archivo nuevo; **Convertir** reemplaza el
original tras guardar una copia en `Urpify/Backup/ShaderPanel`.

Cuando el conversor no puede terminar, la columna derecha muestra un borrador de solo lectura con la línea donde se paró y
el motivo. Puedes guardar el borrador para terminarlo a mano; Convertir y la migración nunca lo usan.

## Vista previa

La pestaña **Vista previa** renderiza el shader en una esfera, un cubo, un plano o tu propia malla, en Built-in y en URP,
con los mismos valores de material en ambos. **Vista** cambia entre Built-in, URP, una cortina con deslizador y un mapa de
diferencias. **Dirección de la luz** gira la luz principal. Cambia una propiedad del material y ambas vistas previas la
siguen.

La vista previa de Built-in apaga la render pipeline durante el render y la vuelve a encender, en una sola llamada. Si eso
falla, el panel pasa a **Desde capturas** y muestra en su lugar las capturas de las escenas. Para un archivo ya convertido,
la mitad de Built-in renderiza el original desde la copia de seguridad de la migración.

## Comprobar el resultado

Cada shader que la migración convierte también se comprueba con un render: una esfera con los materiales del propio
proyecto para ese shader, en Built-in antes de la conversión y en URP después. Cuando difiere más del 25 % de la esfera, el
shader se convierte igualmente pero se marca como `Revisar`, y el par de imágenes se guarda en `Urpify/Reports/self-check/`.
Un shader cuyo código convertido no compila se deja como estaba y se informa.

## Cuando la conversión se detiene

El panel nombra la construcción que el conversor no admite y qué hacer. [Qué no se convierte](not-converted.md) las lista
todas. Si el shader debería convertirse y no lo hace, **Informe > Enviar informe...** empaqueta el shader, sus archivos
include y los hallazgos en un archivo: guárdalo o envíanoslo (solo cuando pulses Enviar).

## Licencia del shader

Cuando el archivo indica una licencia en su cabecera, el panel la muestra. Una copia convertida sigue bajo la misma
licencia; con una licencia no comercial no se puede vender.
