# Shaders vert/frag sem iluminação

![Estande V01: um shader CG sem iluminação com neblina e UV rolando, igual nos três](../../images/V01.jpg)

*Estande V01: um shader CG sem iluminação com neblina e UV rolando. O URP o desenha como está.*

## O que acontece

Um shader vert/frag sem tag LightMode ou com `Always` costuma renderizar no URP sem mudanças: o URP desenha uma passada sem
tag LightMode como `SRPDefaultUnlit`, e o código CG com `UnityCG.cginc` continua compilando. O Urpify confere se o shader
compila no URP e, se compilar, não mexe nele.

Achado: nenhum, ou uma nota de que o URP desenha o shader como está.

O que ainda pode quebrar, e tem página própria:
- duas ou mais passadas sem LightMode: [Passadas extras](extra-passes.md);
- ler `_CameraDepthTexture`: [Textura de profundidade](depth-texture.md);
- `GrabPass`: [GrabPass](grabpass.md);
- variáveis de luz do Built-in como `_LightColor0` numa passada sem iluminação: no URP elas valem zero; o Pro as liga à
  luz principal do URP.
