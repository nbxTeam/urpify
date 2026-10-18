# Linha de comando

Tudo o que a janela faz também funciona no modo batch, então um servidor de build pode analisar um branch, convertê-lo e
falhar o build quando uma cena fica rosa. O pacote gratuito roda Scan, Capture, Verify, Rollback, todas as etapas de
Migrate (a etapa Conversores do Urpify roda as correções gratuitas) e a análise de pacotes. Converter shaders, scripts e
componentes, e exportar um pacote convertido, exige o Pro.

```
Unity -batchmode -projectPath <projeto> -executeMethod Urpify.Cli.Commands.<Comando> [argumentos]
```

**Não passe `-quit`.** Cada comando fecha o Editor sozinho com o seu código de saída, depois de gravá-lo num arquivo:
alguns projetos antigos fazem a Unity travar ao sair depois que o trabalho terminou, então a CI deve ler o arquivo de
resultado em vez de confiar só no código de saída do processo.

| Código de saída | Significado |
|---|---|
| 0 | Concluído. |
| 1 | Falhou: argumento errado, arquivo ausente, etapa com falha. O motivo está no resultado JSON. |
| 2 | Concluído, e foram encontrados achados no nível de `-urpifyFailOn` ou acima. |

Todo comando grava, no sucesso e em qualquer erro, um resultado JSON (`command`, `step`, `status`, `exitCode`, `error`,
`reports`) e um resultado em texto: o código de saída ou, para uma etapa do Migrate, a etapa e o status dela, como
`KitConverters Done` (`Done`, `Skipped`, `Failed`, `TimedOut`). Os arquivos:

| Comando | Resultado JSON (`Urpify/Reports/`) | Resultado em texto |
|---|---|---|
| Scan | `urpify-scan-result.json` | `urpify-scan-result.txt` (ambos em `-urpifyOutput`, padrão `Urpify/Reports`) |
| Migrate | `urpify-step-result.json` | `Urpify/urpify-step-result.txt` |
| Capture | | `Urpify/Captures/<label>/urpify-capture-result.txt` |
| Verify | `urpify-verify-result.json` | `Urpify/urpify-verify-result.txt` |
| Rollback | `urpify-rollback-result.json` | `Urpify/urpify-rollback-result.txt` |
| Package | `urpify-package-result.json` | `Urpify/urpify-package-result.txt` |
| BakeEnvironment | `urpify-bake-result.json` | `Urpify/urpify-bake-result.txt` |
| Capture, Migrate ou Package com argumento errado | `urpify-command-result.json` | `Urpify/urpify-command-result.txt` |

## Comandos

### Scan

```
-executeMethod Urpify.Cli.Commands.Scan
    [-urpifyOutput <pasta>]                padrão Urpify/Reports
    [-urpifyScope <caminho>[;<caminho>...]]  padrão Assets
    [-urpifyFormat json,html]              padrão os dois
    [-urpifyFailOn pink|silent|degraded]
    [-urpifyNoCache]
```

Somente leitura. Grava o relatório JSON descrito no [esquema do relatório](../report-schema.md) (em inglês) e um relatório
HTML independente. `urp-migration-report-latest.json` é sempre gravado, ao lado dos arquivos com data e hora. A análise
guarda resultados por arquivo em `Library/Urpify/cache`; `-urpifyNoCache` ignora o cache.

### Capture e Verify

```
-executeMethod Urpify.Cli.Commands.Capture -urpifyLabel <rótulo>
    [-urpifyScenes build|all|<a.unity>;<b.unity>]   padrão: Project Settings > Urpify > Comparação > Cenas
    [-urpifyWidth 960 -urpifyHeight 540 -urpifyFrames 30]
-executeMethod Urpify.Cli.Commands.Verify [-urpifyBefore before] [-urpifyAfter after] [-urpifyFailOn pink|changed]
```

Capture roda cada cena no Play Mode e salva quadros da câmera principal e de cada Migration Viewpoint em
`Urpify/Captures/<rótulo>/`. Verify compara dois rótulos e grava um relatório com o veredicto observado por cena.

### Migrate

```
-executeMethod Urpify.Cli.Commands.Migrate -urpifyStep <etapa>
    [-urpifyShaderOutput replace|write-copy|keep-builtin]       kit-converters; padrão: a configuração do projeto
    [-urpifyGrabPass opaque-texture|after-transparents]          kit-converters; padrão: a configuração do projeto
    [-urpifyScenes ...] [-urpifyWidth ...] [-urpifyHeight ...] [-urpifyFrames ...]   etapas de captura
    [-urpifyStepTimeout 20]                                      minutos sem progresso até parar a etapa; 0 = nunca
```

