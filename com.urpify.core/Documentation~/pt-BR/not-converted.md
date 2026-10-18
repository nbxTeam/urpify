# O que não é convertido e por quê

O Urpify lista tudo o que não converte, com o motivo, antes de a migração começar. Cada caso abaixo pertence a um de três
grupos:

- **À mão: o projeto já estava quebrado antes da migração.** O Urpify não corrige erros do próprio projeto; ele diz o que
  está quebrado e o que fazer primeiro.
- **À mão: não há nada para converter.** O URP não tem equivalente, conferido no código-fonte do URP.
- **Ainda sem suporte.** É possível, mas esta versão do Urpify não faz. É no que trabalhamos a seguir; o achado diz o que
  fazer à mão enquanto isso.

A parcela de shaders que o Urpify converte, medida num teste independente com shaders que os desenvolvedores não
viram, está na página do produto. Esta página lista o que não é convertido e por quê.

## O projeto já estava quebrado antes da migração

| Caso | O que você vê | O que fazer |
|---|---|---|
| Um shader não compila na pipeline embutida | «O shader já não compila» | Corrija primeiro os erros de compilação no Built-in; os objetos já são rosa. |
| Os scripts do projeto não compilam | «Os scripts do projeto não compilam» | Corrija-os primeiro: eles bloqueiam o Play Mode, as capturas e as etapas que reescrevem scripts. Projetos antigos costumam precisar atualizar chamadas de API obsoletas. |
| Um shader inclui um arquivo que não está no projeto | «Include não encontrado» | Adicione o arquivo ou corrija o caminho e analise de novo. |
| `UsePass` cita um shader que não está no projeto | «UsePass ...: shader não encontrado» | Adicione o shader ou remova a linha `UsePass`. O objeto também é rosa no Built-in. |
| Um renderer usa um material que não está no projeto | «Material ausente em ... renderer(s)» | Atribua um material ou recupere o material ou pacote que falta. Acontece quando a Unity atualiza um pacote (por exemplo, o ProBuilder) cujo material padrão antigo não existe mais. |

## Não há nada para converter

| Caso | Por quê |
|---|---|
| Shaders internos do renderer embutido: `Internal-DeferredShading`, `Internal-DeferredReflections`, `Internal-ScreenSpaceShadows`, `Internal-DepthNormalsTexture`, `Internal-MotionVectors`, `Internal-PrePassLighting`, `Internal-Flare`, `Internal-Halo` e as cópias deles | O Built-in os roda a partir dos slots de shaders embutidos do Graphics Settings. O URP nunca lê esses slots: o código do URP 14.0.12 e 17.5.0 nunca chama `GraphicsSettings.GetCustomShader` nem usa `BuiltinShaderType`. O URP renderiza iluminação deferred, sombras, depth-normals e vetores de movimento com shaders próprios. |
| Passadas com LightMode `Deferred`, `PrepassBase`, `PrepassFinal` | Os caminhos deferred e deferred legado do Built-in. O URP renderiza o objeto na sua passada forward, que o Urpify constrói a partir das passadas forward do shader. |
| Funções `finalgbuffer:` de surface shaders | Só rodam na passada G-buffer deferred do Built-in; o shader forward convertido não as chama. |
| `Camera.depthTextureMode` | O URP ignora. O URP gera as texturas de profundidade e normais pelas configurações do URP Asset, e o Urpify as liga quando um shader as lê. |

## Ainda sem suporte

