# 커스텀 라이팅 함수

![부스 S05: 툰 램프 라이팅 함수. Unity 컨버터 후에는 분홍색, Urpify 후에는 같음](../../images/S05.jpg)

*부스 S05: 자체 라이팅 함수(툰 램프)를 가진 서피스 셰이더.*

## 무엇이 깨지는가

서피스 셰이더는 자체 라이팅 모델을 `#pragma surface surf Toon`으로 이름 붙이고 `LightingToon`을 정의할 수 있습니다.
Built-in은 이를 라이트마다 한 번 호출합니다: 메인 라이트는 ForwardBase에서, 각 추가 라이트는 ForwardAdd에서, `_LightColor0`과
라이트의 감쇠를 써서 호출합니다. URP에는 서피스 셰이더가 없으므로 오브젝트가 분홍색입니다.

발견 사항: "커스텀 라이팅 함수 ..."(`SHADER.SURFACE.CUSTOM_LIGHTING`).

## Urpify가 하는 일(Pro)

작성자의 함수는 바꾸지 않고 복사합니다. 생성된 포워드 패스는 URP의 메인 라이트에 대해, 이어서 각 추가 라이트에 대해 URP의
그림자와 감쇠를 써서 이 함수를 호출하고, 결과를 더합니다. Built-in의 두 패스가 하던 것과 같습니다. 함수 안에서
`_LightColor0`은 호출 대상 라이트의 색입니다.

Built-in의 모든 시그니처를 인식합니다.

| 시그니처 | 예 |
|---|---|
| 시선 방향 없음 | `half4 LightingToon(SurfaceOutput s, half3 lightDir, half atten)` |
| 시선 방향 있음 | `half4 LightingToon(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)` |
| 글로벌 일루미네이션 있음 | `half4 LightingToon(SurfaceOutput s, half3 viewDir, UnityGI gi)`와 그 `LightingToon_GI` |
| 디퍼드와 프리패스 변형 | `_Deferred`, `_PrePass`: 쓰지 않음. URP는 포워드로 렌더링 |

함수는 컴파일러가 찾을 수 있는 곳이면 어디서든 찾습니다: 프로그램 안, `CGINCLUDE` 블록, 어떤 깊이든 프로젝트의 include 파일,
함수를 선언하는 `#define` 매크로.

## 얼마나 가까운가

`자동, 근사`. Built-in은 추가 라이트마다 가산 블렌딩의 별도 패스로 더하고, URP는 한 패스에서 라이팅합니다. 불투명 오브젝트는
합계가 같고, 투명 셰이더는 라이트가 겹치는 곳에서 다를 수 있습니다. 주변광은 Built-in처럼 URP의 구면 조화 함수에서 옵니다.

함수가 셰이더에도, 스캔한 include 파일에도 없으면 발견 사항이 그 이름을 알려 주며, 그 include 파일이 스캔 범위에 들어올
때까지 셰이더는 변환되지 않습니다.
