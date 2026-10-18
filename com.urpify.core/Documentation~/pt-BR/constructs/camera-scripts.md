# Scripts que renderizam câmeras: espelhos, portais, reflexos da água

![Estande M01: um espelho plano, preto depois do conversor da Unity, volta a refletir depois do Urpify](../../images/M01.jpg)

*Estande M01: um espelho plano escrito como fazem MirrorReflection e Standard Assets Water: `OnWillRenderObject`,
`Camera.current`, uma câmera de reflexo com plano próximo oblíquo e `Camera.Render`. Só depois do conversor da Unity, o
espelho fica preto.*

## O que quebra

O URP nunca chama `OnPreCull`, `OnPreRender` e `OnPostRender` de uma câmera, e chama `OnWillRenderObject` durante o
culling, onde nenhuma outra câmera pode renderizar. Portais e espelhos que renderizam uma segunda câmera nesses métodos
mostram uma superfície lisa ou preta. Não há erro. Conferido no URP 14 e 17.

Achados: «OnPreCull numa câmera» e outros (`SCRIPT.CAMERA_CALLBACKS`), «OnWillRenderObject renderiza uma câmera»
(`SCRIPT.WILLRENDER_CAMERA`).

## O que o Urpify faz (Pro)

O script mantém os métodos e a lógica. O Urpify o registra nos eventos de câmera do URP:

- `OnPreCull` e `OnPreRender` são chamados a partir de `RenderPipelineManager.beginCameraRendering` da câmera deles,
  `OnPostRender` a partir de `endCameraRendering`.
- `OnWillRenderObject` é chamado antes do culling da câmera do URP para os renderers que a câmera vê, com `Camera.current`
  apontando para essa câmera.
- `Camera.Render()` dentro deles renderiza no quadro do URP por um pedido de render, então a textura do reflexo ou do
  portal está pronta antes de a câmera que a mostra desenhá-la.
- `OnRenderObject` é chamado depois dos objetos opacos e transparentes da câmera.

O catálogo do Urpify reconhece scripts conhecidos: Portals do SebLague, MirrorReflection, Standard Assets Water e a câmera
de normais do ToonWaterShader são convertidos com receitas conferidas nesses projetos. Os outros usam o mesmo mecanismo e
recebem «Automático, aproximado».

## Quão perto

`Auto` para as receitas conferidas do catálogo, `Automático, aproximado` para os outros: confira essas cenas na comparação
de cenas. Uma diferença do Built-in: `OnWillRenderObject` é chamado para renderers dentro do frustum da câmera sem
occlusion culling, então um espelho escondido atrás de uma parede renderiza o reflexo mesmo assim.

O código ligado aos delegates `Camera.onPreCull`, `Camera.onPreRender` e `Camera.onPostRender` ainda não é convertido; o
achado diz como movê-lo. Um `OnRenderImage` que renderiza uma câmera não roda no URP, veja
[O que não é convertido](../not-converted.md).
