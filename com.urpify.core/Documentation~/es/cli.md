# Línea de comandos

Todo lo que hace la ventana también funciona en modo batch, así que un servidor de build puede analizar una rama,
convertirla y hacer fallar la build cuando una escena se vuelve rosa. El paquete gratuito ejecuta Scan, Capture, Verify,
Rollback, todos los pasos de Migrate (su paso Conversores de Urpify ejecuta las correcciones gratuitas) y el análisis de
paquetes. Convertir shaders, scripts y componentes, y exportar un paquete convertido, requiere Pro.

```
Unity -batchmode -projectPath <proyecto> -executeMethod Urpify.Cli.Commands.<Comando> [argumentos]
```

**No pases `-quit`.** Cada comando cierra el Editor por sí mismo con su código de salida, después de escribirlo en un
archivo: algunos proyectos antiguos hacen que Unity falle al salir una vez terminado el trabajo, así que la CI debe leer
el archivo de resultado en lugar de fiarse solo del código de salida del proceso.

| Código de salida | Significado |
|---|---|
| 0 | Hecho. |
| 1 | Falló: argumento incorrecto, archivo que falta, paso fallido. El motivo está en el resultado JSON. |
| 2 | Hecho, y se encontraron hallazgos de nivel `-urpifyFailOn` o superior. |

Cada comando escribe, con éxito y ante cualquier error, un resultado JSON (`command`, `step`, `status`, `exitCode`,
`error`, `reports`) y un resultado de texto: el código de salida o, para un paso de Migrate, el paso y su estado, como
`KitConverters Done` (`Done`, `Skipped`, `Failed`, `TimedOut`). Los archivos:

| Comando | Resultado JSON (`Urpify/Reports/`) | Resultado de texto |
|---|---|---|
| Scan | `urpify-scan-result.json` | `urpify-scan-result.txt` (ambos en `-urpifyOutput`, por defecto `Urpify/Reports`) |
| Migrate | `urpify-step-result.json` | `Urpify/urpify-step-result.txt` |
| Capture | | `Urpify/Captures/<label>/urpify-capture-result.txt` |
| Verify | `urpify-verify-result.json` | `Urpify/urpify-verify-result.txt` |
| Rollback | `urpify-rollback-result.json` | `Urpify/urpify-rollback-result.txt` |
| Package | `urpify-package-result.json` | `Urpify/urpify-package-result.txt` |
| BakeEnvironment | `urpify-bake-result.json` | `Urpify/urpify-bake-result.txt` |
| Capture, Migrate o Package con un argumento erróneo | `urpify-command-result.json` | `Urpify/urpify-command-result.txt` |

## Comandos

### Scan

```
-executeMethod Urpify.Cli.Commands.Scan
    [-urpifyOutput <carpeta>]              por defecto Urpify/Reports
    [-urpifyScope <ruta>[;<ruta>...]]      por defecto Assets
    [-urpifyFormat json,html]              por defecto ambos
    [-urpifyFailOn pink|silent|degraded]
    [-urpifyNoCache]
```

Solo lectura. Escribe el informe JSON descrito en el [esquema del informe](../report-schema.md) (en inglés) y un informe
HTML autónomo. `urp-migration-report-latest.json` se escribe siempre, junto a los archivos con marca de tiempo. El
análisis guarda resultados por archivo en `Library/Urpify/cache`; `-urpifyNoCache` lo omite.

### Capture y Verify

```
-executeMethod Urpify.Cli.Commands.Capture -urpifyLabel <etiqueta>
    [-urpifyScenes build|all|<a.unity>;<b.unity>]   por defecto: Project Settings > Urpify > Comparación > Escenas
    [-urpifyWidth 960 -urpifyHeight 540 -urpifyFrames 30]
-executeMethod Urpify.Cli.Commands.Verify [-urpifyBefore before] [-urpifyAfter after] [-urpifyFailOn pink|changed]
```

Capture reproduce cada escena en Play Mode y guarda fotogramas de la cámara principal y de cada Migration Viewpoint en
`Urpify/Captures/<etiqueta>/`. Verify compara dos etiquetas y escribe un informe con el veredicto observado por escena.

### Migrate

```
-executeMethod Urpify.Cli.Commands.Migrate -urpifyStep <paso>
    [-urpifyShaderOutput replace|write-copy|keep-builtin]       kit-converters; por defecto, el ajuste del proyecto
    [-urpifyGrabPass opaque-texture|after-transparents]          kit-converters; por defecto, el ajuste del proyecto
    [-urpifyScenes ...] [-urpifyWidth ...] [-urpifyHeight ...] [-urpifyFrames ...]   pasos de captura
    [-urpifyStepTimeout 20]                                      minutos sin progreso antes de detener el paso; 0 = nunca
```

