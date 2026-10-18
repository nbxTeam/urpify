# 명령줄

창에서 하는 모든 일은 배치 모드에서도 동작하므로, 빌드 서버가 브랜치를 스캔·변환하고 씬이 분홍색이 되면 빌드를 실패시킬 수
있습니다. 무료 패키지는 Scan, Capture, Verify, Rollback, 모든 Migrate 단계("Urpify 컨버터" 단계는 무료 수정을 실행), 패키지
분석을 실행합니다. 셰이더, 스크립트, 컴포넌트 변환과 변환된 패키지 내보내기에는 Pro가 필요합니다.

```
Unity -batchmode -projectPath <프로젝트> -executeMethod Urpify.Cli.Commands.<명령> [인수]
```

**`-quit`은 넘기지 마세요.** 각 명령은 종료 코드를 파일에 쓴 다음, 그 코드로 에디터를 직접 종료합니다. 오래된 프로젝트 중에는
작업이 끝난 뒤 종료할 때 Unity가 충돌하는 것도 있으므로, CI는 프로세스 종료 코드만 믿지 말고 결과 파일을 읽어야 합니다.

| 종료 코드 | 의미 |
|---|---|
| 0 | 완료. |
| 1 | 실패: 잘못된 인수, 파일 누락, 단계 실패. 이유는 JSON 결과에 있습니다. |
| 2 | 완료했으며, `-urpifyFailOn` 이상의 발견 사항이 있습니다. |

모든 명령은 성공할 때와 오류가 날 때 모두 JSON 결과(`command`, `step`, `status`, `exitCode`, `error`, `reports`)와
텍스트 결과를 씁니다. 텍스트는 종료 코드이고, Migrate 단계는 단계와 상태입니다(예: `KitConverters Done`. `Done`, `Skipped`,
`Failed`, `TimedOut`). 파일:

| 명령 | JSON 결과 (`Urpify/Reports/`) | 텍스트 결과 |
|---|---|---|
| Scan | `urpify-scan-result.json` | `urpify-scan-result.txt` (둘 다 `-urpifyOutput`, 기본값 `Urpify/Reports`) |
| Migrate | `urpify-step-result.json` | `Urpify/urpify-step-result.txt` |
| Capture | | `Urpify/Captures/<label>/urpify-capture-result.txt` |
| Verify | `urpify-verify-result.json` | `Urpify/urpify-verify-result.txt` |
| Rollback | `urpify-rollback-result.json` | `Urpify/urpify-rollback-result.txt` |
| Package | `urpify-package-result.json` | `Urpify/urpify-package-result.txt` |
| BakeEnvironment | `urpify-bake-result.json` | `Urpify/urpify-bake-result.txt` |
| 인수가 잘못된 Capture, Migrate, Package | `urpify-command-result.json` | `Urpify/urpify-command-result.txt` |

## 명령

### Scan

```
-executeMethod Urpify.Cli.Commands.Scan
    [-urpifyOutput <폴더>]                기본값 Urpify/Reports
    [-urpifyScope <경로>[;<경로>...]]     기본값 Assets
    [-urpifyFormat json,html]             기본값 둘 다
    [-urpifyFailOn pink|silent|degraded]
    [-urpifyNoCache]
```

읽기 전용입니다. [보고서 스키마](../report-schema.md)(영어)에 설명된 JSON 보고서와 단독으로 열 수 있는 HTML 보고서를 씁니다.
`urp-migration-report-latest.json`은 타임스탬프가 붙은 파일 옆에 항상 쓰입니다. 스캔은 파일별 결과를 `Library/Urpify/cache`에
캐시하며, `-urpifyNoCache`는 캐시를 쓰지 않습니다.

### Capture와 Verify

```
-executeMethod Urpify.Cli.Commands.Capture -urpifyLabel <레이블>
    [-urpifyScenes build|all|<a.unity>;<b.unity>]   기본값: Project Settings > Urpify > 비교 > 씬
    [-urpifyWidth 960 -urpifyHeight 540 -urpifyFrames 30]
-executeMethod Urpify.Cli.Commands.Verify [-urpifyBefore before] [-urpifyAfter after] [-urpifyFailOn pink|changed]
```

Capture는 각 씬을 Play Mode에서 재생하고 메인 카메라와 모든 Migration Viewpoint의 프레임을 `Urpify/Captures/<레이블>/`에
저장합니다. Verify는 두 레이블을 비교해 씬별 관찰 판정을 담은 보고서를 씁니다.

### Migrate

```
-executeMethod Urpify.Cli.Commands.Migrate -urpifyStep <단계>
    [-urpifyShaderOutput replace|write-copy|keep-builtin]       kit-converters; 기본값은 프로젝트 설정
    [-urpifyGrabPass opaque-texture|after-transparents]          kit-converters; 기본값은 프로젝트 설정
    [-urpifyScenes ...] [-urpifyWidth ...] [-urpifyHeight ...] [-urpifyFrames ...]   캡처 단계
    [-urpifyStepTimeout 20]                                      진행이 없을 때 단계를 멈추기까지의 분; 0 = 멈추지 않음
```

