# La ventana de Urpify

`Window > Rendering > Urpify`. La ventana mantiene sus textos breves; esta página explica qué significan sus iconos,
contadores y etiquetas. Pasa el cursor por cualquier icono o contador de la ventana para ver la misma explicación.

## Iconos de gravedad

Cada icono tiene su propia forma, así que el color nunca es la única pista.

| Icono | Nombre | Qué pasa al cambiar a URP |
|---|---|---|
| Cuadrado relleno (rosa) | Se ve rosa | El objeto se renderiza con el shader de error de URP. En una build de release es invisible. |
| Triángulo relleno (ámbar) | Se rompe sin error | Ni error ni rosa, pero el efecto falta o está mal: un portal se convierte en una pared gris, el agua pierde su espuma, un efecto de posprocesado desaparece. |
| Rombo relleno (azul) | Se ve distinto | Se renderiza, con una diferencia visible, como una escena más oscura. |
| Anillo (gris) | Sin cambio visible | Nada que hacer, o el conversor de Unity lo resuelve tal como está. |

Una fila de la lista, una escena y un asset toman el icono de su peor hallazgo.

## La barra bajo la barra de herramientas

Números en grupos con nombre. Antes de la migración: `Escenas` y `Assets`, cada uno con un icono y un número por
gravedad: cuántas escenas tienen objetos con esa gravedad y cuántos assets la tienen. Una escena o un asset cuenta en cada
categoría que tiene, así que los números se solapan. Tras las capturas, `Capturado` añade lo que muestran los fotogramas:
rosa nuevo, cambiadas, sin cambios. Después de la migración el primer grupo es `Resultado`: arreglado, por revisar,
pendiente. Cada número es un botón: selecciónalo para que la lista muestre solo lo que cuenta y vuelve a seleccionarlo
para mostrarlo todo. Pasa el cursor por un número para ver qué cuenta, y por `Último análisis` para ver el número de
archivos y el tiempo del análisis.

## Veredictos de una escena

Una escena tiene dos veredictos:

- **Estático**, del análisis: la peor rotura de todo lo que usa la escena. Un objeto rosa puede quedar fuera de todos los
  fotogramas capturados, así que un rosa estático puede estar junto a un «Igual» observado.
- **Observado**, de las capturas de antes y después de la migración: el peor fotograma. Cada fotograma muestra la
  proporción de píxeles cambiados y de píxeles rosas después; pasa el cursor por la línea para ver la diferencia media y
  la proporción de rosa antes. Una zona cambiada más grande (un reflejo perdido, un brillo perdido) hace que un fotograma
  «cambie» aunque cambien pocos píxeles en total. Los errores en la Console mientras la escena se reproducía tras la
  migración también la hacen «cambiar», mostrando el primer error. Si el análisis esperaba una rotura silenciosa que la
  migración no arregló (solo el conversor de Unity, el paquete gratuito para una rotura que solo arregla Pro, o una rotura
  que Urpify no pudo convertir) y los fotogramas aun así se ven igual, el panel lo dice: revisa el mapa de diferencias.

En la vista de antes y después, la parte a la izquierda de la línea es antes y la de la derecha es después. `Mostrar
diferencias` marca los píxeles cambiados: cuanto mayor el cambio, más rojo el píxel.

## Quién lo arregla

El resumen cuenta los assets rotos según quién los arregla:

- **El conversor de Unity y Urpify**: el Render Pipeline Converter de Unity y después las correcciones que Urpify ejecuta
  tras él (ambos en el paquete gratuito).
- **Urpify Pro** (en el paquete gratuito: **Necesitan Pro**, con un candado): shaders, scripts y componentes que solo
  traducen los conversores de Pro.
- **A mano o aún no compatible**: el proyecto ya estaba roto antes de la migración, la construcción no tiene equivalente
  en URP, o Urpify aún no la convierte. El hallazgo dice qué hacer.

Una fila de la lista dice quién arregla su asset: `Unity` (el conversor de Unity), `Urpify` (los arreglos gratuitos), `Pro`
(Urpify Pro; en el paquete gratuito, un candado y `Pro`), `A mano`. La descripción emergente dice cuánto se acerca el
resultado: coincide con el original, se le acerca o está convertido pero se ve distinto (revísalo). El filtro `Quién lo
arregla` selecciona los mismos grupos. Después de la migración la fila dice en su lugar qué pasó con el asset:
`Arreglado`, `Revisar` o `Pendiente`. Antes del paso Conversores de Urpify es lo que espera el análisis; después, lo que
hizo el paso. El informe conserva los niveles propios de los conversores (`Auto`, `Aprox.`, `Revisar`, `Aún no`,
`Manual`).

