# Árboles de Tree Creator y de soft occlusion

El laboratorio no tiene puesto para árboles; se comprobaron con ShaderTutorials (ronja-tutorials) y los árboles de Nature
Starter Kit.

## Qué se rompe

URP no tiene los shaders de Tree Creator (`Nature/Tree Creator Bark`, `Leaves`, `Leaves Fast`, `Optimized`) ni los shaders
de árboles de soft occlusion. Los árboles hechos con ellos se ven rosas. La hierba y las mallas de detalle del terreno no
se ven afectadas: URP las dibuja con sus propios shaders de detalle.

Hallazgo: «..., sin versión para URP» (`MATERIAL.BUILTIN_TREE`).

## Qué hace Urpify (Pro)

Unity publica el código de sus shaders integrados bajo la licencia MIT. Urpify Pro convierte los 8 shaders de árboles
propios de Unity con su conversor habitual, añade a su biblioteca las funciones de `TerrainEngine.cginc` que necesitan
(viento, flexión, billboards) y pasa los materiales de corteza y hojas, también los de dentro de prefabs, a los shaders
convertidos. El texto de la licencia está en `Third Party Notices.md` de Pro.

## Cuánto se acerca

`Automático, aproximado`. Los árboles dejan de ser rosas y conservan sus materiales. La animación del viento y los
billboards en URP se revisaron en unos segundos de fotogramas, no se midieron píxel a píxel; compara tus escenas en la
comparación de escenas. Los assets de SpeedTree los actualiza el conversor de Unity, no Urpify.
