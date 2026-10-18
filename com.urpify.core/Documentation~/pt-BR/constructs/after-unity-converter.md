# Correções depois do conversor da Unity

O Render Pipeline Converter da Unity é a primeira coisa que a migração roda, e quase sempre acerta. Estes são os casos em
que o resultado dele é mensuravelmente errado, e o que o pacote gratuito faz em seguida. Todos estão no pacote gratuito,
na etapa Conversores do Urpify.

## Standard no modo Transparent

![Estande STD04: vidro no modo Standard Transparent, branco depois do conversor da Unity, vidro de novo depois do Urpify](../../images/STD04.jpg)

*STD04: vidro no modo Transparent do Standard.*

O conversor da Unity leva o Transparent para a mistura Premultiply do URP, que espera cores já multiplicadas pelo alfa. O
vidro fica quase opaco e branco. O Urpify muda o material para a mistura Alpha com Preserve Specular Lighting, que é como o
Built-in o mistura. Achado: «..., modo Transparent» (`MATERIAL.STANDARD_TRANSPARENT`).

## Legacy Self-Illumin

![Estande LEG03: Legacy Self-Illumin, escuro depois do conversor da Unity, brilha depois do Urpify](../../images/LEG03.jpg)

*LEG03: `Legacy Shaders/Self-Illumin/Diffuse`.*

O conversor da Unity leva esses materiais para o Simple Lit, que não pega emissão do alfa Illumin: o brilho some. O Urpify
os leva para o próprio shader Self-Illumin para URP, que brilha como no Built-in. Achado: `MATERIAL.SELF_ILLUMIN`.

## Materiais embutidos do Editor

Renderers que usam os `Default-Material` ou `Default-Diffuse` embutidos do Editor (um cubo novo, um plano) são apontados
pelo conversor da Unity para o material padrão do URP, que é cinza médio em vez de branco. O Urpify os aponta para cópias
de URP com os valores do material embutido. Achado: `SCENE.BUILTIN_MATERIAL`.

## Materiais dentro de pacotes

Os arquivos de um pacote são somente leitura, então o conversor da Unity nunca muda um material que fica num pacote.
Quando o shader dele é só do Built-in, cada renderer que o usa fica rosa: as paredes do ProBuilder 5 usam
`ProBuilderDefault.mat`, cujo shader `ProBuilder/Standard Vertex Color` é um surface shader. O Urpify aponta esses
renderers, em cenas e prefabs, para uma cópia de URP em `Assets/Urpify/Materials/Packages/`: o atualizador da própria
Unity para um shader embutido (`Auto`); senão, URP Lit com a cor, a textura, a suavidade e o metálico do material
(`Automático, aproximado`: o que o próprio shader acrescenta, como cores de vértice, não é mantido). Achado:
`SCENE.PACKAGE_MATERIAL`.

## Shaders embutidos sem atualizador

Alguns shaders embutidos não têm atualizador no conversor da Unity («No upgrader found»): `Legacy Shaders/Decal`,
`Legacy Shaders/Transparent/VertexLit`, `Transparent/Parallax Diffuse` e `Specular`, e no URP 14 dois atualizadores
registrados com nomes antigos. O Urpify leva esses materiais para o shader de URP mais próximo da própria tabela, com as
propriedades transferidas. Achado: `MATERIAL.BUILTIN_NO_UPGRADER`.

## Post-processing Stack v2

O conversor da Unity transforma volumes e perfis do PPv2 em Volumes do URP, mas no modo batch não espera a busca de assets
de que precisa e não converte nenhum: o bloom some sem erro. A etapa Rodar o conversor da Unity do Urpify espera a busca e
depois transfere os parâmetros do Bloom com uma calibração (threshold e scatter) para o brilho ficar mais perto do
Built-in. O resto da diferença é o próprio bloom do URP, veja [Onde o URP fica diferente](../urp-differences.md). Achado:
`SCENE.PPV2`.

## Configurações do URP Asset

- **Depth Texture e Opaque Texture** são ligadas em cada URP Asset quando um shader que o URP desenha como está as lê (veja
  [Textura de profundidade](depth-texture.md)).
- **Texturas do URP Asset** (URP 14): o conversor da Unity salva os URP Assets dele com as texturas blue noise e Bayer
  vazias. O Editor as preenche; um jogo compilado não: ele lança `NullReferenceException` a cada quadro e fica preto. O
  Urpify as preenche a partir do pacote do URP. Achado: `SETTINGS.URP_ASSET_TEXTURES`.
- **Luz principal**: nos níveis de qualidade com Pixel Light Count 0, o Built-in ainda iluminava por pixel a luz
  direcional principal; o Urpify define ali a Main Light do URP como Per Pixel.

## Material Variants

O conversor da Unity leva um Material Variant para o shader de URP do pai, mas descarta os valores próprios da variante:
ela vira uma cópia silenciosa do pai. O Urpify lê os valores próprios da variante no backup da etapa Rodar o conversor da
Unity e os coloca de volta como substituições das propriedades do URP. Achado: `MATERIAL.VARIANT`.

## Materiais somente leitura dentro de modelos

Materiais embutidos em arquivos FBX são somente leitura, e o módulo do conversor da Unity para eles não rodava de forma
confiável no modo batch. O Urpify roda esse módulo objeto por objeto para os renderers e terrenos que encontrou, nas duas
versões da Unity.
