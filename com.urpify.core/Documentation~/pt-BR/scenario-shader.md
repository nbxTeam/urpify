# Converter um shader

Você escreve ou mantém shaders e quer um deles no URP, sem mexer na fórmula.

## Abrir o shader

Qualquer uma destas opções abre o painel do shader:
- selecionar o shader na lista da janela do Urpify depois de uma análise;
- clicar com o botão direito no shader na janela Project > Urpify > Analyze;
- arrastar um arquivo `.shader` para a janela do Urpify, da janela Project ou direto do disco. Um arquivo do disco fica
  onde está; nada é importado.

## Ler o código

A aba **Código** mostra o original à esquerda e o shader convertido à direita. As linhas alteradas estão marcadas; passe o
mouse sobre uma para ver por que ela mudou: uma passada com uma nova tag para o URP, um include do Built-in substituído,
propriedades do material movidas para `UnityPerMaterial` para o SRP Batcher.

O que o Urpify muda e o que ele deixa como está:
- O nome do shader e os nomes das propriedades ficam, então os materiais continuam funcionando e mantêm seus valores.
- As funções do autor (`surf`, `vert`, `frag`, funções de iluminação, auxiliares) são copiadas como estão. Só muda a
  ligação: como o shader recebe luzes, sombras, profundidade, a imagem da tela e as matrizes.
- Nomes do Built-in como `UnityObjectToClipPos`, `_LightColor0` ou `UNITY_LIGHT_ATTENUATION` continuam funcionando pela
  biblioteca de compatibilidade do Urpify, `BirpCompat.hlsl`, que os leva ao URP.

**Editar resultado** deixa você mudar o código convertido e **Compilar** com as keywords que os materiais dele usam; os
erros aparecem nas linhas. **Salvar como...** grava o resultado num arquivo novo; **Converter** substitui o original
depois de fazer backup dele em `Urpify/Backup/ShaderPanel`.

Quando o conversor não consegue terminar, a coluna da direita mostra um rascunho somente leitura com a linha onde ele
parou e o motivo. Você pode salvar o rascunho para terminar à mão; Converter e a migração nunca o usam.

## Prévia

A aba **Prévia** renderiza o shader numa esfera, num cubo, num plano ou na sua própria malha, no Built-in e no URP, com os
mesmos valores de material nos dois. **Visualização** alterna entre Built-in, URP, uma cortina com controle deslizante e
um mapa de diferenças. **Direção da luz** gira a luz principal. Mude uma propriedade do material e as duas prévias
acompanham.

A prévia do Built-in desliga a render pipeline no momento do render e a liga de volta, numa única chamada. Se isso falhar,
o painel muda para **Das capturas** e mostra as capturas das cenas. Para um arquivo já convertido, a metade do Built-in
renderiza o original a partir do backup da migração.

## Conferir o resultado

Cada shader que a migração converte também é conferido por um render: uma esfera com os materiais do próprio projeto para
esse shader, no Built-in antes da conversão e no URP depois. Quando mais de 25 % da esfera difere, o shader é convertido
mesmo assim, mas marcado como `Revisar`, e o par de imagens fica em `Urpify/Reports/self-check/`. Um shader cujo código
convertido não compila volta a ser como era e é informado.

## Quando a conversão para

O painel nomeia a construção que o conversor não suporta e o que fazer. [O que não é convertido](not-converted.md) lista
todas. Se o shader deveria converter e não converte, **Relatório > Enviar relatório...** empacota o shader, seus arquivos
include e os achados num arquivo: salve-o ou envie para nós (só quando você clicar em Enviar).

## Licença do shader

Quando o arquivo informa uma licença no cabeçalho, o painel a mostra. Uma cópia convertida continua sob a mesma licença;
com uma licença não comercial, ela não pode ser vendida.
