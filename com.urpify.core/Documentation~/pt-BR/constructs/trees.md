# Árvores do Tree Creator e de soft occlusion

O laboratório não tem estande para árvores; elas foram conferidas no ShaderTutorials (ronja-tutorials) com as árvores do
Nature Starter Kit.

## O que quebra

O URP não tem os shaders do Tree Creator (`Nature/Tree Creator Bark`, `Leaves`, `Leaves Fast`, `Optimized`) nem os
shaders de árvores de soft occlusion. Árvores feitas com eles ficam rosa. Grama e malhas de detalhe do terreno não são
afetadas: o URP as desenha com os próprios shaders de detalhe.

Achado: «..., sem versão para URP» (`MATERIAL.BUILTIN_TREE`).

## O que o Urpify faz (Pro)

A Unity publica o código dos seus shaders embutidos sob a licença MIT. O Urpify Pro converte os 8 shaders de árvores da
própria Unity com o conversor normal, acrescenta à sua biblioteca as funções de `TerrainEngine.cginc` de que eles precisam
(vento, flexão, billboards) e leva os materiais de casca e folhas, inclusive dentro de prefabs, para os shaders
convertidos. O texto da licença está em `Third Party Notices.md` do Pro.

## Quão perto

`Automático, aproximado`. As árvores deixam de ser rosa e mantêm os materiais. A animação do vento e os billboards no URP
foram vistos em alguns segundos de quadros, não medidos pixel a pixel; compare as suas cenas na comparação de cenas.
Assets de SpeedTree são atualizados pelo conversor da Unity, não pelo Urpify.
