# A janela do Urpify

`Window > Rendering > Urpify`. A janela mantém os próprios textos curtos; esta página explica o que significam os ícones,
contadores e marcas. Passe o mouse sobre qualquer ícone ou contador da janela para ver a mesma explicação.

## Ícones de gravidade

Cada ícone tem uma forma própria, então a cor nunca é a única pista.

| Ícone | Nome | O que acontece depois de mudar para o URP |
|---|---|---|
| Quadrado preenchido (rosa) | Fica rosa | O objeto é renderizado com o shader de erro do URP. Num build de release ele fica invisível. |
| Triângulo preenchido (âmbar) | Quebra sem erro | Nem erro nem rosa, mas o efeito sumiu ou está errado: um portal vira uma parede cinza, a água perde a espuma, um pós-efeito desaparece. |
| Losango preenchido (azul) | Fica diferente | Renderiza, com uma diferença visível, como uma cena mais escura. |
| Anel (cinza) | Sem mudança visível | Nada a fazer, ou o conversor da Unity resolve como está. |

Uma linha da lista, uma cena e um asset recebem o ícone do seu pior achado.

## A faixa abaixo da barra de ferramentas

Números em grupos com nome. Antes da migração: `Cenas` e `Assets`, cada um com um ícone e um número por gravidade:
quantas cenas têm objetos com essa gravidade e quantos assets a têm. Uma cena ou asset conta em cada categoria que tem,
então os números se sobrepõem. Depois das capturas, `Capturado` acrescenta o que os quadros mostram: rosa novo, alteradas,
sem mudança. Depois da migração o primeiro grupo é `Resultado`: corrigido, a conferir, a fazer. Cada número é um botão:
selecione-o para a lista mostrar só o que ele conta, e selecione de novo para mostrar tudo. Passe o mouse sobre um número
para ver o que ele conta, e sobre `Última análise` para ver o número de arquivos e o tempo da análise.

## Veredictos de uma cena

Uma cena tem dois veredictos:

- **Estático**, da análise: a pior quebra entre tudo o que a cena usa. Um objeto rosa pode ficar fora de todos os quadros
  capturados, então um rosa estático pode estar ao lado de um «Igual» observado.
- **Observado**, das capturas de antes e depois da migração: o pior quadro. Cada quadro mostra a proporção de pixels
  alterados e de pixels rosa depois; passe o mouse sobre a linha para ver a diferença média e a proporção de rosa antes.
  Uma área alterada maior (um reflexo perdido, um brilho perdido) faz um quadro «mudar» mesmo que poucos pixels mudem no
  total. Erros no Console enquanto a cena rodava depois da migração também a fazem «mudar», mostrando o primeiro erro. Se a
  análise esperava uma quebra silenciosa que a migração não corrigiu (só o conversor da Unity, o pacote gratuito para uma
  quebra que só o Pro corrige, ou uma quebra que o Urpify não conseguiu converter) e mesmo assim os quadros ficaram
  iguais, o painel avisa: confira o mapa de diferenças.

Na visão de antes e depois, a parte à esquerda da linha é antes e a da direita é depois. `Mostrar diferenças` marca os
pixels alterados: quanto maior a mudança, mais vermelho o pixel.

## Quem corrige

A visão geral conta os assets quebrados por quem os corrige:

- **O conversor da Unity e o Urpify**: o Render Pipeline Converter da Unity e depois as correções que o Urpify roda em
  seguida (os dois no pacote gratuito).
- **Urpify Pro** (no pacote gratuito: **Precisam do Pro**, com um cadeado): shaders, scripts e componentes que só os
  conversores do Pro traduzem.
- **À mão ou ainda sem suporte**: o projeto já estava quebrado antes da migração, a construção não tem equivalente no URP,
  ou o Urpify ainda não a converte. O achado diz o que fazer.

Uma linha da lista diz quem corrige o asset dela: `Unity` (o conversor da Unity), `Urpify` (as correções gratuitas), `Pro`
(Urpify Pro; no pacote gratuito, um cadeado e `Pro`), `À mão`. A dica diz quão perto o resultado fica: igual ao original,
perto dele ou convertido, mas diferente (confira). O filtro `Quem corrige` seleciona os mesmos grupos. Depois da migração
a linha diz em vez disso o que aconteceu com o asset: `Corrigido`, `Conferir` ou `A fazer`. Antes do passo Conversores do
Urpify, é o que a análise espera; depois dele, o que o passo fez. O relatório mantém os níveis dos próprios conversores
(`Auto`, `Aprox.`, `Revisar`, `Ainda não`, `Manual`).