| Construção | O que acontece agora | O que fazer à mão |
|---|---|---|
| Efeitos alimentados por `Light.AddCommandBuffer` (luz volumétrica e semelhantes) | O efeito falta no URP; o resto do quadro renderiza. | Mova os comandos da luz para um Renderer Feature em `BeforeRenderingShadows` ou `AfterRenderingShadows`; o URP renderiza shadow maps de outro jeito, então confira o que os comandos esperam. |
| Efeitos de imagem cujo `OnRenderImage` chega a uma renderização de câmera, um dispatch de compute ou outro desenho imediato | O efeito falta no URP; o resto do quadro é desenhado. | Tire essa chamada do efeito: para `OnPreRender`, ou para um command buffer executado com `ImageEffectGraphics.ExecuteCommandBuffer`. |
| Efeitos de imagem cujos shaders leem o G-buffer do Built-in (`_CameraGBufferTexture0-3`: reflexos em espaço de tela e similares) | O efeito falta no URP; o resto do quadro é desenhado. | Refaça as entradas com as texturas de profundidade, normais e opaca do URP, ou use o renderer Deferred do URP com o G-buffer dele. |
| Delegates `Camera.onPreCull`, `Camera.onPreRender`, `Camera.onPostRender` | O código ligado a eles para de rodar. | Mova-o para `RenderPipelineManager.beginCameraRendering` ou `endCameraRendering`. |
| `OnRenderImage` que desenha imediatamente (`Graphics.DrawMeshNow` e semelhantes) | Falta o efeito. | Desenhe com um command buffer (`CommandBuffer.Blit`, `DrawMesh`) e rode-o com `ImageEffectGraphics.ExecuteCommandBuffer`. |
| `OnRenderImage` que renderiza uma câmera (`Camera.Render`, também por um método auxiliar) | O efeito não roda no URP: dentro do quadro da própria câmera nenhuma outra câmera pode renderizar, e o quadro sairia preto. | Renderize a outra câmera em `OnPreRender` ou `OnPreCull` e deixe em `OnRenderImage` só as chamadas de Blit. Um Blit que testa o stencil buffer da câmera também não o vê no URP. |
| Command buffers nos eventos de iluminação deferred do Built-in de uma luz | Faltam. | Um Renderer Feature no evento correspondente do URP. |
| Shaders que leem o G-buffer do Built-in (`_CameraGBufferTexture0` a `3`), como GI em espaço de tela | O efeito perde a entrada sem nenhum erro: o renderer Forward do URP não tem G-buffer, e o caminho Deferred do URP empacota o seu de outro jeito (`_GBuffer0` a `_GBuffer6`). | Reconstrua as entradas com as texturas de profundidade, normais e opaca do URP, ou mude o renderer do URP para Deferred e reescreva as leituras para `UnityGBuffer.hlsl`. |
| Vários GrabPass com texturas diferentes num shader | O shader não é convertido. | O URP tem uma Opaque Texture: junte as capturas ou mantenha uma e leia-a duas vezes. |
| Mais de 32 passadas desenhadas num SubShader | Não é convertido. | Junte passadas ou mova algumas para um SubShader ou material próprio. |
| `UsePass` de um shader cuja passada está num bloco `CGINCLUDE` | Não é convertido. | Copie a passada para o shader. |
| Um include embutido para o qual o Urpify ainda não tem equivalente | «Stopped: ... includes ...», o shader não é convertido. | Porte à mão as funções do include ou envie o shader com Enviar relatório. |
| Uma passada `ForwardAdd` que lê diretamente algumas variáveis de luz do Built-in, ilumina por vértice ou não devolve uma cor | Não é convertida. | Reescreva a passada com `UNITY_LIGHT_ATTENUATION` ou mova-a para o loop de luzes adicionais do URP. |
| Uma função de iluminação com uma assinatura que o conversor não conhece | Não é convertida. | Use uma das assinaturas `Lighting<Name>` documentadas do Built-in. |
| Passadas da pipeline de função fixa (sem programa de shader) | Não são convertidas: o URP só desenha passadas com programa. | Reescreva a passada como um programa vert/frag. |
| Um Material Variant cujo arquivo não está no backup antes do conversor da Unity | Os valores próprios dele se perdem no conversor da Unity. | Migre com as etapas do Urpify, que guardam esse arquivo, ou defina os valores de novo. |
| Um shader ou script convertido que não compila | O original é restaurado, com a mensagem do compilador no achado. | Corrija o resultado no painel do shader e salve com Salvar como, ou envie o shader com Enviar relatório. |

## Convertido, mas não idêntico

Isto é convertido e funciona, com uma diferença que é do próprio URP, não da conversão. Imagens em
[Onde o URP fica diferente](urp-differences.md).

- **GrabPass com Opaque Texture** (padrão): o URP copia a tela antes dos objetos transparentes, então eles não aparecem na
  refração. Escolha a Origem do GrabPass «Depois dos transparentes» para copiar depois.
- **Funções de iluminação próprias e passadas ForwardAdd:** a função do autor roda para a luz principal e cada luz
  adicional numa só passada, com as sombras e a atenuação do URP.
- **Bloom do Post-processing Stack v2:** vira o bloom do URP, cujo brilho é mais suave e largo; a pirâmide de bloom do URP
  espalha de outro jeito os pontos pequenos e brilhantes.
- **Standard para URP Lit** (a conversão da própria Unity): difere em até cerca de 7 % do quadro nas nossas cenas de
  teste.
- **Reflexos em `OnWillRenderObject`** são chamados antes do culling do URP para os renderers que a câmera vê, sem
  occlusion culling, então um reflexo atrás de uma parede é renderizado mesmo assim.
