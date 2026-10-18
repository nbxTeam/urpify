# O que o conversor da Unity faz

O Render Pipeline Converter da Unity atualiza materiais que usam os shaders embutidos da própria Unity. O Urpify o roda
como a etapa Rodar o conversor da Unity da migração, com os ajustes de que ele precisa para rodar de forma confiável a
partir de um script, e informa o log dele.

![Estande STD01: Standard metálico com normal map e metallic map](../../images/STD01.jpg)

*STD01: Standard, fluxo metálico, normal map e metallic map. O conversor da Unity o atualiza para URP Lit.*

![Estande TER01: um terreno com duas camadas](../../images/TER01.jpg)

*TER01: um terreno com duas camadas passa para o shader de terreno do URP.*

O que o conversor cobre, medido nos estandes STD01 a STD06, LEG01 a LEG05, PAR01, PAR02, FBX01, FBX02 e TER01:

- Standard e Standard (Specular setup) nos modos Opaque, Cutout e Fade, com normal maps, emissão e mapas de detalhe. O modo
  Transparent é a exceção, veja [Correções depois do conversor da Unity](after-unity-converter.md).
- Legacy Shaders (Diffuse, Specular, Bumped, Reflective, Transparent, Cutout, Lightmapped, Parallax, VertexLit); a exceção
  é o Self-Illumin.
- Shaders Mobile, Unlit/Texture, Particles/Standard Surface e Unlit, partículas legadas.
- Nature/Terrain/Standard e o material de terreno padrão; SpeedTree.
- Materiais embutidos em modelos importados.
- Clipes de animação que animam propriedades de materiais do Built-in (a análise os lista; o Urpify ainda não confere o
  resultado).
- Post-processing Stack v2, veja [Correções depois do conversor da Unity](after-unity-converter.md).

A análise do Urpify lista esses materiais como corrigidos pelo conversor da Unity, então a visão geral os conta separados
do que precisa do Urpify.

## Rodar de forma confiável

Na Unity 6.5, o comando batch documentado da Unity lança `NullReferenceException` sem `-typesFilter`, e o Editor costuma
travar ao sair depois de um trabalho bem-sucedido (códigos de saída 21, 139). O Urpify chama o conversor pela API, julga o
resultado pelo log do conversor e por uma nova análise em vez do código de saída, e inclui o log no relatório.
