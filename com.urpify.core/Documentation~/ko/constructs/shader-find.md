# 내장 셰이더 이름을 쓰는 Shader.Find

![부스 C04: Shader.Find("Standard")로 코드에서 만든 머티리얼. Unity 컨버터 후에는 분홍색, Urpify가 수정](../../images/C04.jpg)

*부스 C04: 스크립트가 실행 중에 `new Material(Shader.Find("Standard"))`로 머티리얼을 만들고 `_Color`를 설정합니다.*

## 무엇이 깨지는가

Unity 컨버터가 업그레이드하는 것은 머티리얼 파일이지 코드가 아닙니다. `Shader.Find("Standard")`, `"Diffuse"`,
`"Legacy Shaders/..."` 같은 내장 셰이더 이름으로 머티리얼을 만드는 스크립트는 URP가 그릴 수 없는 셰이더를 받아, 오브젝트가
분홍색이 됩니다. 스크립트가 설정하는 프로퍼티 이름(`_MainTex`, `_Color`, `_Glossiness`)은 URP Lit에서 숨은 프로퍼티라서,
셰이더를 직접 바꿔도 이 호출들은 아무것도 바꾸지 못합니다.

발견 사항: `Shader.Find("...")`(`SCRIPT.SHADER_FIND_BUILTIN`), "코드의 Built-in 프로퍼티 이름"(`SCRIPT.BUILTIN_PROPERTY_NAMES`).

## Urpify가 하는 일(Free)

무료 패키지에 들어 있는, Unity 컨버터 이후의 수정 중 하나입니다. 같은 스크립트 안에서:

- 셰이더 이름을 Unity 컨버터가 그 내장 셰이더에 고르는 URP 셰이더(`Standard`는 `Universal Render Pipeline/Lit`)로, Unity
  규칙이 없는 셰이더는 Urpify 표에서 가장 가까운 URP 셰이더로 바꿉니다.
- 같은 메서드가 설정하는 프로퍼티 이름을 URP 이름(`_MainTex`는 `_BaseMap`, `_Color`는 `_BaseColor`)으로 바꿉니다.

출력이 "Built-in 지원 유지"이면 각 이름은 실행 중에 활성 파이프라인에 따라 골라지므로, 스크립트는 Built-in에서도 계속
동작합니다.

다시 쓴 스크립트는 저장하기 전에 컴파일합니다. 컴파일되지 않으면 원본을 복원하고 보고합니다.

## 얼마나 가까운가

`자동`.
