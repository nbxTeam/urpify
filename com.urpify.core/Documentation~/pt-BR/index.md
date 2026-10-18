# Urpify

O Urpify leva um projeto Unity, um shader ou um pacote da Asset Store da Built-in Render Pipeline para o URP. Ele mostra
tudo o que a troca quebra antes de qualquer mudança, inclusive o que quebra sem nenhum erro. Depois converte o que
consegue, faz backup de cada arquivo que toca e compara cada cena antes e depois com quadros capturados. Cada etapa pode
ser revertida.

O Urpify vem em dois pacotes:

| | Urpify (grátis, MIT) | Urpify Pro |
|---|---|---|
| Analisar um projeto, pasta, arquivo ou `.unitypackage` | Sim | Sim |
| Comparar cenas antes e depois com quadros capturados | Sim | Sim |
| Rodar o Render Pipeline Converter da Unity de forma confiável, pela janela ou pela linha de comando | Sim | Sim |
| Correções depois do conversor da Unity (vidro, Self-Illumin, materiais embutidos, bloom do Post-processing v2, configurações do URP Asset, materiais de pacotes) | Sim | Sim |
| Backup e reversão | Sim | Sim |
| Painel do shader: código, prévia no Built-in e no URP como estão | Sim | Sim |
| Converter shaders próprios, efeitos de imagem, scripts de câmera, command buffers e projetores | Não | Sim |
| Saída: um arquivo de shader para as duas pipelines (Manter suporte ao Built-in) | Não | Sim |
| Exportar um `.unitypackage` convertido | Só o relatório | Sim |
| Linha de comando | Análise, captura, comparação, conversor da Unity e correções gratuitas | Tudo |

## Conteúdo

Comece aqui:
- [Início rápido](quick-start.md): o ciclo completo em 5 minutos, com a cena de demonstração ou com o seu projeto.
- [Migrar um jogo](scenario-game.md)
- [Converter um shader](scenario-shader.md)
- [Converter um pacote da Asset Store para as duas pipelines](scenario-asset.md)

Como o Urpify trata cada construção da Built-in Render Pipeline:
- [Construções: visão geral](constructs/index.md), uma página por construção com imagens de antes e depois.
- [O que não é convertido e por quê](not-converted.md)
- [Onde o URP fica diferente](urp-differences.md)

Referência:
- [A janela do Urpify](window.md): ícones, contadores, veredictos.
- [Configurações](settings.md)
- [Linha de comando](cli.md)
- [Esquema do relatório](../report-schema.md) (em inglês)
- [Versões da Unity e do URP](urp-version-matrix.md)
- [Perguntas frequentes](faq.md)
- [Redistribuir shaders convertidos](../license-redistribution.md) (em inglês)

## Requisitos

- Unity 2022.3 LTS (URP 14) ou Unity 6000.0 LTS, 6000.3 LTS, 6000.5 ou 6000.6 (URP 17), as versões testadas. Veja [Versões da Unity e do URP](urp-version-matrix.md).
- Um projeto na Built-in Render Pipeline ou um `.unitypackage` feito para ela. O Urpify instala o URP sozinho quando a
  migração precisa.
- Nenhum outro pacote. O Urpify não envia nada pela rede, exceto o arquivo de shader que você decidir enviar com
  `Enviar relatório` (veja as [perguntas frequentes](faq.md)).

## Idiomas

A janela fala English, 简体中文, 日本語, 한국어, Русский, Español, Português (Brasil) e Deutsch. Por padrão ela segue o idioma
do Editor (Preferences > Languages) e usa inglês quando o Urpify não tem esse idioma. Mude em Project Settings > Urpify >
Pessoal > Idioma. As notas dos conversores nos relatórios, a linha de comando e os detalhes técnicos do relatório HTML
ficam em inglês.
