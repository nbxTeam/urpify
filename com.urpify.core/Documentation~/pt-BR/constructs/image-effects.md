# Efeitos de imagem: OnRenderImage

![Estande C02: um efeito de imagem com dessaturação e vinheta, some depois do conversor da Unity, volta depois do Urpify](../../images/C02.jpg)

*Estande C02: um pós-efeito com `OnRenderImage`, dessaturação com vinheta. Só depois do conversor da Unity, o efeito some,
sem nenhum erro.*

## O que quebra

O URP nunca chama `OnRenderImage`, então todo efeito de imagem escrito para o Built-in para de funcionar. Nem erro nem
rosa: o jogo só fica diferente. Conferido no URP 14 e 17.

Achado: «OnRenderImage ({0} chamadas de Blit)» (`SCRIPT.ONRENDERIMAGE`), gravidade «Quebra sem erro». O shader do efeito
recebe o achado «Shader de efeito de imagem».

## O que o Urpify faz (Pro)

O script continua sendo um MonoBehaviour na câmera e continua servindo de painel de controle: os campos dele, os valores
que ele define a cada quadro e os materiais não mudam. O Renderer Feature Built-in Camera Hooks do Urpify chama o
`OnRenderImage` do script no ponto do quadro do URP escolhido nas configurações («Inserir efeitos de tela em»: por padrão,
depois do pós-processamento), com a cor da câmera como origem. As chamadas de `Graphics.Blit` do script passam por
`ImageEffectGraphics`, que as grava no quadro do URP com os mesmos materiais, passadas e render textures temporárias, na
mesma ordem. Efeitos com `[ImageEffectOpaque]` rodam depois do céu, como no Built-in. Vários efeitos numa câmera mantêm a
ordem. O desenho com `GL` dentro de `OnRenderImage` (quads, matrizes, limpezas) também é gravado.

O shader do efeito fica como está: `vert_img`, `v2f_img` e `_MainTex` funcionam, porque o efeito continua passando a tela
em `_MainTex`, como no Built-in.

## Quão perto

`Auto` para um único Blit, `Automático, aproximado` para cadeias e command buffers. Um efeito que desenha imediatamente com
`Graphics.DrawMeshNow` ou semelhantes, ou que renderiza uma câmera (`Camera.Render`), não pode ser gravado no quadro do
URP: o achado diz como reorganizá-lo.

As chamadas `Graphics.Blit` de outros scripts que o efeito chama (um efeito ScriptableObject por trás do script da
câmera, um gerenciador que roda uma cadeia) são gravadas do mesmo jeito. Um efeito cuja cadeia renderiza uma câmera,
dispara um compute shader ou lê o G-buffer do Built-in (`_CameraGBufferTexture0-3`) fica como está (ainda sem suporte):
no URP ele falta e o resto do quadro é desenhado. Se o quadro de uma cena ficar quase de uma cor só depois da migração, os
efeitos dessa cena são marcados para revisão: desligue-os na câmera para achar a causa, ou reverta.

Jogos compilados mantêm o efeito: tudo o que o Urpify chama pelo nome é protegido contra o stripping de código, conferido
em builds Mono e IL2CPP.
