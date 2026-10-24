# Командная строка

Всё, что делает окно, работает и в пакетном режиме, поэтому сервер сборки может просканировать ветку, перевести её и
провалить сборку, если сцена стала розовой. Бесплатный пакет выполняет Scan, Capture, Verify, Rollback, все шаги Migrate
(его шаг «Конвертеры Urpify» выполняет бесплатные исправления) и анализ пакета. Для перевода шейдеров, скриптов и
компонентов и для экспорта переведённого пакета нужен Pro.

```
Unity -batchmode -projectPath <проект> -executeMethod Urpify.Cli.Commands.<Команда> [аргументы]
```

**Не передавайте `-quit`.** Каждая команда сама закрывает редактор со своим кодом выхода, предварительно записав его в
файл: некоторые старые проекты роняют Unity при выходе уже после работы, поэтому CI лучше читать файл результата, а не
полагаться только на код выхода процесса.

| Код выхода | Значение |
|---|---|
| 0 | Готово. |
| 1 | Ошибка: неверный аргумент, отсутствующий файл, проваленный шаг. Причина — в JSON-результате. |
| 2 | Готово, и найдены находки уровня `-urpifyFailOn` или выше. |

Каждая команда при успехе и при любой ошибке пишет JSON-результат (`command`, `step`, `status`, `exitCode`, `error`,
`reports`) и текстовый результат: код выхода, а для шага Migrate — шаг и его статус, например `KitConverters Done`
(`Done`, `Skipped`, `Failed`, `TimedOut`). Файлы:

| Команда | JSON-результат (`Urpify/Reports/`) | Текстовый результат |
|---|---|---|
| Scan | `urpify-scan-result.json` | `urpify-scan-result.txt` (оба в `-urpifyOutput`, по умолчанию `Urpify/Reports`) |
| Migrate | `urpify-step-result.json` | `Urpify/urpify-step-result.txt` |
| Capture | | `Urpify/Captures/<label>/urpify-capture-result.txt` |
| Verify | `urpify-verify-result.json` | `Urpify/urpify-verify-result.txt` |
| Rollback | `urpify-rollback-result.json` | `Urpify/urpify-rollback-result.txt` |
| Package | `urpify-package-result.json` | `Urpify/urpify-package-result.txt` |
| BakeEnvironment | `urpify-bake-result.json` | `Urpify/urpify-bake-result.txt` |
| Capture, Migrate или Package с неверным аргументом | `urpify-command-result.json` | `Urpify/urpify-command-result.txt` |

## Команды

### Scan

```
-executeMethod Urpify.Cli.Commands.Scan
    [-urpifyOutput <папка>]              по умолчанию Urpify/Reports
    [-urpifyScope <путь>[;<путь>...]]    по умолчанию Assets
    [-urpifyFormat json,html]            по умолчанию оба
    [-urpifyFailOn pink|silent|degraded]
    [-urpifyNoCache]
```

Только чтение. Пишет JSON-отчёт, описанный в [схеме отчёта](../report-schema.md), и самодостаточный HTML-отчёт.
`urp-migration-report-latest.json` пишется всегда, рядом с файлами с меткой времени. Сканирование кэширует результаты по
файлам в `Library/Urpify/cache`; `-urpifyNoCache` не использует кэш.

### Capture и Verify

```
-executeMethod Urpify.Cli.Commands.Capture -urpifyLabel <метка>
    [-urpifyScenes build|all|<a.unity>;<b.unity>]   по умолчанию: Project Settings > Urpify > Compare > Scenes
    [-urpifyWidth 960 -urpifyHeight 540 -urpifyFrames 30]
-executeMethod Urpify.Cli.Commands.Verify [-urpifyBefore before] [-urpifyAfter after] [-urpifyFailOn pink|changed]
```

Capture запускает каждую сцену в Play Mode и сохраняет кадры основной камеры и всех Migration Viewpoint в
`Urpify/Captures/<метка>/`. Verify сравнивает две метки и пишет отчёт с вердиктом по снимкам для каждой сцены.

### Migrate

```
-executeMethod Urpify.Cli.Commands.Migrate -urpifyStep <шаг>
    [-urpifyShaderOutput replace|write-copy|keep-builtin]       kit-converters; по умолчанию — настройка проекта
    [-urpifyGrabPass opaque-texture|after-transparents]          kit-converters; по умолчанию — настройка проекта
    [-urpifyScenes ...] [-urpifyWidth ...] [-urpifyHeight ...] [-urpifyFrames ...]   шаги снимков
    [-urpifyStepTimeout 20]                                      минут без прогресса до остановки шага; 0 = никогда
```

