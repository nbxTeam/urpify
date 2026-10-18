# Shaders vert/frag sin iluminación

![Puesto V01: un shader CG sin iluminación con niebla y UV desplazándose, igual en los tres](../../images/V01.jpg)

*Puesto V01: un shader CG sin iluminación con niebla y UV desplazándose. URP lo dibuja tal cual.*

## Qué pasa

Un shader vert/frag sin etiqueta LightMode o con `Always` suele renderizarse en URP sin cambios: URP dibuja una pasada sin
etiqueta LightMode como `SRPDefaultUnlit`, y el código CG con `UnityCG.cginc` sigue compilando. Urpify comprueba que el
shader compila en URP y, si es así, lo deja en paz.

Hallazgo: ninguno, o una nota de que URP dibuja el shader tal cual.

Lo que aún puede romperse, y tiene su propia página:
- dos o más pasadas sin LightMode: [Pasadas extra](extra-passes.md);
- leer `_CameraDepthTexture`: [Textura de profundidad](depth-texture.md);
- `GrabPass`: [GrabPass](grabpass.md);
- variables de luz de Built-in como `_LightColor0` en una pasada sin iluminación: en URP valen cero; Pro las lleva a la
  luz principal de URP.
