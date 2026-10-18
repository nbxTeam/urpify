# 테셀레이션

![부스 S09: 디스플레이스먼트가 있는 테셀레이션](../../images/S09.jpg)

*부스 S09: `tessellate:`와 디스플레이스먼트를 쓰는 서피스 셰이더.*

## 무엇이 깨지는가

`tessellate:func`는 Unity가 서피스 셰이더용 헐과 도메인 단계를 생성하게 합니다. URP에는 서피스 셰이더가 없으므로 오브젝트가
분홍색입니다.

발견 사항: "테셀레이션(...)"(`SHADER.SURFACE.TESSELLATION`).

## Urpify가 하는 일(Pro)

컨버터는 내장 파이프라인을 따르는 템플릿으로 헐과 도메인 단계를 생성합니다: 작성자의 테셀레이션 함수가 계수를 주고, 분할은
`fractional_odd`이며, 그다음 `vertex:` 함수가 새 정점을 옮깁니다. 오브젝트를 그리는 모든 패스(포워드, 그림자, 깊이)가
테셀레이션되므로 그림자도 변위된 표면을 따릅니다. `tessphong:`은 지정한 강도로 Phong 스무딩을 더합니다.

직접 헐과 도메인 단계를 쓴 vert/frag 셰이더는 이를 유지합니다. Built-in이 정의하는 속성(`UNITY_domain`,
`UNITY_partitioning`, `UNITY_outputtopology`, `UNITY_patchconstantfunc`, `UNITY_outputcontrolpoints`)은 Urpify 라이브러리에서
제공하며, 컴파일 검사는 헐과 도메인 단계도 포함합니다.

## 얼마나 가까운가

`자동, 근사`: 같은 계수와 같은 변위에 URP 라이팅이 더해집니다.
