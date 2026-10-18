# Textura depth-normals e shaders de substituição

O laboratório não tem estande para estas construções; elas foram conferidas no ToonWaterShader (IronWarrior, Unlicense),
cuja espuma da água depende das duas.

## O que quebra

- `_CameraDepthNormalsTexture` não existe no URP. Um shader que a lê não recebe nada: contornos baseados em normais e a
  espuma ao longo de uma margem somem.
- `Camera.SetReplacementShader` e `Camera.RenderWithShader` não têm suporte no URP. Um script que renderiza a cena com um
  shader de substituição numa textura (um jeito comum de obter normais ou profundidade para um efeito) não renderiza nada
  útil, sem aviso. No ToonWaterShader foi assim que a espuma se perdeu.

Achados: «Lê _CameraDepthNormalsTexture» (`SHADER.DEPTHNORMALS_TEXTURE`), uma chamada de shader de substituição
(`SCRIPT.REPLACEMENT_SHADER`).

## O que o Urpify faz (Pro)

- O Renderer Feature Built-in Camera Hooks do Urpify grava `_CameraDepthNormalsTexture` a partir das texturas de
  profundidade e normais do URP na codificação do Built-in (`EncodeDepthNormal`), então o shader a lê como antes. O
  feature só pede ao URP a textura de normais quando um shader precisa dela.
- Uma câmera com shader de substituição vira uma passada do mesmo Renderer Feature, que desenha os objetos opacos da
  câmera com o shader de substituição no mesmo destino, com a mesma correspondência de tags (`RenderType`).

## Quão perto

`Automático, aproximado`. As normais do URP vêm da própria passada de depth-normals dele, então as bordas podem diferir um
pouco do Built-in.
