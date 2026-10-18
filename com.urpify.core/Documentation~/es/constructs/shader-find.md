# Shader.Find con nombres de shaders integrados

![Puesto C04: un material creado en código con Shader.Find("Standard"), rosa tras el conversor de Unity, arreglado por Urpify](../../images/C04.jpg)

*Puesto C04: un script crea en tiempo de ejecución un material con `new Material(Shader.Find("Standard"))` y establece
`_Color`.*

## Qué se rompe

El conversor de Unity actualiza archivos de materiales, no código. Un script que crea un material con
`Shader.Find("Standard")`, `"Diffuse"`, `"Legacy Shaders/..."` o cualquier otro nombre de shader integrado obtiene un
shader que URP no puede dibujar, y el objeto es rosa. Los nombres de propiedad que establece el script (`_MainTex`,
`_Color`, `_Glossiness`) son propiedades ocultas en URP Lit, así que, aunque se cambie el shader a mano, esas llamadas no
cambian nada.

Hallazgos: `Shader.Find("...")` (`SCRIPT.SHADER_FIND_BUILTIN`), «Nombres de propiedades de Built-in en código»
(`SCRIPT.BUILTIN_PROPERTY_NAMES`).

## Qué hace Urpify (Free)

Es una de las correcciones tras el conversor de Unity, en el paquete gratuito. En el mismo script:

- el nombre del shader pasa a ser el shader de URP que el conversor de Unity elige para ese shader integrado (`Standard` a
  `Universal Render Pipeline/Lit`), o el shader de URP más cercano de la tabla de Urpify para shaders sin regla de Unity;
- los nombres de propiedad que establece el mismo método pasan a los nombres de URP (`_MainTex` a `_BaseMap`, `_Color` a
  `_BaseColor`).

Con la salida Mantener compatibilidad con Built-in, cada nombre se elige en tiempo de ejecución según la pipeline activa,
así que el script sigue funcionando también en Built-in.

El script reescrito se compila antes de guardarse; si no compila, se restaura el original y se informa.

## Cuánto se acerca

`Auto`.
