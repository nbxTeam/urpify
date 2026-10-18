# En qué se ve distinto URP

Algunas diferencias tras una migración no son roturas ni errores de conversión: URP simplemente dibuja el mismo material
un poco distinto que Built-in. La comparación de escenas las marca como «Cambió» cuando son lo bastante grandes, así que
conviene conocerlas.

## Standard a URP Lit

![Puesto STD01: Standard y URP Lit uno al lado del otro](../images/STD01.jpg)

La conversión propia de Unity de Standard a URP Lit cambia un poco los brillos especulares y los reflejos: URP tiene otro
modelo de reflexión y otros reflejos del entorno. En nuestras escenas de prueba difiere hasta aproximadamente un 7 % del
fotograma. Los umbrales de Urpify para «Igual» y «Cambió» están calibrados justo con esta conversión, así que por sí sola
no hace que una escena «cambie».

## Luces adicionales

Built-in dibuja cada luz adicional en su propia pasada aditiva; URP las suma en una pasada, con su propia curva de
atenuación y un límite de luces por objeto (configurable en el URP Asset). Las escenas con muchas luces puntuales se ven
algo distintas, y URP descarta las luces que superan el límite por objeto.

## Bloom

El bloom de URP reparte los puntos brillantes pequeños de forma más ancha y suave que Post-processing Stack v2. Urpify
traslada los parámetros del bloom con una calibración que acerca el brillo, pero no lo hace idéntico; el resto es la
pirámide de bloom distinta, que ningún parámetro reproduce.

## Luz ambiental en un juego de Unity 6

Una escena que toma su luz ambiental del skybox pero no tiene datos de iluminación horneados se ve más oscura en un juego
de Unity 6 que en el Editor, tanto en Built-in como en URP. No lo causa la migración, pero se nota al comparar el juego
migrado con el Editor. El análisis lo informa («Luz ambiental del skybox sin hornear»), y el botón **Hornear entorno** del
panel de la escena hornea solo la luz ambiental y los reflejos, con copia de seguridad.

## GrabPass con Opaque Texture

Con el origen de GrabPass por defecto, los objetos transparentes no aparecen en la refracción: URP copia la pantalla antes
de dibujarlos. Elige el Origen de GrabPass «Tras los transparentes» para igualar a Built-in. Consulta
[GrabPass](constructs/grabpass.md).

## Objetos transparentes y orden

URP ordena los objetos transparentes de la misma manera, pero dibuja las pasadas extra de los shaders convertidos (la
tercera y siguientes) después de las dos primeras pasadas de todos los objetos, lo que puede cambiar el orden de objetos
transparentes superpuestos. Consulta [Pasadas extra](constructs/extra-passes.md).
