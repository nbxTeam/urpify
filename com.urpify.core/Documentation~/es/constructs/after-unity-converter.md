# Correcciones tras el conversor de Unity

El Render Pipeline Converter de Unity es lo primero que ejecuta la migración, y casi siempre acierta. Estos son los casos
en que su resultado es mediblemente incorrecto, y lo que hace después el paquete gratuito. Todos están en el paquete
gratuito, en el paso Conversores de Urpify.

## Standard en modo Transparent

![Puesto STD04: vidrio en modo Standard Transparent, blanco tras el conversor de Unity, vidrio de nuevo tras Urpify](../../images/STD04.jpg)

*STD04: vidrio en el modo Transparent de Standard.*

El conversor de Unity lleva Transparent a la mezcla Premultiply de URP, que espera colores ya multiplicados por el alfa.
El vidrio se vuelve casi opaco y blanco. Urpify cambia el material a la mezcla Alpha con Preserve Specular Lighting, que es
como lo mezcla Built-in. Hallazgo: «..., modo Transparent» (`MATERIAL.STANDARD_TRANSPARENT`).

## Legacy Self-Illumin

![Puesto LEG03: Legacy Self-Illumin, oscuro tras el conversor de Unity, brilla tras Urpify](../../images/LEG03.jpg)

*LEG03: `Legacy Shaders/Self-Illumin/Diffuse`.*

El conversor de Unity pasa estos materiales a Simple Lit, que no toma emisión del alfa Illumin: el brillo desaparece.
Urpify los pasa a su propio shader Self-Illumin para URP, que brilla como en Built-in. Hallazgo: `MATERIAL.SELF_ILLUMIN`.

## Materiales integrados del Editor

Los renderers que usan los `Default-Material` o `Default-Diffuse` integrados del Editor (un cubo nuevo, un plano) los
apunta el conversor de Unity al material por defecto de URP, que es gris medio en lugar de blanco. Urpify los apunta a
copias de URP con los valores del material integrado. Hallazgo: `SCENE.BUILTIN_MATERIAL`.

## Materiales dentro de paquetes

Los archivos de un paquete son de solo lectura, así que el conversor de Unity nunca cambia un material que vive en un
paquete. Cuando su shader es solo de Built-in, cada renderer que lo usa se vuelve rosa: las paredes de ProBuilder 5 usan
`ProBuilderDefault.mat`, cuyo shader `ProBuilder/Standard Vertex Color` es un surface shader. Urpify apunta esos
renderers, en escenas y prefabs, a una copia de URP en `Assets/Urpify/Materials/Packages/`: el actualizador propio de
Unity para un shader integrado (`Auto`); si no, URP Lit con el color, la textura, la suavidad y el metálico del material
(`Automático, aproximado`: lo que añade el propio shader, como los colores de vértice, no se conserva). Hallazgo:
`SCENE.PACKAGE_MATERIAL`.

## Shaders integrados sin actualizador

Algunos shaders integrados no tienen actualizador en el conversor de Unity («No upgrader found»): `Legacy Shaders/Decal`,
`Legacy Shaders/Transparent/VertexLit`, `Transparent/Parallax Diffuse` y `Specular`, y en URP 14 dos actualizadores
registrados con nombres antiguos. Urpify pasa esos materiales al shader de URP más cercano de su propia tabla, con las
propiedades trasladadas. Hallazgo: `MATERIAL.BUILTIN_NO_UPGRADER`.

## Post-processing Stack v2

El conversor de Unity convierte los volúmenes y perfiles de PPv2 en Volumes de URP, pero en modo batch no espera la
búsqueda de assets que necesita y no convierte ninguno: el bloom desaparece sin ningún error. El paso Ejecutar el
conversor de Unity de Urpify espera la búsqueda y después traslada los parámetros del Bloom con una calibración
(threshold y scatter) para que el brillo se acerque más al de Built-in. El resto de la diferencia es el propio bloom de
URP, consulta [En qué se ve distinto URP](../urp-differences.md). Hallazgo: `SCENE.PPV2`.

## Ajustes del URP Asset

- **Depth Texture y Opaque Texture** se activan en cada URP Asset cuando las lee un shader que URP dibuja tal cual
  (consulta [Textura de profundidad](depth-texture.md)).
- **Texturas del URP Asset** (URP 14): el conversor de Unity guarda sus URP Assets con las texturas blue noise y Bayer
  vacías. El Editor las rellena; un juego compilado no: lanza `NullReferenceException` en cada fotograma y se ve negro.
  Urpify las rellena desde el paquete de URP. Hallazgo: `SETTINGS.URP_ASSET_TEXTURES`.
- **Luz principal**: en los niveles de calidad con Pixel Light Count 0, Built-in iluminaba igualmente por píxel la luz
  direccional principal; Urpify pone ahí la Main Light de URP en Per Pixel.

## Material Variants

El conversor de Unity pasa un Material Variant al shader de URP de su padre, pero descarta los valores propios de la
variante: se convierte en una copia silenciosa del padre. Urpify lee los valores propios de la variante de la copia de
seguridad del paso Ejecutar el conversor de Unity y los vuelve a poner como sobrescrituras de las propiedades de URP.
Hallazgo: `MATERIAL.VARIANT`.

## Materiales de solo lectura dentro de modelos

Los materiales incrustados en archivos FBX son de solo lectura, y el módulo del conversor de Unity para ellos no se
ejecutaba de forma fiable en modo batch. Urpify ejecuta ese módulo objeto por objeto para los renderers y terrenos que
encontró, en ambas versiones de Unity.
