# Opciones de los surface shaders

`#pragma surface surf Standard <opciones>` cambia lo que genera Unity. Urpify lee cada opción y mantiene su significado
en las pasadas que escribe (Pro).

![Puesto S03: Lambert con una función de viento vertex:](../../images/S03.jpg)

*S03: `vertex:vert` dobla la malla como el viento. La función de vértices se ejecuta en cada pasada, así que la sombra
también se dobla.*

![Puesto S08: disolución con clip() y addshadow](../../images/S08.jpg)

*S08: disolución con `clip()` y `addshadow`: la sombra se recorta igual.*

![Puesto S10: decal:add](../../images/S10.jpg)

*S10: `decal:add`, una capa aditiva sobre objetos opacos.*

![Puesto S11: niebla por altura en finalcolor:](../../images/S11.jpg)

*S11: niebla por altura en `finalcolor:`, aplicada tras la iluminación, como en Built-in.*

## Opciones y en qué se convierten

| Opción | En URP |
|---|---|
| `vertex:func` | Se ejecuta en cada pasada generada: forward, sombras, profundidad, depth-normals, meta. Los campos extra de `Input` que rellena llegan a la función surface. |
| `finalcolor:func` | Se llama con el color iluminado, antes de la niebla. |
| `finalgbuffer:func` | No se usa: solo se ejecuta en la pasada G-buffer diferida de Built-in. Una nota lo indica. |
| `alpha`, `alpha:blend`, `alpha:fade`, `alpha:premul` | Cola transparente y la mezcla correspondiente; `premul` conserva el especular premultiplicado. |
| `alphatest:prop` | `clip()` contra la propiedad, en cada pasada. |
| `keepalpha` | El alfa de la función surface también se escribe en las pasadas opacas. |
| `decal:add`, `decal:blend` | Se dibuja tras los objetos opacos con mezcla aditiva o alfa, sin sombras. |
| `addshadow` | La pasada de sombras ejecuta la función de vértices y el recorte de la superficie, como la pasada forward. |
| `noshadow` | Sin pasada de sombras; la pasada forward no recibe sombras. |
| `noambient`, `novertexlights`, `nolightmap`, `nofog`, `nometa`, `noforwardadd` | Se quita la parte correspondiente: luz ambiental, luces por vértice, lightmaps, niebla, la pasada Meta, luces adicionales. |
| `halfasview`, `interpolateview`, `approxview`, `dualforward` | Ajustes de calidad de la dirección de vista que URP no necesita; se ignoran con una nota. |
| `fullforwardshadows`, `nodynlightmap`, `nodirlightmap`, `nolppv`, `noshadowmask`, `noinstancing`, `exclude_path:*`, `dithercrossfade` | Se reconocen; los ajustes propios de URP deciden esto (sombras de luces adicionales, modos de lightmap, light probe proxy volumes, fundido LOD), así que no cambian nada en el shader convertido. |
| `tessellate:func`, `tessphong:` | Consulta [Teselación](surface-tessellation.md). |

Una opción que Urpify no conoce aparece en el hallazgo «Opciones de surface no compatibles»
(`SHADER.SURFACE.UNKNOWN_OPTION`); el resto del shader se convierte igualmente.

El ajuste Sombras (Project Settings > Urpify) puede forzar o desactivar las sombras en todos los surface shaders
convertidos.
