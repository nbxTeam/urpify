# Versões da Unity e do URP

| Unity | URP | Status | Testado com |
|---|---|---|---|
| 6000.6 | 17.6 | Suportado | 6000.6.5f1 com URP 17.6.0: testes unitários, 37 estandes, a demonstração, 4 projetos reais |
| 6000.5 | 17.5 | Suportado | 6000.5.9f1 com URP 17.5.0: testes unitários, 37 estandes, projetos reais, corpus de shaders |
| 6000.4 | 17.4 | Não testado | |
| 6000.3 LTS | 17.3 | Suportado | 6000.3.26f1 com URP 17.3.0: testes unitários, 37 estandes, a demonstração, 4 projetos reais. O conversor em lote do próprio Unity falha nesta versão; o Urpify contorna isso (abaixo) |
| 6000.1, 6000.2 | 17.1, 17.2 | Não testado | |
| 6000.0 LTS | 17.0 | Suportado | 6000.0.84f1 com URP 17.0.4: testes unitários, 37 estandes, a demonstração, 4 projetos reais |
| 2022.3 LTS | 14 | Suportado | 2022.3.62f3 com URP 14.0.12: o mesmo conjunto do 6000.5 |
| 2021.3 LTS | 12 | Sem suporte na 1.0 | Planejado se houver pedidos |

Migre na versão do Editor com que você vai publicar: as capturas de antes e depois só são comparáveis dentro da mesma
versão da Unity.

## O conversor do Unity e o código de saída no 6000.0 e no 6000.3

- O URP 17.0 e o 17.3 têm a API em lote antiga do conversor do Unity (`ConverterContainerId`). No URP 17.3.0 (6000.3.26f1) ela
  lança `MissingMethodException` para o abstrato `Base2DMaterialUpgrader` antes de converter qualquer coisa, em qualquer projeto. O
  Urpify então lista ele mesmo os conversores de Built-in para URP e os executa do mesmo jeito; os Detalhes da etapa dizem isso.
- O Unity 6000.0.84f1 e o 6000.3.26f1 com frequência, e o 6000.5.9f1 às vezes, travam ao sair do modo em lote (código de saída 139) depois que o
  trabalho está feito, com ou sem o Urpify. Na linha de comando, leia `Urpify/urpify-<command>-result.txt` (veja [Linha de comando](cli.md)).

## O que muda entre o URP 14 e o URP 17

A biblioteca de shaders do Urpify (`BirpCompat*.hlsl`) e o seu Renderer Feature levam em conta cada diferença abaixo,
então um shader convertido compila e renderiza nas duas versões. Cada linha foi conferida no código dos pacotes URP
14.0.12 (2022.3.62f3) e URP 17.5.0 (6000.5.9f1).

| Tema | URP 14.0.12 | URP 17.5.0 | Onde foi conferido |
|---|---|---|---|
| Luz principal | `GetMainLight()`, `(shadowCoord)`, `(shadowCoord, positionWS, shadowMask)`, `(inputData, shadowMask, aoFactor)` | As mesmas 4 sobrecargas | `ShaderLibrary/RealtimeLights.hlsl` |
| Luzes adicionais | `GetAdditionalLight(i, positionWS)`, `(i, positionWS, shadowMask)`, `(i, inputData, shadowMask, aoFactor)` | As mesmas 3 sobrecargas | `ShaderLibrary/RealtimeLights.hlsl` |
| Loop de luzes Forward+ | `USE_FORWARD_PLUS` | `USE_CLUSTER_LIGHT_LOOP`; `USE_FORWARD_PLUS` continua como alias em `ForwardPlusKeyword.deprecated.hlsl` | `ShaderLibrary/RealtimeLights.hlsl`, `Core.hlsl` |
| Sombras de luzes adicionais | `AdditionalLightRealtimeShadow(lightIndex, positionWS, lightDirection)` | O mesmo, mais uma sobrecarga com parâmetros de sombra e dados de amostragem | `ShaderLibrary/Shadows.hlsl` |
| Harmônicos esféricos | `SampleSH(normalWS)` em `GlobalIllumination.hlsl` do pacote URP | `SampleSH(normalWS)` em `AmbientProbe.hlsl` do pacote Core | `ShaderLibrary` do URP e do Core |
| Lightmaps | `LIGHTMAP_ON`, `DIRLIGHTMAP_COMBINED` | As mesmas keywords | `ShaderLibrary/GlobalIllumination.hlsl` |
| Crossfade de LOD | `LODCrossFade.hlsl` | `LODCrossFade.hlsl` | `ShaderLibrary/` |
| Decals | `DBuffer.hlsl` | `DBuffer.hlsl` | `ShaderLibrary/` |
| Texturas de profundidade, normais e opaca | `DeclareDepthTexture.hlsl`, `DeclareNormalsTexture.hlsl`, `DeclareOpaqueTexture.hlsl` | Os mesmos arquivos | `ShaderLibrary/` |
| Renderer Feature | `ScriptableRenderPass.Execute`; Render Graph interno | Render Graph (`RecordRenderGraph`); desde a Unity 6.4 não há modo de compatibilidade: `RenderGraphSettings.enableRenderCompatibilityMode` sempre retorna false | `Runtime/Passes/ScriptableRenderPass.cs`, `Runtime/Settings/RenderGraphSettings.cs` |
| Opaque Texture para câmeras de prévia | Não é copiada para uma câmera de prévia (`copyColorPass &= !isPreviewCamera`) | É copiada | `Runtime/UniversalRenderer.cs`. Por isso a autoverificação do Urpify no 2022.3 renderiza shaders com GrabPass com uma câmera de jogo. |
| Texturas do URP Asset | Um jogo compilado lança `NullReferenceException` a cada quadro e fica preto quando as `m_Textures` do URP Asset (blue noise, Bayer) estão vazias, como o conversor da Unity as salva | Não observado em jogos do 6000.5 dos mesmos projetos | `UniversalRenderPipeline.cs` (`SetupPerFrameShaderConstants`). O pacote gratuito as preenche depois do conversor da Unity. |

## O que muda no conversor da Unity

| Tema | 2022.3 / URP 14 | 6000.5 / URP 17.5 |
|---|---|---|
| Regras de materiais para `Legacy Shaders/Particles/VertexLit Blended` e `Mobile/Bumped Specular (1 Directional Realtime Light)` | Registradas com nomes de shader antigos, então o conversor da Unity as ignora; o Urpify atualiza esses materiais pela própria tabela | A Unity os atualiza sozinha |
| Modo batch sem `-typesFilter` | Não medido: o Urpify também não usa ali o comando batch da Unity | Lança `NullReferenceException` (`Converters.cs:134`); o Urpify chama o conversor pela API |
| Código de saída do comando batch da Unity depois de um trabalho bem-sucedido | Não medido | 3 de 4 projetos reais terminaram com código 21 ou 139 (o processo trava ao sair); o Urpify julga o resultado pelo log do conversor e por uma nova análise, nas duas versões |
