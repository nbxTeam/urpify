# Migrar um jogo

Você tem um jogo na Built-in Render Pipeline e quer levá-lo para o URP com a mesma aparência.

## Antes de começar

- Faça commit do projeto. O Urpify faz backup do que muda, mas um commit é o ponto de restauração em que você mais
  confia.
- Confira se o projeto compila e entra no Play Mode no Built-in. O Urpify informa erros de compilação de scripts como um
  achado: eles bloqueiam o Play Mode e, com isso, as capturas.
- Decida quais cenas comparar: Project Settings > Urpify > Comparação > Cenas. Por padrão, as cenas do Build Settings.
- Coloque alguns Migration Viewpoints em cenas onde você sabe que um efeito importa: um portal, um espelho, água, um
  pós-efeito. Adicione o componente `MigrationViewpoint` a um GameObject vazio e aponte-o como uma câmera. Ele é marcado
  como EditorOnly e nunca vai para um build.

## Analisar e ler a visão geral

Window > Rendering > Urpify > **Analisar projeto**. A visão geral começa pelas cenas que quebram e depois conta os assets
quebrados por quem os corrige:

- **O conversor da Unity e o Urpify**: shaders embutidos que o Render Pipeline Converter da Unity atualiza, e os casos em
  que o resultado dele está errado, que o pacote gratuito corrige em seguida.
- **Urpify Pro**: shaders próprios, efeitos de imagem, scripts de câmera, command buffers e projetores.
- **À mão ou ainda sem suporte**: o projeto já estava quebrado antes da migração, o URP não tem para onde levar a
  construção, ou o Urpify ainda não a converte. Veja [O que não é convertido](not-converted.md).

A lista à esquerda pode ser filtrada por problema, correção e tipo, e pesquisada por nome de arquivo ou id de regra.
**Corrigido sem o Pro** mostra o que o pacote gratuito resolve.

## Migrar

**Migrar...** > **Rodar tudo**, ou etapa por etapa. As etapas que mudam arquivos fazem backup antes. Enquanto uma etapa
roda, a janela mostra o progresso, e qualquer etapa pode rodar de novo. Se o Editor fechar no meio de uma etapa, na
próxima vez que a janela abrir ela oferece rodar a etapa de novo ou revertê-la.

## Comparar e revisar

Depois de **Capturar depois e comparar**, abra **Comparar cenas**:

- **Igual**: os quadros coincidem dentro dos limites calibrados em Standard para URP Lit.
- **Mudou**: alguns pixels diferem, ou uma área mudou muito (um reflexo perdido, um brilho perdido), ou a cena registrou
  erros enquanto rodava.
- **Rosa novo**: um objeto é renderizado com o shader de erro.

Veja cada cena que mudou com **Mostrar diferenças**. Algumas diferenças são do URP, não da migração: veja
[Onde o URP fica diferente](urp-differences.md).

O relatório final (**Revisão**) lista cada arquivo convertido com seu nível: `Auto` (praticamente idêntico), `Aprox.`
(próximo, não idêntico pixel a pixel), `Revisar` (convertido, mas o render de teste do próprio Urpify difere do original:
confira) e o que precisa ser feito à mão.

## Reverter

**Reverter tudo** deixa o projeto como estava antes da primeira etapa. **Reverter até aqui** numa etapa desfaz todas as
etapas posteriores. Arquivos que você mudou depois da migração ficam como estão e são listados no Console.

## Num pipeline de build

Cada etapa roda pela linha de comando, então um servidor de build pode migrar um branch e falhar o build quando uma cena
fica rosa. Veja [Linha de comando](cli.md).