Los pasos en orden, uno por proceso del Editor: `scan`, `backup`, `capture-before`, `install-urp`, `unity-converter`,
`kit-converters`, `capture-after`, `review`. Install URP añade un paquete que compila el proceso siguiente, por eso cada
paso va en su propio proceso. Backup se ejecuta antes de Capture before, y Capture before se niega a ejecutarse sin copia
de seguridad: al reproducir escenas Unity vuelve a guardar assets antiguos y los paquetes crean archivos, y la copia debe
conservar el proyecto tal como estaba.

La versión de la Asset Store no instala paquetes: instala URP en el Package Manager antes de `install-urp`, que entonces solo lo comprueba. Un proyecto cuyo conversor de Unity necesita su ventana (con Post-processing v2, Unity 6000.3) ejecuta `unity-converter` desde la ventana de Urpify.

Los pasos que cambian archivos avisan en el log si el árbol de trabajo de git tiene cambios sin commit.

### Rollback

```
-executeMethod Urpify.Cli.Commands.Rollback [-urpifyToStep backup]
```

Sin `-urpifyToStep` revierte todos los pasos. Los archivos cambiados después de la migración y los que aparecieron entre
pasos se quedan como están y se listan en el log.

### Package

```
-executeMethod Urpify.Cli.Commands.Package -urpifyPackage <archivo.unitypackage> [-urpifyOutput <carpeta>]
    [-urpifyExport <convertido.unitypackage>] [-urpifyShaderOutput replace|keep-builtin] [-urpifyKeepScope]
```

Importa el paquete en una carpeta temporal, lo analiza y, con `-urpifyExport` (Pro), escribe el paquete convertido como se
describe en [Convertir un paquete de la Asset Store](scenario-asset.md). El informe de análisis se escribe siempre, también
cuando la exportación falla o se rechaza; entonces el código de salida es 1. La exportación compila los shaders
convertidos para URP, así que URP debe estar instalado en el proyecto. `-urpifyKeepScope` conserva la carpeta temporal
para revisarla.

### Bake environment

```
-executeMethod Urpify.Cli.Commands.BakeEnvironment -urpifyScene <Assets/a.unity> [-urpifyUndo]
```

Hornea la luz ambiental y los reflejos del skybox en una escena que no los tiene, con copia de seguridad; `-urpifyUndo`
revierte el horneado. Consulta el hallazgo «Luz ambiental del skybox sin hornear».

## Pasos interrumpidos y límites de tiempo

Un paso que se detiene a mitad (el Editor se cerró o falló, el paso falló) conserva un registro completo de lo que cambió:
la siguiente vez que se abre el Editor lo cierra como interrumpido, y sus cambios entran en la reversión. Mientras un paso
se ejecuta, `Urpify/progress.json` indica lo último que hizo (`Step`, `What`, `Done`, `Total`, `UpdatedUtc`). En modo
batch, un paso que no avanza durante `-urpifyStepTimeout` minutos se detiene como un Editor cerrado, con el motivo en
`progress.json` y en el mensaje del paso. El progreso es lo que el paso informa y los archivos que escribe, no el log de
Unity. Un Editor con ventana nunca se detiene.

## Proyectos cuyos scripts no compilan

Unity detiene una ejecución batch antes de que empiece cualquier comando («Scripts have compiler errors»). Añade
`-ignorecompilererrors`: el análisis informa entonces de los errores de compilación por sí mismo
(`PROJECT.SCRIPT_COMPILATION_FAILED`), y los pasos que cambian scripts los dejan en paz.

## Ejemplo: hacer fallar la build cuando una escena se vuelve rosa

```
U="/ruta/a/Unity"; P=/ruta/al/proyecto
for step in scan backup capture-before install-urp unity-converter kit-converters capture-after review; do
  "$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Migrate -urpifyStep $step -logFile -
  grep -q " Done\| Skipped" "$P/Urpify/urpify-step-result.txt" || { echo "falló el paso $step"; exit 1; }
done
"$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Verify -urpifyFailOn pink -logFile -
exit $(cat "$P/Urpify/urpify-verify-result.txt")
```

## Archivos que escriben los pasos

- `Urpify/state.json`: el estado de la migración, fuera de `Assets`.
- `Urpify/Backup/<id>/`: una copia de seguridad por cada paso que cambia archivos, con un manifiesto del hash de cada
  archivo antes y después.
- `Urpify/Captures/<etiqueta>/`: los fotogramas capturados y `captures.json`.
- `Urpify/Reports/`: informes de análisis, comparación y revisión; `kit-converters.json`, el informe propio del paso
  Conversores de Urpify (por archivo: `Path`, `Converter`, `Outcome`, `Notes`; cambios en el URP Asset; ajustes de URP que
  necesitan los archivos convertidos); `self-check/`, los renders de cada shader convertido en Built-in y en URP.

Añade `Urpify/` al control de versiones si quieres conservar el historial de la migración; la build no lo lee.
