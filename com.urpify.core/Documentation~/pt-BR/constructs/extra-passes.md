# Passadas extras: contornos, cascas, camadas

![Estande S12: um shader toon com uma passada de contorno de casco invertido](../../images/S12.jpg)

*Estande S12: um surface shader toon com uma passada de contorno escrita à mão. Só depois do conversor da Unity, sobra
dele um contorno em volta de uma esfera rosa.*

## O que quebra

O URP desenha no máximo duas passadas de um SubShader por objeto: uma `SRPDefaultUnlit` e uma `UniversalForward`, nessa
ordem. O Built-in desenha todas as passadas. Um shader com uma passada de contorno ao lado da passada de iluminação, cascas
de pelo, camadas de hachura ou duas passadas sem tag LightMode mantém só uma delas, muitas vezes sem aviso.

Achados: «Passadas extras ao lado de um programa surface» (`SHADER.SURFACE.EXTRA_PASS`), «Passadas sem LightMode: N»
(`SHADER.PASS.MULTIPLE_UNTAGGED`).

## O que o Urpify faz (Pro)

- **Duas passadas desenhadas:** a primeira vira `SRPDefaultUnlit` e a segunda `UniversalForward`, então a ordem de desenho
  do Built-in se mantém. Conferido no URP 14 e 17.
- **Três ou mais passadas desenhadas:** a partir da terceira, as passadas recebem as tags `UrpifyPass3`, `UrpifyPass4` e
  assim por diante. O Renderer Feature Built-in Camera Hooks do Urpify as desenha depois dos objetos opacos ou
  transparentes do URP, as passadas de cada objeto em ordem. Até 32 passadas desenhadas por SubShader.
- **Passadas escritas à mão ao lado de um programa surface** são mantidas e convertidas pelas regras de vert/frag.
- Shaders de efeitos de imagem que o `Graphics.Blit` desenha não recebem tags: as passadas deles são escolhidas por
  índice.

O Renderer Feature é adicionado na etapa Conversores do Urpify a cada renderer do URP que precisa dele.

## Quão perto

`Auto` para duas passadas, `Automático, aproximado` para três ou mais. Se houver vários desses objetos na tela, as duas
primeiras passadas de cada objeto são desenhadas antes das passadas extras de qualquer objeto, o que pode mudar a ordem
de objetos sobrepostos.
