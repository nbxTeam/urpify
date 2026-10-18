# Onde o URP fica diferente

Algumas diferenças depois de uma migração não são quebras nem erros de conversão: o URP simplesmente desenha o mesmo
material um pouco diferente do Built-in. A comparação de cenas as marca como «Mudou» quando são grandes o bastante, então
vale conhecê-las.

## Standard para URP Lit

![Estande STD01: Standard e URP Lit lado a lado](../images/STD01.jpg)

A conversão da própria Unity de Standard para URP Lit muda um pouco os brilhos especulares e os reflexos: o URP tem outro
modelo de reflexão e outros reflexos do ambiente. Nas nossas cenas de teste, a diferença chega a cerca de 7 % do quadro.
Os limites do Urpify para «Igual» e «Mudou» são calibrados justamente nessa conversão, então ela sozinha não faz uma cena
«mudar».

## Luzes adicionais

O Built-in desenha cada luz adicional numa passada aditiva própria; o URP as soma numa passada, com a sua própria curva
de atenuação e um limite de luzes por objeto (configurável no URP Asset). Cenas com muitas luzes pontuais ficam um pouco
diferentes, e o URP descarta as luzes acima do limite por objeto.

## Bloom

O bloom do URP espalha pontos pequenos e brilhantes de forma mais larga e suave que o Post-processing Stack v2. O Urpify
leva os parâmetros do bloom com uma calibração que aproxima o brilho, mas não o deixa idêntico; o resto é a pirâmide de
bloom diferente, que nenhum parâmetro reproduz.

## Luz ambiente num jogo na Unity 6

Uma cena que pega a luz ambiente do skybox, mas não tem dados de iluminação assados, fica mais escura num jogo na Unity 6
do que no Editor, tanto no Built-in quanto no URP. Isso não é causado pela migração, mas aparece quando você compara o
jogo migrado com o Editor. A análise informa («Luz ambiente do skybox não assada»), e o botão **Assar ambiente** no painel
da cena assa só a luz ambiente e os reflexos, com backup.

## GrabPass com Opaque Texture

Com a origem padrão do GrabPass, os objetos transparentes não aparecem na refração: o URP copia a tela antes de
desenhá-los. Escolha a Origem do GrabPass «Depois dos transparentes» para ficar igual ao Built-in. Veja
[GrabPass](constructs/grabpass.md).

## Objetos transparentes e ordenação

O URP ordena os objetos transparentes do mesmo jeito, mas desenha as passadas extras dos shaders convertidos (a terceira
em diante) depois das duas primeiras passadas de todos os objetos, o que pode mudar a ordem de objetos transparentes
sobrepostos. Veja [Passadas extras](constructs/extra-passes.md).
