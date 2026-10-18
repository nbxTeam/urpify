# Textura de profundidade

![Estande D01: água com espuma nas interseções, branca depois do conversor da Unity, como no Built-in depois do Urpify](../../images/D01.jpg)

*Estande D01: água transparente que lê `_CameraDepthTexture` para a cor de profundidade e a espuma em volta de uma pedra.
Só depois do conversor da Unity, a água fica branca.*

## O que quebra

Um shader que lê `_CameraDepthTexture` só recebe profundidade quando a Depth Texture está ligada no URP Asset. O conversor
da Unity só a liga nos níveis de qualidade que tinham Soft Particles. Um shader transparente tem um segundo problema: o
URP, por padrão, copia a profundidade depois dos objetos transparentes, e o conversor da Unity deixa assim. Na Unity 6 esse
shader então não recebe profundidade nenhuma e a água fica branca; no 2022.3 ele lê a profundidade do quadro anterior,
então quadros parados parecem certos, mas objetos em movimento deixam rastro.

Achados: «Lê _CameraDepthTexture» (`SHADER.DEPTH_TEXTURE`), «Transparente, lê ...» (`SHADER.DEPTH_TEXTURE_TRANSPARENT`).

## O que o Urpify faz

- **Free e Pro:** liga a Depth Texture em cada URP Asset e, para um leitor transparente, define o Depth Texture Mode do
  renderer como After Opaques. Um shader que o URP desenha como está passa a funcionar sem mudar o código: o estande D01 é
  corrigido só pelo pacote gratuito.
- **Pro, quando o shader é convertido:** a declaração `sampler2D _CameraDepthTexture` do autor é trocada por
  `DeclareDepthTexture.hlsl` do URP, e as leituras (`tex2D`, `tex2Dproj`, `SAMPLE_DEPTH_TEXTURE`,
  `SAMPLE_DEPTH_TEXTURE_PROJ`) viram `SampleSceneDepth`. Assim o shader também funciona com XR single-pass instanced,
  onde a textura de profundidade é um array. `LinearEyeDepth`, `Linear01Depth` e `COMPUTE_EYEDEPTH` vêm da biblioteca do
  Urpify.

## Quão perto

`Auto`. Se uma leitura não puder ser reescrita, a declaração do autor fica, e o achado diz que o XR single-pass instanced
não tem suporte para esse shader.