As etapas em ordem, uma por processo do Editor: `scan`, `backup`, `capture-before`, `install-urp`, `unity-converter`,
`kit-converters`, `capture-after`, `review`. Install URP adiciona um pacote que o processo seguinte compila, por isso cada
etapa roda no seu próprio processo. Backup roda antes de Capture before, e Capture before se recusa a rodar sem backup: ao
rodar as cenas, a Unity salva de novo assets antigos e os pacotes criam arquivos, e o backup precisa guardar o projeto
como ele era.

As etapas que mudam arquivos avisam no log se a árvore de trabalho do git tem mudanças sem commit.

### Rollback

```
-executeMethod Urpify.Cli.Commands.Rollback [-urpifyToStep backup]
```

Sem `-urpifyToStep`, reverte todas as etapas. Arquivos alterados depois da migração e arquivos que apareceram entre as
etapas ficam como estão e são listados no log.

### Package

```
-executeMethod Urpify.Cli.Commands.Package -urpifyPackage <arquivo.unitypackage> [-urpifyOutput <pasta>]
    [-urpifyExport <convertido.unitypackage>] [-urpifyShaderOutput replace|keep-builtin] [-urpifyKeepScope]
```

Importa o pacote numa pasta temporária, analisa e, com `-urpifyExport` (Pro), grava o pacote convertido como descrito em
[Converter um pacote da Asset Store](scenario-asset.md). O relatório da análise é sempre gravado, também quando a
exportação falha ou é recusada; nesse caso o código de saída é 1. A exportação compila os shaders convertidos para o URP,
então o URP precisa estar instalado no projeto. `-urpifyKeepScope` mantém a pasta temporária para inspeção.

### Bake environment

```
-executeMethod Urpify.Cli.Commands.BakeEnvironment -urpifyScene <Assets/a.unity> [-urpifyUndo]
```

Assa a luz ambiente e os reflexos do skybox numa cena que não os tem, com backup; `-urpifyUndo` reverte o bake. Veja o
achado «Luz ambiente do skybox não assada».

## Etapas interrompidas e limites de tempo

Uma etapa que para no meio (o Editor foi fechado ou travou, a etapa falhou) guarda um registro completo do que mudou: a
próxima abertura do Editor a fecha como interrompida, e as mudanças dela entram na reversão. Enquanto uma etapa roda,
`Urpify/progress.json` diz o que ela fez por último (`Step`, `What`, `Done`, `Total`, `UpdatedUtc`). No modo batch, uma
etapa que não avança por `-urpifyStepTimeout` minutos é parada como um Editor fechado, com o motivo em `progress.json` e
na mensagem da etapa. Progresso é o que a etapa informa e os arquivos que ela grava, não o log da Unity. Um Editor com
janela nunca é parado.

## Projetos cujos scripts não compilam

A Unity interrompe uma execução batch antes de qualquer comando começar («Scripts have compiler errors»). Adicione
`-ignorecompilererrors`: a análise então informa os erros de compilação por conta própria
(`PROJECT.SCRIPT_COMPILATION_FAILED`), e as etapas que mudam scripts os deixam como estão.

## Exemplo: falhar o build quando uma cena fica rosa

```
U="/caminho/para/Unity"; P=/caminho/para/projeto
for step in scan backup capture-before install-urp unity-converter kit-converters capture-after review; do
  "$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Migrate -urpifyStep $step -logFile -
  grep -q " Done\| Skipped" "$P/Urpify/urpify-step-result.txt" || { echo "a etapa $step falhou"; exit 1; }
done
"$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Verify -urpifyFailOn pink -logFile -
exit $(cat "$P/Urpify/urpify-verify-result.txt")
```

## Arquivos que as etapas gravam

- `Urpify/state.json`: o estado da migração, fora de `Assets`.
- `Urpify/Backup/<id>/`: um backup por etapa que muda arquivos, com um manifesto do hash de cada arquivo antes e depois.
- `Urpify/Captures/<rótulo>/`: os quadros capturados e `captures.json`.
- `Urpify/Reports/`: relatórios de análise, comparação e revisão; `kit-converters.json`, o relatório próprio da etapa
  Conversores do Urpify (por arquivo: `Path`, `Converter`, `Outcome`, `Notes`; mudanças no URP Asset; configurações do URP
  de que os arquivos convertidos precisam); `self-check/`, os renders de cada shader convertido no Built-in e no URP.

Adicione `Urpify/` ao controle de versão se quiser guardar o histórico da migração; o build não o lê.
