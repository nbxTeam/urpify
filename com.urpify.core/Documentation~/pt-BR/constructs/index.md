# Construções

O Urpify suporta construções, não shaders específicos: um shader é convertido quando todas as construções que ele usa são
suportadas; senão, a análise nomeia a que falta. Cada página abaixo explica uma construção da Built-in Render Pipeline: o
que quebra no URP, o que o Urpify faz a respeito e quão perto o resultado chega.

As imagens vêm do laboratório de testes do Urpify, uma cena com um estande próprio para cada construção. Cada uma mostra o
mesmo estande três vezes: no Built-in, no URP só depois do Render Pipeline Converter da Unity, e no URP depois do conversor
da Unity e do Urpify. São os quadros que as próprias etapas de captura do Urpify fizeram, recortados no estande.

| Construção | Página | Estande | Quem corrige |
|---|---|---|---|
| Surface shaders com modelos de iluminação embutidos | [Surface shaders](surface-shaders.md) | S01–S04 | Pro |
| Funções de iluminação próprias | [Iluminação própria](surface-custom-lighting.md) | S05 | Pro |
| Opções de `#pragma surface`: `vertex:`, `finalcolor:`, `decal:`, `alphatest:`, `addshadow` e outras | [Opções de surface](surface-options.md) | S03, S06–S08, S10, S11 | Pro |
| Tesselação de surface shaders | [Tesselação](surface-tessellation.md) | S09 | Pro |
| Passadas extras: contornos, camadas de pelo, 3 ou mais passadas | [Passadas extras](extra-passes.md) | S12 | Pro |
| Shaders vert/frag sem iluminação | [Vert/frag sem iluminação](vertfrag-unlit.md) | V01 | Nada a fazer |
| Shaders vert/frag iluminados: ForwardBase, ForwardAdd, sombras, vertex-lit | [Vert/frag iluminados](vertfrag-lit.md) | V02 | Pro |
| GrabPass | [GrabPass](grabpass.md) | G01, G02 | Pro |
| `_CameraDepthTexture` | [Textura de profundidade](depth-texture.md) | D01 | Free e Pro |
| `_CameraDepthNormalsTexture`, shaders de substituição | [Depth-normals e shaders de substituição](depth-normals.md) | nenhum | Pro |
| Efeitos de imagem `OnRenderImage` | [Efeitos de imagem](image-effects.md) | C02 | Pro |
| `CommandBuffer` em eventos de câmera | [Command buffers](command-buffers.md) | C03 | Pro |
| Scripts que renderizam câmeras: espelhos, portais, reflexos da água | [Scripts de câmera](camera-scripts.md) | M01 | Pro |
| Componentes `Projector` | [Projetores](projectors.md) | C01 | Pro |
| `Shader.Find` com nomes de shaders embutidos | [Shader.Find no código](shader-find.md) | C04 | Free |
| O que o conversor da Unity quebra: Standard Transparent, Self-Illumin, materiais embutidos, materiais de pacotes, Post-processing v2, texturas do URP Asset, Material Variants | [Correções depois do conversor da Unity](after-unity-converter.md) | STD04, LEG03 | Free |
| O que o conversor da Unity faz: Standard, Legacy, Mobile, partículas, terreno, modelos importados | [O conversor da Unity](unity-converter.md) | STD01–STD06, LEG01–LEG05, PAR01, PAR02, FBX01, FBX02, TER01 | Unity |
| Árvores do Tree Creator e de soft occlusion | [Árvores](trees.md) | nenhum | Pro |
| Um arquivo de shader para Built-in e URP | [Manter suporte ao Built-in](keep-builtin-support.md) | todos os estandes de shader | Pro |

As construções que o Urpify não converte estão, com os motivos, em [O que não é convertido](../not-converted.md).
