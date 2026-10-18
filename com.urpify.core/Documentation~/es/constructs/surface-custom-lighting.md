# Funciones de iluminación propias

![Puesto S05: una función de iluminación con rampa toon, rosa tras el conversor de Unity, igual tras Urpify](../../images/S05.jpg)

*Puesto S05: un surface shader con su propia función de iluminación, una rampa toon.*

## Qué se rompe

Un surface shader puede nombrar su propio modelo de iluminación, `#pragma surface surf Toon`, y definir `LightingToon`.
Built-in la llama una vez por luz: en ForwardBase para la luz principal y en ForwardAdd para cada luz adicional, con
`_LightColor0` y la atenuación de la luz. URP no tiene surface shaders, así que el objeto es rosa.

Hallazgo: «Función de iluminación propia ...» (`SHADER.SURFACE.CUSTOM_LIGHTING`).

## Qué hace Urpify (Pro)

La función del autor se copia sin cambios. La pasada forward generada la llama para la luz principal de URP y después para
cada luz adicional, con las sombras y la atenuación de URP, y suma los resultados, como hacían las dos pasadas de Built-in.
Dentro de la función, `_LightColor0` es el color de la luz para la que se llama.

Se reconocen todas las firmas de Built-in:

| Firma | Ejemplo |
|---|---|
| Sin dirección de vista | `half4 LightingToon(SurfaceOutput s, half3 lightDir, half atten)` |
| Con dirección de vista | `half4 LightingToon(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)` |
| Con iluminación global | `half4 LightingToon(SurfaceOutput s, half3 viewDir, UnityGI gi)` y su `LightingToon_GI` |
| Variantes deferred y prepass | `_Deferred`, `_PrePass`: no se usan, URP renderiza en forward |

La función se encuentra en cualquier sitio donde la encontraría el compilador: en el programa, en bloques `CGINCLUDE`, en
archivos include del proyecto a cualquier profundidad y en macros `#define` que declaran funciones.

## Cuánto se acerca

`Automático, aproximado`. Built-in suma cada luz adicional en su propia pasada con mezcla aditiva; URP las ilumina en una
pasada. Para objetos opacos las sumas coinciden; los shaders transparentes pueden diferir donde las luces se solapan. La
luz ambiental viene de los armónicos esféricos de URP, como en Built-in.

Si la función no está en el shader ni en los archivos include analizados, el hallazgo la nombra y el shader no se
convierte hasta que su archivo include esté en el ámbito del análisis.