Um shader que o Urpify converte é conferido por um render: antes da conversão, uma esfera com os materiais do projeto
para esse shader é renderizada no Built-in, depois no URP, e as duas são comparadas. Quando uma grande parte da esfera
difere, o shader é convertido mesmo assim, mas não se chama `Auto`: ele é `Revisar`, e o par de imagens fica em
`Urpify/Reports/self-check/`. «Uma grande parte» é mais de 25 % da esfera: a conversão da própria Unity de Standard para
URP Lit muda cores escuras e saturadas em até cerca de 20 %, e é assim que o URP Lit fica, não uma falha. Rosa, ou um
objeto que esconde o que está atrás e mostra a cor de limpeza da câmera, é `Revisar` em qualquer tamanho.

Alguns shaders não podem ser julgados numa esfera e ficam no máximo em `Aprox.`, com o motivo no relatório; confira-os na
comparação de cenas:

- um efeito de tela sem material (scripts o aplicam na imagem inteira);
- um shader interno sob `Hidden/` (scripts o desenham com dados próprios);
- um shader que lê o tempo (`_Time`): o render do Built-in é feito antes da conversão e o do URP depois, então mostram
  momentos diferentes da animação (só contam o rosa ou um render que escurece).

Na Unity 2022.3, um shader com `GrabPass` é renderizado com uma câmera oculta do tipo Game em vez de uma câmera de
prévia: o URP 14 não dá Opaque Texture a uma câmera de prévia. Essa câmera também roda os Renderer Features do seu URP
Asset.

## Mostrar o que quebra

`Mostrar o que quebra`, na visão geral e acima dos passos na janela de migração, mostra o que o URP sozinho faz com o
projeto antes de você corrigir qualquer coisa: faz o backup, captura as cenas no Built-in, instala o URP, roda o conversor
da Unity, captura as cenas de novo e abre o resultado. As correções do Urpify ainda não rodaram. Depois escolha `Corrigir
com o Urpify` (os conversores do Urpify, as capturas de novo e o relatório) ou `Reverter`. No pacote gratuito as correções
são as gratuitas, e o resultado diz o que precisa do Urpify Pro.

## Os passos em ordem

Um passo cujos passos anteriores não foram feitos não roda e diz por quê abaixo da descrição: Capturar antes precisa do
backup, o conversor da Unity precisa do URP instalado, os Conversores do Urpify precisam do conversor da Unity (exceto com
Keep Built-in support, em que o projeto continua no Built-in), e Capturar depois precisa das imagens de antes e de um
projeto que renderize com URP. Duas capturas feitas com o mesmo pipeline nunca são comparadas: o resultado diz que não há
o que comparar.

## O resultado depois da migração

Assim que o conversor da Unity rodou, o lado direito mostra o resultado em vez da visão geral: uma linha com o que importa
(por exemplo `Pronto. Suas cenas estão iguais a antes da mudança`), a cena que mais mudou, antes e depois, depois só o que
precisa de você, cada item com seu botão, e os assets corrigidos recolhidos em `Corrigido automaticamente`. A janela
analisa o projeto de novo depois de cada passo, então a lista mostra o que está corrigido e o que falta. Os arquivos que o
Urpify acrescenta ao projeto (`Assets/Urpify/`, a pasta `UrpCompat` de um pacote, os scripts `UrpSettingsCheck_*`) nunca
aparecem na lista.

## Comparar cenas

Cada cena capturada com o resultado em palavras. Selecione uma cena para comparar as imagens ali mesmo, com o controle
deslizante; `Abrir a cena` abre a cena. Com uma só cena, a comparação já vem aberta. A janela sempre mostra a última
comparação, mesmo depois de uma análise posterior. As imagens são salvas como a tela as mostra, sem transparência, no
tamanho da janela Game, a não ser que `Tamanho da captura` nas configurações diga outra coisa.

## Marca de licença

Um shader cujo cabeçalho cita uma licença (um identificador SPDX como `MIT` ou `CC-BY-NC-4.0`) a mostra como marca ao lado
do nome. Uma cópia convertida é uma obra derivada e continua sob a mesma licença. Uma licença não comercial (`-NC`) tem
uma marca âmbar: uma cópia convertida desse shader não pode ser vendida.

## Luz ambiente (painel da cena)

Na Unity 6, um jogo renderiza sem essa luz uma cena que pega a luz ambiente do skybox e não tem dados de iluminação
assados, tanto no Built-in quanto no URP, então a cena fica mais escura que no Editor. A migração não causa isso, então o
achado é informativo (um ícone de anel). (Na Unity 2022.3, o jogo recebe a luz ambiente mesmo sem dados assados, e o
Urpify não informa essas cenas.) `Assar ambiente` assa nos dados de iluminação da cena só a luz ambiente e os reflexos do
skybox (lightmaps não são assados, e o Auto Generate é desligado para a cena), para que o jogo bata com o Editor. Antes é
feito um backup do projeto; `Reverter bake` no mesmo lugar, ou Reverter na janela de migração, desfaz.
