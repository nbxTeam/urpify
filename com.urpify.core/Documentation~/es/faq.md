# Preguntas frecuentes

## ¿Urpify cambia mi proyecto al analizarlo?

No. El análisis solo lee archivos. Los cambios los hacen únicamente los pasos de la ventana de migración que lo indican y
el botón Convertir del panel del shader, siempre con una copia de seguridad antes.

## ¿Qué parte de mi proyecto se convertirá?

El análisis lo dice antes de que cambie nada: el resumen cuenta los assets rotos según quién los arregla.
La página del producto indica qué convirtió Urpify en una prueba independiente con shaders que sus desarrolladores no
vieron.

## ¿Puedo deshacer una migración?

Sí. **Revertir todo** restaura cada archivo que la migración cambió y mueve a la copia de seguridad los archivos que creó.
Los archivos que cambiaste después de la migración y los que aparecieron entre pasos se quedan como están y se listan. Un
paso interrumpido se revierte igual que uno terminado. Aun así, haz commit del proyecto antes de empezar: es en el commit
en lo que más confías.

## ¿Necesito Pro?

El paquete gratuito encuentra todo lo que se rompe, ejecuta el conversor de Unity de forma fiable, arregla lo que el
conversor de Unity hace mal, compara escenas antes y después, y revierte. Pro convierte lo que el paquete gratuito no
puede: shaders propios, efectos de imagen, scripts de cámara, command buffers y proyectores. El resumen muestra cuántos de
tus assets rotos necesitan Pro antes de comprarlo.

## ¿Urpify reescribe el código de mi shader?

Solo el cableado. Las funciones del autor (`surf`, `vert`, `frag`, funciones de iluminación, auxiliares) se copian tal
cual; lo que cambia es cómo obtiene el shader las luces, sombras, profundidad, la imagen de la pantalla y las matrices.
Los nombres de Built-in siguen funcionando gracias a la biblioteca de compatibilidad de Urpify. El nombre del shader y los
nombres de las propiedades se quedan, así que los materiales conservan sus valores.

## ¿Y si un shader convertido se ve mal?

Cada shader convertido se renderiza en Built-in y en URP sobre una esfera con tus materiales. Si difiere, se marca como
`Revisar` y el par de imágenes queda en `Urpify/Reports/self-check/`. Ábrelo en el panel del shader: la pestaña Vista
previa muestra Built-in y URP uno al lado del otro, la pestaña Código cada cambio con su motivo. Ahí puedes editar el
resultado y recompilarlo, o revertir el shader con la migración.

## ¿Funciona con mi versión de Unity?

Probado con Unity 2022.3 LTS, 6000.0 LTS, 6000.3 LTS, 6000.5 y 6000.6; 6000.1, 6000.2 y 6000.4 no están probados. Consulta [Versiones de Unity y URP](urp-version-matrix.md).

## ¿Funciona en CI?

Sí. Cada paso se ejecuta desde la línea de comandos y escribe un resultado JSON, consulta [Línea de comandos](cli.md).

## ¿Mantener compatibilidad con Built-in hace las builds más grandes?

Un poco. En el banco de pruebas (Unity 6.5, Mono) los datos del juego ocupaban 32,5 MB en Built-in, 38,2 MB en URP con
shaders solo de URP y 38,5 MB con shaders para ambas pipelines: 0,34 MB, o un 0,9 %, más.

## ¿Los efectos de imagen y proyectores convertidos cuestan más en URP?

Se ejecutan a través del Renderer Feature de Urpify, que graba las mismas llamadas de dibujo en el fotograma de URP. Un
proyector cuesta unos 0,02 ms por fotograma en una escena pequeña; con 5000 renderers visibles, unos 2,2 ms. Los juegos
compilados conservan los efectos, en builds Mono e IL2CPP.

## ¿Qué envía Enviar informe?

Nada hasta que pulses Enviar. Informe > Enviar informe... empaqueta el shader seleccionado, sus archivos include, los
hallazgos, las versiones de Unity, URP y Urpify, los errores del compilador y, si los das, tu comentario y tu contacto. La
ventana lista cada archivo antes de enviar. **Enviar** sube el archivo a nuestro canal de soporte; **Guardar zip** lo guarda
en tu equipo. Los archivos solo se usan para entender qué le falta al conversor y después se borran. Es la única llamada
de red de Urpify. Como máximo un informe por minuto.

## ¿Puedo vender shaders que Urpify convirtió?

Sí, bajo sus propias licencias. Consulta [Redistribuir shaders convertidos](../license-redistribution.md) (en inglés). Un
shader cuya cabecera indica una licencia no comercial sigue bajo ella; el panel del shader muestra la línea de la
licencia.

## ¿Por qué el análisis dice que mi proyecto ya estaba roto antes de la migración?

Porque lo estaba: un shader que no compila en Built-in, scripts que no compilan, un archivo include que no está en el
proyecto, un material que no está en el proyecto. Urpify lo muestra aparte porque la migración no lo arreglará.
Arréglalo primero y vuelve a analizar.

## Después de revertir URP, los shaders muestran errores de include una vez. ¿Por qué?

Tras quitar URP de un proyecto que lo tenía (reversión o vuelta atrás manual), la primera sesión del Editor aún puede
compilar los SubShaders de URP de los shaders hechos con Mantener compatibilidad con Built-in y registrar errores de
include, por resultados de importación que quedan en `Library/Artifacts`. En la segunda sesión desaparecen. Es el
comportamiento de importación de Unity, no de los archivos de shader: en un proyecto que nunca tuvo URP no hay errores.
Abre el proyecto una vez más o ejecuta Assets > Reimport All.

## La ventana está en otro idioma.

Project Settings > Urpify > Personal > Idioma. Por defecto el idioma sigue al del Editor y usa inglés cuando Urpify no
tiene ese idioma.
