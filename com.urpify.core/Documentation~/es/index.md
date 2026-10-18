# Urpify

Urpify pasa un proyecto de Unity, un shader o un paquete de la Asset Store de la Built-in Render Pipeline a URP. Muestra
todo lo que el cambio rompe antes de que cambie nada, incluido lo que se rompe sin ningún error. Después convierte lo que
puede, guarda una copia de seguridad de cada archivo que toca y compara cada escena antes y después con fotogramas
capturados. Cada paso se puede revertir.

Urpify viene en dos paquetes:

| | Urpify (gratis, MIT) | Urpify Pro |
|---|---|---|
| Analizar un proyecto, carpeta, archivo o `.unitypackage` | Sí | Sí |
| Comparar escenas antes y después con fotogramas capturados | Sí | Sí |
| Ejecutar el Render Pipeline Converter de Unity de forma fiable, desde la ventana o la línea de comandos | Sí | Sí |
| Correcciones tras el conversor de Unity (vidrio, Self-Illumin, materiales integrados, bloom de Post-processing v2, ajustes del URP Asset, materiales de paquetes) | Sí | Sí |
| Copia de seguridad y reversión | Sí | Sí |
| Panel del shader: código, vista previa en Built-in y en URP tal como están | Sí | Sí |
| Convertir shaders propios, efectos de imagen, scripts de cámara, command buffers y proyectores | No | Sí |
| Salida: un archivo de shader para ambas pipelines (Mantener compatibilidad con Built-in) | No | Sí |
| Exportar un `.unitypackage` convertido | Solo el informe | Sí |
| Línea de comandos | Análisis, captura, comparación, conversor de Unity y correcciones gratuitas | Todo |

## Contenido

Empieza aquí:
- [Inicio rápido](quick-start.md): el ciclo completo en 5 minutos, con la escena de demostración o con tu proyecto.
- [Migrar un juego](scenario-game.md)
- [Convertir un shader](scenario-shader.md)
- [Convertir un paquete de la Asset Store para ambas pipelines](scenario-asset.md)

Cómo trata Urpify cada construcción de la Built-in Render Pipeline:
- [Construcciones: resumen](constructs/index.md), una página por construcción con imágenes de antes y después.
- [Qué no se convierte y por qué](not-converted.md)
- [En qué se ve distinto URP](urp-differences.md)

Referencia:
- [La ventana de Urpify](window.md): iconos, contadores, veredictos.
- [Configuración](settings.md)
- [Línea de comandos](cli.md)
- [Esquema del informe](../report-schema.md) (en inglés)
- [Versiones de Unity y URP](urp-version-matrix.md)
- [Preguntas frecuentes](faq.md)
- [Redistribuir shaders convertidos](../license-redistribution.md) (en inglés)

## Requisitos

- Unity 2022.3 LTS (URP 14) o Unity 6000.0 LTS, 6000.3 LTS, 6000.5 o 6000.6 (URP 17), las versiones probadas. Consulta [Versiones de Unity y URP](urp-version-matrix.md).
- Un proyecto con la Built-in Render Pipeline o un `.unitypackage` hecho para ella. Urpify instala URP por su cuenta
  cuando la migración lo necesita.
- Ningún otro paquete. Urpify no envía nada por la red, salvo el archivo del shader que decidas enviar con
  `Enviar informe` (consulta las [preguntas frecuentes](faq.md)).

## Idiomas

La ventana habla English, 简体中文, 日本語, 한국어, Русский, Español, Português (Brasil) y Deutsch. Por defecto sigue el
idioma del Editor (Preferences > Languages) y usa inglés cuando Urpify no tiene ese idioma. Cámbialo en Project Settings >
Urpify > Personal > Idioma. Las notas de los conversores en los informes, la línea de comandos y los detalles técnicos
del informe HTML se quedan en inglés.
