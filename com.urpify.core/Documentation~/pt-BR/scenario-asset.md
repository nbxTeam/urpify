# Converter um pacote da Asset Store para as duas pipelines

Você publica shaders ou efeitos na Asset Store e quer um único pacote que funcione para compradores no Built-in e no URP.
A ação principal é **Exportar para Built-in e URP** (Pro).

## O que você precisa

- Um projeto com o URP instalado. Os shaders convertidos são compilados para o URP antes de serem gravados, então a
  exportação se recusa a rodar sem ele. O seu pacote em si pode continuar sendo um pacote de Built-in; o projeto só
  hospeda a exportação.
- O `.unitypackage` que você distribui hoje.

## Analisar o pacote

Na janela do Urpify: **Alterar** (escopo) > **Pacote da Unity (.unitypackage)...** e escolha o seu arquivo. O Urpify o
importa numa pasta temporária, analisa e remove a pasta quando você fecha a janela ou muda o escopo. O seu projeto fica
como estava.

A visão geral mostra o que quebra para um comprador no URP, do mesmo jeito que para um projeto. O pacote gratuito para
aqui: o relatório (Relatório > Exportar HTML) é o que você pode mostrar ou anexar.

## Exportar

Na visão geral, **Exportar para Built-in e URP...**, ou Relatório > **Exportar pacote convertido...**. Escolha onde
salvar. O novo `.unitypackage` mantém cada GUID e cada caminho do original e contém:

- os shaders convertidos. Com a saída **Manter suporte ao Built-in** (Project Settings > Urpify > Conversão > Saída), cada
  arquivo mantém sem mudança os SubShaders originais do Built-in e ganha um SubShader do URP na frente deles, então um
  arquivo funciona nas duas pipelines; a Unity escolhe o SubShader que corresponde à pipeline ativa;
- os materiais que usavam shaders embutidos da Unity, atualizados como o conversor da Unity faria no projeto do
  comprador, com as correções do Urpify depois;
- scripts convertidos como num projeto, cada um compilado contra o projeto antes de ser gravado. Um script cuja reescrita
  não compila fica como estava e é listado;
- a biblioteca de shaders do Urpify e sua pequena parte de runtime, numa pasta `UrpCompat` em cada pasta raiz do seu
  pacote, com GUIDs novos a cada exportação e o runtime no namespace do seu pacote. Dois pacotes de autores diferentes
  nunca compartilham nem sobrescrevem a cópia um do outro;
- uma pequena verificação do Editor, com o nome do seu pacote. Quando o projeto do comprador usa URP, ela confere nos URP
  Assets o que os seus shaders precisam (Depth Texture, Opaque Texture, o Renderer Feature Built-in Camera Hooks) e
  pergunta uma vez antes de ligar qualquer coisa. No Built-in ela não faz nada.

Um relatório de compatibilidade é salvo ao lado do pacote: cada shader com seu resultado e as configurações do URP Asset
de que os compradores precisam.

## Conferir como um comprador

Importe o pacote exportado num projeto limpo de Built-in e num projeto limpo de URP. No Built-in, a saída Manter suporte
ao Built-in renderiza exatamente como o original: os SubShaders originais não são tocados. No URP, abra a cena de
demonstração do pacote e compare com as suas capturas de tela do Built-in, ou rode nela a comparação de cenas da migração.

## Redistribuição

Você pode distribuir os shaders convertidos e a pasta `UrpCompat` nos seus pacotes pagos. Veja
[Redistribuir shaders convertidos](../license-redistribution.md) (em inglês).
