# Migrar un juego

Tienes un juego con la Built-in Render Pipeline y lo quieres en URP, con el mismo aspecto.

## Antes de empezar

- Haz commit del proyecto. Urpify guarda copia de lo que cambia, pero un commit es el punto de restauración en el que más
  confías.
- Comprueba que el proyecto compila y entra en Play Mode en Built-in. Urpify informa de los errores de compilación de los
  scripts como un hallazgo: bloquean el Play Mode y, con él, las capturas.
- Decide qué escenas comparar: Project Settings > Urpify > Comparación > Escenas. Por defecto, las escenas de Build
  Settings.
- Coloca algunos Migration Viewpoints en escenas donde sabes que un efecto importa: un portal, un espejo, agua, un efecto
  de posprocesado. Añade el componente `MigrationViewpoint` a un GameObject vacío y oriéntalo como una cámara. Está marcado
  como EditorOnly y nunca llega a una build.

## Analizar y leer el resumen

Window > Rendering > Urpify > **Analizar proyecto**. El resumen empieza por las escenas que se rompen y luego cuenta los
assets rotos según quién los arregla:

- **El conversor de Unity y Urpify**: shaders integrados que el Render Pipeline Converter de Unity actualiza, y los casos
  en que su resultado es incorrecto, que el paquete gratuito arregla después.
- **Urpify Pro**: shaders propios, efectos de imagen, scripts de cámara, command buffers y proyectores.
- **A mano o aún no compatible**: el proyecto ya estaba roto antes de la migración, URP no tiene nada a lo que llevar la
  construcción, o Urpify todavía no la convierte. Consulta [Qué no se convierte](not-converted.md).

La lista de la izquierda se puede filtrar por problema, arreglo y tipo, y buscar por nombre de archivo o id de regla.
**Arreglado sin Pro** muestra lo que resuelve el paquete gratuito.

## Migrar

**Migrar...** > **Ejecutar todo**, o paso a paso. Los pasos que cambian archivos guardan primero una copia. Mientras un
paso se ejecuta, la ventana muestra su progreso, y cualquier paso se puede volver a ejecutar. Si el Editor se cierra a
mitad de un paso, la próxima vez que se abra la ventana ofrece ejecutarlo de nuevo o revertirlo.

## Comparar y revisar

Después de **Capturar después y comparar**, abre **Comparar escenas**:

- **Igual**: los fotogramas coinciden dentro de los umbrales calibrados con Standard a URP Lit.
- **Cambió**: algunos píxeles difieren, o una zona cambió mucho (un reflejo perdido, un brillo perdido), o la escena
  registró errores mientras se reproducía.
- **Rosa nuevo**: un objeto se renderiza con el shader de error.

Mira cada escena que cambió con **Mostrar diferencias**. Algunas diferencias son de URP, no de la migración: consulta
[En qué se ve distinto URP](urp-differences.md).

El informe final (**Revisión**) lista cada archivo convertido con su nivel: `Auto` (prácticamente idéntico), `Aprox.`
(cercano, no idéntico píxel a píxel), `Revisar` (convertido, pero el render de prueba de Urpify difiere del original:
revísalo) y lo que hay que hacer a mano.

## Revertir

**Revertir todo** deja el proyecto como estaba antes del primer paso. **Revertir hasta aquí** en un paso deshace cada paso
posterior. Los archivos que cambiaste después de la migración se quedan como están y se listan en la Console.

## En una pipeline de build

Cada paso se ejecuta desde la línea de comandos, así que un servidor de build puede migrar una rama y hacer fallar la
build cuando una escena se vuelve rosa. Consulta [Línea de comandos](cli.md).
