# Teselación

![Puesto S09: teselación con desplazamiento](../../images/S09.jpg)

*Puesto S09: un surface shader con `tessellate:` y desplazamiento.*

## Qué se rompe

`tessellate:func` hace que Unity genere etapas hull y domain para un surface shader. URP no tiene surface shaders, así que
el objeto es rosa.

Hallazgo: «Teselación (...)» (`SHADER.SURFACE.TESSELLATION`).

## Qué hace Urpify (Pro)

El conversor genera las etapas hull y domain con una plantilla que sigue a la pipeline integrada: la función de teselación
del autor da los factores, partición `fractional_odd`, y después la función `vertex:` desplaza los vértices nuevos. Se
tesela cada pasada que dibuja el objeto (forward, sombras, profundidad), así que la sombra sigue a la superficie
desplazada. `tessphong:` añade el suavizado de Phong con la intensidad indicada.

Los shaders vert/frag que escriben sus propias etapas hull y domain las conservan; los atributos que define Built-in
(`UNITY_domain`, `UNITY_partitioning`, `UNITY_outputtopology`, `UNITY_patchconstantfunc`, `UNITY_outputcontrolpoints`)
vienen de la biblioteca de Urpify, y la comprobación de compilación cubre las etapas hull y domain.

## Cuánto se acerca

`Automático, aproximado`: los mismos factores y el mismo desplazamiento, con la iluminación de URP encima.
