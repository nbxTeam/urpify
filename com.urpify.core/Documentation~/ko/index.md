# Urpify

Urpify는 Unity 프로젝트, 셰이더, Asset Store 패키지를 Built-in Render Pipeline에서 URP로 옮깁니다. 무엇이든 바꾸기 전에
전환으로 깨지는 모든 것을 보여 주며, 오류 없이 깨지는 것도 포함합니다. 그런 다음 변환할 수 있는 것을 변환하고, 건드리는
모든 파일을 백업하고, 각 씬을 마이그레이션 전후로 캡처한 프레임으로 비교합니다. 모든 단계는 롤백할 수 있습니다.

Urpify는 두 개의 패키지로 제공됩니다.

| | Urpify(무료, MIT) | Urpify Pro |
|---|---|---|
| 프로젝트, 폴더, 파일, `.unitypackage` 스캔 | 예 | 예 |
| 캡처한 프레임으로 마이그레이션 전후 씬 비교 | 예 | 예 |
| 창 또는 명령줄에서 Unity의 Render Pipeline Converter를 안정적으로 실행 | 예 | 예 |
| Unity 컨버터 이후의 수정(유리, Self-Illumin, 내장 머티리얼, Post-processing v2 블룸, URP Asset 설정, 패키지 머티리얼) | 예 | 예 |
| 백업과 롤백 | 예 | 예 |
| 셰이더 패널: 코드, Built-in과 URP에서 현재 상태 미리 보기 | 예 | 예 |
| 커스텀 셰이더, 이미지 효과, 카메라 스크립트, 커맨드 버퍼, 프로젝터 변환 | 아니요 | 예 |
| 출력: 두 파이프라인에서 쓰는 하나의 셰이더 파일(Built-in 지원 유지) | 아니요 | 예 |
| 변환된 `.unitypackage` 내보내기 | 보고서만 | 예 |
| 명령줄 | 스캔, 캡처, 비교, Unity 컨버터, 무료 수정 | 전부 |

## 목차

여기서 시작하세요.
- [빠른 시작](quick-start.md): 데모 씬이나 내 프로젝트로 전체 과정을 5분 안에.
- [게임 마이그레이션](scenario-game.md)
- [셰이더 변환](scenario-shader.md)
- [Asset Store 패키지를 두 파이프라인용으로 변환](scenario-asset.md)

Urpify가 Built-in Render Pipeline의 각 구성 요소를 처리하는 방법:
- [구성 요소 개요](constructs/index.md): 구성 요소마다 한 페이지, 전후 이미지 포함.
- [변환되지 않는 것과 그 이유](not-converted.md)
- [URP에서 달라 보이는 점](urp-differences.md)

참고:
- [Urpify 창](window.md): 아이콘, 개수, 판정.
- [설정](settings.md)
- [명령줄](cli.md)
- [보고서 스키마](../report-schema.md)(영어)
- [Unity와 URP 버전](urp-version-matrix.md)
- [자주 묻는 질문](faq.md)
- [변환한 셰이더 재배포](../license-redistribution.md)(영어)

## 요구 사항

- Unity 2022.3 LTS(URP 14) 또는 Unity 6000.0 LTS, 6000.3 LTS, 6000.5, 6000.6(URP 17). 테스트한 버전입니다. [Unity와 URP 버전](urp-version-matrix.md)을 참고하세요.
- Built-in Render Pipeline을 쓰는 프로젝트, 또는 그것을 위해 만든 `.unitypackage`. 마이그레이션에 필요하면 Urpify가 URP를
  직접 설치합니다.
- 다른 패키지는 필요 없습니다. Urpify는 `보고서 보내기`로 직접 보내기로 한 셰이더 압축 파일 외에는 네트워크로 아무것도
  보내지 않습니다([자주 묻는 질문](faq.md) 참고).

## 언어

창은 English, 简体中文, 日本語, 한국어, Русский, Español, Português (Brasil), Deutsch를 지원합니다. 기본적으로 에디터 언어
(Preferences > Languages)를 따르며, Urpify에 그 언어가 없으면 영어를 씁니다. Project Settings > Urpify > 개인 > 언어에서
바꿀 수 있습니다. 보고서의 컨버터 메모, 명령줄, HTML 보고서의 기술 세부 정보는 영어로 유지됩니다.
