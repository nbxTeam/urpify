# Início rápido

O ciclo completo em 5 minutos: analisar, migrar, comparar. Experimente primeiro na cena de demonstração e depois numa
cópia do seu projeto.

## 1. Instalar

Instale num projeto que ainda usa a Built-in Render Pipeline:

- **Urpify Pro:** um único arquivo, `Urpify-Pro-1.0.0.unitypackage` (da Asset Store: Package Manager > My Assets > Import). Assets > Import Package > Custom Package, importe tudo: o Urpify, o Urpify Pro, a demonstração e
  esta documentação estão nele. O Urpify gratuito já foi instalado de um `.unitypackage`? Importe o Pro por cima, não é
  preciso remover nada. Instalado pelo Package Manager? Remova-o lá primeiro (Package Manager > Urpify > Remove), porque o
  Urpify Pro já o contém.
- **Urpify (gratuito):** `Urpify-1.0.0.unitypackage` do [GitHub Releases](https://github.com/nbxTeam/urpify/releases/latest),
  importado do mesmo jeito, ou Package Manager > `+` > Add package from git URL:
  `https://github.com/nbxTeam/urpify.git?path=com.urpify.core#v1.0.0`.

Faça commit do seu projeto no controle de versão antes de migrar. O Urpify faz backup de cada arquivo que muda, mas um
commit é o ponto de restauração mais seguro.

## 2. Abrir a demonstração (opcional)

Help > Urpify > **Import Demo**, ou **Importar demonstração** na janela vazia do Urpify. A demonstração vai para
`Assets/Samples/Urpify/1.0.0/Demo` e a cena dela abre. Se você instalou pelo Package Manager, Package Manager > Urpify > Samples > **Demo** >
Import faz o mesmo. A cena tem um estande para
cada construção que quebra no URP: surface shaders, um shader vert/frag iluminado, vidro com GrabPass, água que lê a
profundidade, um projetor, um espelho renderizado por script, um material criado em código e vidro no modo Standard
Transparent. Cada estande tem um Migration Viewpoint, então a comparação mostra cada um de perto.

## 3. Analisar

Window > Rendering > **Urpify** e depois **Analisar projeto**.

A análise só lê arquivos. A lista à esquerda mostra cada shader, script, material, cena e configuração que quebra no URP,
agrupados por tipo. Cada linha tem um ícone:

- quadrado preenchido: fica rosa;
- triângulo preenchido: quebra sem erro (um portal vira uma parede, a água perde a espuma, um efeito desaparece);
- losango preenchido: fica diferente;
- anel: sem mudança visível.

Selecione uma linha para ver o que quebra, por quê e quem corrige. Um shader abre no painel do shader: o código antes e
depois da conversão e uma prévia no Built-in e no URP lado a lado.

## 4. Migrar

Selecione **Migrar...**. A janela lista as etapas:

1. **Analisar**: a mesma análise somente leitura.
2. **Backup**: copia os assets, as configurações e a lista de pacotes do projeto para `Urpify/Backup`.
3. **Capturar antes**: roda cada cena no Play Mode e salva quadros da câmera principal e de cada Migration Viewpoint.
4. **Instalar o URP**: adiciona a versão do URP que vem com o seu Editor.
5. **Rodar o conversor da Unity**: o Render Pipeline Converter da Unity, com os ajustes de que o Urpify precisa para
   rodá-lo de forma confiável.
6. **Conversores do Urpify**: corrigem o que o conversor da Unity deixa quebrado. Com o Pro, também convertem shaders,
   scripts e componentes.
7. **Capturar depois e comparar**: os mesmos quadros no URP, comparados com os de antes.
8. **Revisão**: o relatório final.

Selecione **Rodar tudo**. Cada etapa também pode rodar sozinha, e cada uma pode ser revertida: **Reverter até aqui** numa
etapa, ou **Reverter tudo**.

## 5. Comparar

Selecione **Comparar cenas**. Cada cena mostra seus quadros de antes e depois com um controle deslizante entre eles.
**Mostrar diferenças** marca os pixels alterados: quanto mais vermelho, maior a mudança. **Só as alteradas** esconde as
cenas que ficaram iguais.

Na janela principal, uma cena tem dois veredictos: **Estático**, da análise, e **Observado**, dos quadros. Um objeto rosa
pode ficar fora de todos os quadros capturados, então um veredicto estático pode ser pior que o observado. Adicione um
Migration Viewpoint (um GameObject vazio com o componente `MigrationViewpoint`) onde quiser olhar mais de perto e capture
de novo.

## 6. Concluir ou reverter

Abra **Relatório** > Exportar HTML para ter um relatório que você pode mandar para a equipe. Se não gostar do resultado,
**Reverter tudo** restaura cada arquivo que a migração mudou e move para o backup os arquivos que ela criou. Arquivos que
apareceram entre as etapas nunca são tocados, então o seu próprio trabalho fica.
Depois de reverter pela janela, o Unity reinicia na hora para carregar o projeto como ele está no disco.

Próximo: [Migrar um jogo](scenario-game.md), [Converter um shader](scenario-shader.md),
[Converter um pacote da Asset Store](scenario-asset.md).