Un shader que Urpify convierte se comprueba con un render: antes de la conversión se renderiza en Built-in una esfera con
los materiales del proyecto para ese shader, después en URP, y se comparan. Cuando difiere una gran parte de la esfera, el
shader se convierte igualmente, pero no se llama `Auto`: es `Revisar`, y el par de imágenes está en
`Urpify/Reports/self-check/`. «Una gran parte» es más del 25 % de la esfera: la conversión propia de Unity de Standard a
URP Lit cambia los colores oscuros y saturados hasta un 20 % aproximadamente, y así es como se ve URP Lit, no un fallo. El
rosa, o un objeto que oculta lo que hay detrás y muestra el color de borrado de la cámara, es `Revisar` a cualquier
tamaño.

Algunos shaders no se pueden juzgar en una esfera y son como mucho `Aprox.`, con el motivo en el informe; revísalos en la
comparación de escenas:

- un efecto de pantalla sin material (los scripts lo aplican a toda la imagen);
- un shader interno bajo `Hidden/` (los scripts lo dibujan con sus propios datos);
- un shader que lee el tiempo (`_Time`): el render de Built-in se hace antes de la conversión y el de URP después, así
  que muestran momentos distintos de la animación (solo cuentan el rosa o un render que se oscurece).

En Unity 2022.3, un shader con `GrabPass` se renderiza con una cámara oculta de tipo Game en lugar de una cámara de vista
previa: URP 14 no da Opaque Texture a una cámara de vista previa. Esa cámara también ejecuta los Renderer Features de tu
URP Asset.

## Mostrar qué se rompe

`Mostrar qué se rompe`, en la vista general y sobre los pasos de la ventana de migración, muestra qué hace URP por sí solo
con el proyecto antes de arreglar nada: hace una copia de seguridad, captura las escenas en Built-in, instala URP, ejecuta
el conversor de Unity, vuelve a capturar las escenas y abre el resultado. Los arreglos de Urpify todavía no se han
ejecutado. Luego elige `Arreglar con Urpify` (los conversores de Urpify, las capturas de nuevo y el informe) o `Revertir`.
En el paquete gratuito los arreglos son los gratuitos, y el resultado dice qué necesita Urpify Pro.

## Los pasos en orden

Un paso cuyos pasos anteriores no están hechos no se ejecuta y dice por qué bajo su descripción: Capturar antes necesita la
copia de seguridad, el conversor de Unity necesita URP instalado, los Conversores de Urpify necesitan el conversor de Unity
(salvo con Keep Built-in support, en que el proyecto sigue en Built-in), y Capturar después necesita las imágenes de antes
y un proyecto que renderice con URP. Dos capturas hechas con la misma pipeline nunca se comparan: el resultado dice que no
hay nada que comparar.

## El resultado después de la migración

En cuanto se ha ejecutado el conversor de Unity, el lado derecho muestra el resultado en lugar de la vista general: una
línea con lo importante (por ejemplo `Listo. Tus escenas se ven igual que antes del cambio`), la escena que más cambió,
antes y después, luego solo lo que te necesita, cada cosa con su botón, y los assets arreglados plegados bajo `Arreglado
automáticamente`. La ventana vuelve a analizar el proyecto después de cada paso, así que la lista muestra qué está
arreglado y qué queda. Los archivos que Urpify añade al proyecto (`Assets/Urpify/`, la carpeta `UrpCompat` de un paquete,
los scripts `UrpSettingsCheck_*`) nunca aparecen en la lista.

## Comparar escenas

Cada escena capturada con su resultado en palabras. Selecciona una escena para comparar sus imágenes ahí mismo, con el
control deslizante; `Abrir escena` la abre. Con una sola escena, la comparación está abierta desde el principio. La ventana
muestra siempre la última comparación, también después de un análisis posterior. Las imágenes se guardan como las muestra
la pantalla, sin transparencia, al tamaño de la vista Game, salvo que `Tamaño de captura` en los ajustes diga otra cosa.

## Etiqueta de licencia

Un shader cuya cabecera nombra una licencia (un identificador SPDX como `MIT` o `CC-BY-NC-4.0`) la muestra como etiqueta
junto a su nombre. Una copia convertida es una obra derivada y sigue bajo la misma licencia. Una licencia no comercial
(`-NC`) tiene una etiqueta ámbar: no se puede vender una copia convertida de ese shader.

## Luz ambiental (panel de la escena)

En Unity 6, un juego renderiza sin esa luz una escena que toma su luz ambiental del skybox y no tiene datos de
iluminación horneados, tanto en Built-in como en URP, así que la escena se ve más oscura que en el Editor. La migración no
lo causa, así que el hallazgo es informativo (un icono de anillo). (En Unity 2022.3 el juego recibe la luz ambiental
aunque no haya datos horneados, y Urpify no informa de esas escenas). `Hornear entorno` hornea en los datos de
iluminación de la escena solo la luz ambiental y los reflejos del skybox (no se hornean lightmaps, y Auto Generate se
desactiva para la escena), para que el juego coincida con el Editor. Antes se hace una copia de seguridad del proyecto;
`Revertir horneado` en el mismo lugar, o Revertir en la ventana de migración, lo deshace.
