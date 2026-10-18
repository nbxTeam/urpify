# Perguntas frequentes

## O Urpify muda o meu projeto ao analisar?

Não. A análise só lê arquivos. Só fazem mudanças as etapas da janela de migração que avisam isso e o botão Converter do
painel do shader, sempre com backup antes.

## Quanto do meu projeto vai ser convertido?

A análise diz antes de qualquer mudança: a visão geral conta os assets quebrados por quem os corrige.
A página do produto mostra o que o Urpify converteu num teste independente com shaders que os desenvolvedores não
viram.

## Posso desfazer uma migração?

Sim. **Reverter tudo** restaura cada arquivo que a migração mudou e move para o backup os arquivos que ela criou.
Arquivos que você mudou depois da migração e arquivos que apareceram entre as etapas ficam como estão e são listados. Uma
etapa interrompida é revertida como uma concluída. Mesmo assim, faça commit do projeto antes de começar: é no commit que
você mais confia.

## Preciso do Pro?

O pacote gratuito encontra tudo o que quebra, roda o conversor da Unity de forma confiável, corrige o que o conversor da
Unity erra, compara cenas antes e depois e reverte. O Pro converte o que o pacote gratuito não consegue: shaders próprios,
efeitos de imagem, scripts de câmera, command buffers e projetores. A visão geral mostra, antes da compra, quantos dos
seus assets quebrados precisam do Pro.

## O Urpify reescreve o código do meu shader?

Só a ligação. As funções do autor (`surf`, `vert`, `frag`, funções de iluminação, auxiliares) são copiadas como estão; o
que muda é como o shader recebe luzes, sombras, profundidade, a imagem da tela e as matrizes. Os nomes do Built-in
continuam funcionando pela biblioteca de compatibilidade do Urpify. O nome do shader e os nomes das propriedades ficam,
então os materiais mantêm os valores.

## E se um shader convertido ficar errado?

Cada shader convertido é renderizado no Built-in e no URP numa esfera com os seus materiais. Se ficar diferente, ele é
marcado como `Revisar`, e o par de imagens fica em `Urpify/Reports/self-check/`. Abra-o no painel do shader: a aba Prévia
mostra Built-in e URP lado a lado, a aba Código mostra cada mudança com o motivo. Ali você pode editar o resultado e
recompilar, ou reverter o shader junto com a migração.

## Funciona com a minha versão da Unity?

Testado no Unity 2022.3 LTS, 6000.0 LTS, 6000.3 LTS, 6000.5 e 6000.6; 6000.1, 6000.2 e 6000.4 não foram testados. Veja [Versões da Unity e do URP](urp-version-matrix.md).

## Funciona em CI?

Sim. Cada etapa roda pela linha de comando e grava um resultado JSON, veja [Linha de comando](cli.md).

## Manter suporte ao Built-in deixa os builds maiores?

Um pouco. Na bancada (Unity 6.5, Mono), os dados do jogo tinham 32,5 MB no Built-in, 38,2 MB no URP com shaders só de URP
e 38,5 MB com shaders para as duas pipelines: 0,34 MB, ou 0,9 %, a mais.

## Efeitos de imagem e projetores convertidos custam mais no URP?

Eles rodam pelo Renderer Feature do Urpify, que grava as mesmas chamadas de desenho no quadro do URP. Um projetor custa
cerca de 0,02 ms por quadro numa cena pequena; com 5000 renderers visíveis, cerca de 2,2 ms. Jogos compilados mantêm os
efeitos, em builds Mono e IL2CPP.

## O que o Enviar relatório envia?

Nada até você clicar em Enviar. Relatório > Enviar relatório... empacota o shader selecionado, os arquivos include dele,
os achados, as versões da Unity, do URP e do Urpify, os erros do compilador e, se você informar, o seu comentário e
contato. A janela lista cada arquivo antes do envio. **Enviar** sobe o arquivo para o nosso canal de suporte; **Salvar
zip** salva na sua máquina. Os arquivos só são usados para entender o que falta ao conversor e depois são apagados. É a
única chamada de rede do Urpify. No máximo um relatório por minuto.

## Posso vender shaders que o Urpify converteu?

Sim, sob as licenças deles. Veja [Redistribuir shaders convertidos](../license-redistribution.md) (em inglês). Um shader
cujo cabeçalho indica uma licença não comercial continua sob ela; o painel do shader mostra a linha da licença.

## Por que a análise diz que o meu projeto já estava quebrado antes da migração?

Porque estava: um shader que não compila no Built-in, scripts que não compilam, um arquivo include que não está no
projeto, um material que não está no projeto. O Urpify mostra isso à parte porque a migração não vai corrigir. Corrija
primeiro e analise de novo.

## Depois de reverter o URP, os shaders mostram erros de include uma vez. Por quê?

Depois de remover o URP de um projeto que o tinha (reversão ou volta manual), a primeira sessão do Editor ainda pode
compilar os SubShaders de URP de shaders feitos com Manter suporte ao Built-in e registrar erros de include, por
resultados de importação que ficaram em `Library/Artifacts`. Na segunda sessão eles somem. É o comportamento de
importação da Unity, não dos arquivos de shader: num projeto que nunca teve URP não há erros. Abra o projeto mais uma vez
ou rode Assets > Reimport All.

## A janela está em outro idioma.

Project Settings > Urpify > Pessoal > Idioma. Por padrão o idioma segue o do Editor e usa inglês quando o Urpify não tem
esse idioma.
