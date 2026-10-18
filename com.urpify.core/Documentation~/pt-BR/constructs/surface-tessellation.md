# Tesselação

![Estande S09: tesselação com deslocamento](../../images/S09.jpg)

*Estande S09: um surface shader com `tessellate:` e deslocamento.*

## O que quebra

`tessellate:func` faz a Unity gerar estágios hull e domain para um surface shader. O URP não tem surface shaders, então o
objeto fica rosa.

Achado: «Tesselação (...)» (`SHADER.SURFACE.TESSELLATION`).

## O que o Urpify faz (Pro)

O conversor gera os estágios hull e domain com um modelo que segue a pipeline embutida: a função de tesselação do autor dá
os fatores, particionamento `fractional_odd`, e depois a função `vertex:` desloca os novos vértices. Cada passada que
desenha o objeto (forward, sombras, profundidade) é tesselada, então a sombra acompanha a superfície deslocada.
`tessphong:` adiciona a suavização de Phong com a intensidade indicada.

Shaders vert/frag que escrevem os próprios estágios hull e domain os mantêm; os atributos que o Built-in define
(`UNITY_domain`, `UNITY_partitioning`, `UNITY_outputtopology`, `UNITY_patchconstantfunc`, `UNITY_outputcontrolpoints`)
vêm da biblioteca do Urpify, e a verificação de compilação cobre os estágios hull e domain.

## Quão perto

`Automático, aproximado`: os mesmos fatores e o mesmo deslocamento, com a iluminação do URP por cima.
