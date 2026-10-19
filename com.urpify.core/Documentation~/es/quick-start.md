# Inicio rápido

El ciclo completo en 5 minutos: analizar, migrar, comparar. Pruébalo primero con la escena de demostración y después con
una copia de tu proyecto.

## 1. Instalar

Instala en un proyecto que todavía use la Built-in Render Pipeline:

- **Urpify Pro:** un solo archivo, `Urpify-Pro-1.0.0.unitypackage` (desde la Asset Store: Package Manager > My Assets > Import). Assets > Import Package > Custom Package, importa todo: contiene Urpify, Urpify Pro, la
  demostración y esta documentación. ¿Ya tienes Urpify gratuito instalado desde un `.unitypackage`? Importa Pro encima, no
  hay que quitar nada. ¿Instalado con el Package Manager? Quítalo allí primero (Package Manager > Urpify > Remove), porque
  Urpify Pro ya lo contiene.
- **Urpify (gratuito):** `Urpify-1.0.0.unitypackage` de [GitHub Releases](https://github.com/nbxTeam/urpify/releases/latest),
  importado de la misma forma, o Package Manager > `+` > Add package from git URL:
  `https://github.com/nbxTeam/urpify.git?path=com.urpify.core#v1.0.0`.

Haz commit de tu proyecto en el control de versiones antes de migrar. Urpify guarda una copia de cada archivo que cambia,
pero un commit es el punto de restauración más seguro.

## 2. Abrir la demostración (opcional)

Help > Urpify > **Import Demo**, o **Importar demostración** en la ventana de Urpify vacía. La demostración va a
`Assets/Samples/Urpify/1.0.0/Demo` y su escena se abre. Si instalaste con el Package Manager, Package Manager > Urpify > Samples > **Demo** >
Import hace lo mismo. La escena tiene un
puesto por cada construcción que se rompe en URP: surface shaders, un shader vert/frag iluminado, vidrio con GrabPass,
agua que lee la profundidad, un proyector, un espejo que renderiza un script, un material creado en código y vidrio en el
modo Standard Transparent. Cada puesto tiene un Migration Viewpoint, así que la comparación muestra cada uno de cerca.

## 3. Analizar

Window > Rendering > **Urpify** y después **Analizar proyecto**.

El análisis solo lee archivos. La lista de la izquierda muestra cada shader, script, material, escena y ajuste que se
rompe en URP, agrupados por tipo. Cada fila tiene un icono:

- cuadrado relleno: se ve rosa;
- triángulo relleno: se rompe sin error (un portal se convierte en una pared, el agua pierde su espuma, un efecto
  desaparece);
- rombo relleno: se ve distinto;
- anillo: sin cambio visible.

Selecciona una fila para ver qué se rompe, por qué y quién lo arregla. Un shader se abre en el panel del shader: su código
antes y después de la conversión y una vista previa en Built-in y en URP, una al lado de la otra.

## 4. Migrar

Selecciona **Migrar...**. La ventana muestra los pasos:

1. **Analizar**: el mismo análisis de solo lectura.
2. **Copia de seguridad**: copia los assets, ajustes y lista de paquetes del proyecto a `Urpify/Backup`.
3. **Capturar antes**: reproduce cada escena en Play Mode y guarda fotogramas de la cámara principal y de cada Migration
   Viewpoint.
4. **Instalar URP**: añade la versión de URP que viene con tu Editor.
5. **Ejecutar el conversor de Unity**: el Render Pipeline Converter de Unity, con los arreglos que Urpify necesita para
   ejecutarlo de forma fiable.
6. **Conversores de Urpify**: arregla lo que el conversor de Unity deja roto. Con Pro, además convierte shaders, scripts y
   componentes.
7. **Capturar después y comparar**: los mismos fotogramas en URP, comparados con los de antes.
8. **Revisión**: el informe final.

Selecciona **Ejecutar todo**. Cada paso también se puede ejecutar por separado y revertir: **Revertir hasta aquí** en un
paso, o **Revertir todo**.

## 5. Comparar

Selecciona **Comparar escenas**. Cada escena muestra sus fotogramas de antes y después con un deslizador entre ellos.
**Mostrar diferencias** marca los píxeles cambiados: cuanto más rojo, mayor el cambio. **Solo cambiadas** oculta las
escenas que se ven igual.

En la ventana principal, una escena tiene dos veredictos: **Estático**, del análisis, y **Observado**, de los fotogramas.
Un objeto rosa puede quedar fuera de todos los fotogramas capturados, así que un veredicto estático puede ser peor que el
observado. Añade un Migration Viewpoint (un GameObject vacío con el componente `MigrationViewpoint`) donde quieras mirar
más de cerca y vuelve a capturar.

## 6. Terminar o revertir

Abre **Informe** > Exportar HTML para obtener un informe que puedes enviar a tu equipo. Si no te gusta el resultado,
**Revertir todo** restaura cada archivo que la migración cambió y mueve a la copia de seguridad los archivos que creó.
Los archivos que aparecieron entre pasos nunca se tocan, así que tu propio trabajo se queda.
Después de revertir desde la ventana, Unity se reinicia enseguida para cargar el proyecto tal como está en el disco.

Siguiente: [Migrar un juego](scenario-game.md), [Convertir un shader](scenario-shader.md),
[Convertir un paquete de la Asset Store](scenario-asset.md).
