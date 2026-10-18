# Projetores

![Estande C01: uma mancha de sombra de um projetor, some depois do conversor da Unity, volta depois do Urpify](../../images/C01.jpg)

*Estande C01: um componente `Projector` projeta uma mancha de sombra embaixo de um cubo. Só depois do conversor da Unity,
não há sombra.*

## O que quebra

O URP não renderiza componentes `Projector`. Manchas de sombra, anéis de seleção e decals projetados somem sem aviso.

Achados: «Projector em ...» (`SCENE.PROJECTOR`), «Shader de projetor» (`SHADER.PROJECTOR`).

## O que o Urpify faz (Pro)

O `Projector` continua sendo um projetor, e o shader dele fica como está. O Renderer Feature Built-in Camera Hooks do
Urpify desenha o material de cada projetor ativo sobre os renderers dentro do frustum e das camadas dele, com
`unity_Projector` e `unity_ProjectorClip` definidos por objeto, como no Built-in. Tamanho, orientação, alcance, camadas
ignoradas e material vêm do componente, então você edita os projetores como antes.

O feature mantém um cache dos projetores e dos renderers que eles alcançam, em vez de procurar na cena a cada quadro: os
projetores são atualizados a cada 30 quadros, os renderers a cada 60, e os dois quando uma cena é carregada ou
descarregada. Os renderers são filtrados pelo frustum de cada projetor e da câmera. Medido na Unity 6.5 com um projetor:
0,02 ms por quadro com 88 renderers; com mais 5000 renderers, cerca de 2,2 ms quando todos estão na frente da câmera e
0,09 ms quando estão atrás dela.

Como nada muda no projetor nem no shader dele, um pacote exportado com Manter suporte ao Built-in continua funcionando nas
duas pipelines.

## Quão perto

`Automático, aproximado`: o mesmo shader sobre os mesmos objetos. O Decal Projector do próprio URP não é usado, então a
aparência continua a do Built-in.