단계는 이 순서로, 에디터 프로세스마다 하나씩 실행합니다: `scan`, `backup`, `capture-before`, `install-urp`,
`unity-converter`, `kit-converters`, `capture-after`, `review`. Install URP는 다음 프로세스가 컴파일할 패키지를 추가하므로,
각 단계는 따로따로 프로세스에서 실행합니다. Backup은 Capture before보다 먼저 실행하며, 백업 없이는 Capture before가 실행되지
않습니다: 씬을 재생하면 Unity가 오래된 에셋을 다시 저장하고 패키지가 파일을 만들기 때문에, 백업은 원래 상태의 프로젝트를
보존해야 합니다.

파일을 바꾸는 단계는 git 작업 트리에 커밋하지 않은 변경이 있으면 로그에 경고합니다.

### Rollback

```
-executeMethod Urpify.Cli.Commands.Rollback [-urpifyToStep backup]
```

`-urpifyToStep` 없이는 모든 단계를 롤백합니다. 마이그레이션 후에 바뀐 파일과 단계 사이에 생긴 파일은 그대로 남고 로그에
나열됩니다.

### Package

```
-executeMethod Urpify.Cli.Commands.Package -urpifyPackage <파일.unitypackage> [-urpifyOutput <폴더>]
    [-urpifyExport <변환된.unitypackage>] [-urpifyShaderOutput replace|keep-builtin] [-urpifyKeepScope]
```

패키지를 임시 폴더로 가져와 스캔하고, `-urpifyExport`(Pro)를 주면 [Asset Store 패키지 변환](scenario-asset.md)에 설명한
대로 변환된 패키지를 씁니다. 분석 보고서는 내보내기가 실패하거나 거부되어도 항상 쓰이며, 그때 종료 코드는 1입니다. 내보내기는
변환된 셰이더를 URP용으로 컴파일하므로 프로젝트에 URP가 설치되어 있어야 합니다. `-urpifyKeepScope`는 확인용으로 임시 폴더를
남깁니다.

### Bake environment

```
-executeMethod Urpify.Cli.Commands.BakeEnvironment -urpifyScene <Assets/a.unity> [-urpifyUndo]
```

스카이박스 주변광과 반사가 없는 씬에, 백업을 만든 뒤 이를 베이크합니다. `-urpifyUndo`는 베이크를 롤백합니다. 발견 사항
"스카이박스 주변광이 베이크되지 않음"을 참고하세요.

## 중단된 단계와 시간 제한

도중에 멈춘 단계(에디터가 종료되거나 충돌함, 단계가 실패함)는 바꾼 내용의 완전한 기록을 남깁니다: 다음에 에디터를 시작할 때
중단된 것으로 마무리되고, 그 변경은 롤백에 포함됩니다. 단계가 실행되는 동안 `Urpify/progress.json`은 마지막으로 한 일
(`Step`, `What`, `Done`, `Total`, `UpdatedUtc`)을 알려 줍니다. 배치 모드에서는 `-urpifyStepTimeout`분 동안 진행이 없는 단계를
에디터가 종료된 것과 같이 멈추고, 이유를 `progress.json`과 단계 메시지에 남깁니다. 진행이란 단계가 보고하는 내용과 쓰는
파일이며, Unity 로그가 아닙니다. 창이 있는 에디터는 절대 멈추지 않습니다.

## 스크립트가 컴파일되지 않는 프로젝트

Unity는 어떤 명령도 시작하기 전에 배치 실행을 중단합니다("Scripts have compiler errors"). `-ignorecompilererrors`를 추가하세요:
스캔이 컴파일 오류를 직접 보고하고(`PROJECT.SCRIPT_COMPILATION_FAILED`), 스크립트를 바꾸는 단계는 그 스크립트를 건드리지
않습니다.

## 예: 씬이 분홍색이 되면 빌드 실패시키기

```
U="/path/to/Unity"; P=/path/to/project
for step in scan backup capture-before install-urp unity-converter kit-converters capture-after review; do
  "$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Migrate -urpifyStep $step -logFile -
  grep -q " Done\| Skipped" "$P/Urpify/urpify-step-result.txt" || { echo "step $step failed"; exit 1; }
done
"$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Verify -urpifyFailOn pink -logFile -
exit $(cat "$P/Urpify/urpify-verify-result.txt")
```

## 단계가 쓰는 파일

- `Urpify/state.json`: 마이그레이션 상태. `Assets` 밖에 있습니다.
- `Urpify/Backup/<id>/`: 파일을 바꾸는 단계마다 하나씩의 백업. 각 파일의 전후 해시를 담은 매니페스트 포함.
- `Urpify/Captures/<레이블>/`: 캡처한 프레임과 `captures.json`.
- `Urpify/Reports/`: 스캔, 비교, 검토 보고서. `kit-converters.json`은 "Urpify 컨버터" 단계 자체의 보고서(파일별 `Path`,
  `Converter`, `Outcome`, `Notes`, URP Asset 변경, 변환된 파일에 필요한 URP 설정). `self-check/`는 변환된 각 셰이더의
  Built-in과 URP 렌더링입니다.

마이그레이션 기록을 남기고 싶다면 `Urpify/`를 버전 관리에 추가하세요. 빌드는 이를 읽지 않습니다.