Шаги по порядку, по одному на процесс редактора: `scan`, `backup`, `capture-before`, `install-urp`, `unity-converter`,
`kit-converters`, `capture-after`, `review`. Install URP добавляет пакет, который компилирует следующий процесс, поэтому
каждый шаг — в своём процессе. Backup выполняется до Capture before, а Capture before без бэкапа не запускается: при
проигрывании сцен Unity пересохраняет старые ассеты, а пакеты создают файлы, и бэкап должен хранить проект таким, каким
он был.

Версия из Asset Store не устанавливает пакеты: установите URP в Package Manager до `install-urp` — шаг тогда только проверяет его. Проект, которому конвертер Unity нужен в своём окне (используется Post-processing v2, Unity 6000.3), запускает `unity-converter` из окна Urpify.

Шаги, которые меняют файлы, предупреждают в логе, если в рабочем дереве git есть незакоммиченные изменения.

### Rollback

```
-executeMethod Urpify.Cli.Commands.Rollback [-urpifyToStep backup]
```

Без `-urpifyToStep` откатывает все шаги. Файлы, изменённые после миграции, и файлы, появившиеся между шагами, остаются
как есть и перечисляются в логе.

### Package

```
-executeMethod Urpify.Cli.Commands.Package -urpifyPackage <файл.unitypackage> [-urpifyOutput <папка>]
    [-urpifyExport <переведённый.unitypackage>] [-urpifyShaderOutput replace|keep-builtin] [-urpifyKeepScope]
```

Импортирует пакет во временную папку, сканирует его, а с `-urpifyExport` (Pro) пишет переведённый пакет, как описано в
[Перевести пакет из Asset Store](scenario-asset.md). Отчёт анализа пишется всегда, в том числе когда экспорт не удался или
отклонён; код выхода тогда 1. Экспорт компилирует переведённые шейдеры для URP, поэтому URP должен быть установлен в
проекте. `-urpifyKeepScope` оставляет временную папку для просмотра.

### Bake environment

```
-executeMethod Urpify.Cli.Commands.BakeEnvironment -urpifyScene <Assets/a.unity> [-urpifyUndo]
```

Запекает рассеянный свет и отражения скайбокса в сцену, где их нет, с бэкапом; `-urpifyUndo` откатывает запекание. См.
находку «Рассеянный свет неба не запечён».

## Прерванные шаги и тайм-ауты

Шаг, остановившийся на полпути (редактор убит или упал, шаг провалился), сохраняет полную запись того, что он изменил:
следующий запуск редактора закрывает его как прерванный, и его изменения входят в откат. Пока шаг работает,
`Urpify/progress.json` говорит, что он сделал последним (`Step`, `What`, `Done`, `Total`, `UpdatedUtc`). В пакетном режиме
шаг, не продвигающийся `-urpifyStepTimeout` минут, останавливается как убитый редактор, с причиной в `progress.json` и в
сообщении шага. Прогресс — то, что сообщает шаг, и файлы, которые он пишет, а не лог Unity. Редактор с окном не
останавливается никогда.

## Проекты, скрипты которых не компилируются

Unity останавливает пакетный запуск до начала любой команды («Scripts have compiler errors»). Добавьте
`-ignorecompilererrors`: сканирование тогда само сообщит об ошибках компиляции (`PROJECT.SCRIPT_COMPILATION_FAILED`), а
шаги, меняющие скрипты, их не тронут.

## Пример: провалить сборку, если сцена стала розовой

```
U="/путь/к/Unity"; P=/путь/к/проекту
for step in scan backup capture-before install-urp unity-converter kit-converters capture-after review; do
  "$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Migrate -urpifyStep $step -logFile -
  grep -q " Done\| Skipped" "$P/Urpify/urpify-step-result.txt" || { echo "шаг $step не прошёл"; exit 1; }
done
"$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Verify -urpifyFailOn pink -logFile -
exit $(cat "$P/Urpify/urpify-verify-result.txt")
```

## Файлы, которые пишут шаги

- `Urpify/state.json`: состояние миграции, вне `Assets`.
- `Urpify/Backup/<id>/`: по бэкапу на каждый шаг, меняющий файлы, с манифестом хеша каждого файла до и после.
- `Urpify/Captures/<метка>/`: снятые кадры и `captures.json`.
- `Urpify/Reports/`: отчёты сканирования, сравнения и итога; `kit-converters.json`, собственный отчёт шага «Конвертеры
  Urpify» (по файлам: `Path`, `Converter`, `Outcome`, `Notes`; изменения URP Asset; настройки URP, нужные переведённым
  файлам); `self-check/`, рендеры каждого переведённого шейдера в Built-in и URP.

Добавьте `Urpify/` в систему контроля версий, если хотите хранить историю миграции; сборка его не читает.
