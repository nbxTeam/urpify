# 라이팅 있는 vert/frag 셰이더

![부스 V02: ForwardBase, ForwardAdd, 그림자가 있는 라이팅 vert/frag 셰이더](../../images/V02.jpg)

*부스 V02: ForwardBase와 ForwardAdd 패스, `SHADOW_ATTENUATION`, 그림자를 드리우는 패스.*

## 무엇이 깨지는가

URP는 `ForwardBase`, `ForwardAdd`, `Vertex`, `VertexLM`, `PrepassBase` 태그가 붙은 패스를 오류 셰이더로 그리므로 오브젝트가
분홍색이 됩니다. Built-in의 라이트 변수(`_LightColor0`, `_WorldSpaceLightPos0`), 그림자 매크로(`SHADOW_COORDS`,
`TRANSFER_SHADOW`, `SHADOW_ATTENUATION`, `UNITY_LIGHT_ATTENUATION`), `UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"`는
URP에 대응하는 것이 없습니다.

발견 사항: "ForwardBase 패스", "ForwardAdd 패스", "Built-in의 ShadowCaster 패스", "LightMode ...", "UsePass ...".

## Urpify가 하는 일(Pro)

| Built-in | URP |
|---|---|
| `ForwardBase` | `UniversalForward`. 라이팅, 그림자, 주변광은 Built-in 이름을 통해 URP에서 받음 |
| `ForwardAdd` | 포워드 패스 안에서 URP의 추가 라이트를 도는 루프: 작성자의 프래그먼트 코드를 라이트마다 한 번 호출하고 결과를 더함 |
| `ShadowCaster` | `ApplyShadowBias`를 쓰는 URP의 `ShadowCaster`. 작성자의 정점 코드는 유지. `DepthOnly` 사본이 깊이 프리패스를 그림 |
| `UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"` | 생성한 그림자 패스 |
| 프로젝트의 다른 셰이더에서 가져온 `UsePass` | 패스를 복사해 나머지와 함께 변환 |
| `Vertex`, `VertexLM`(정점 라이팅만) | `UniversalForward`. URP의 메인 라이트와 추가 라이트로 정점 단위 라이팅 |
| `_LightColor0`, `_WorldSpaceLightPos0` | 패스를 그리는 대상 라이트 |
| `UNITY_LIGHT_ATTENUATION`, `SHADOW_ATTENUATION`, `LIGHT_ATTENUATION` | URP의 그림자와 거리 감쇠 |
| `ShadeSH9`, `UNITY_FOG_*`, `UnityObjectToClipPos`와 나머지 `UnityCG.cginc` | Urpify의 라이브러리 `BirpCompat.hlsl` |

작성자의 `vert`와 `frag`는 바꾸지 않고 복사합니다. CG 블록은 HLSL 블록이 되고, `fixed`는 `half`가 되며, `sampler2D`/`tex2D`는
그대로 둡니다: URP 14와 17에서 컴파일됩니다.

## 얼마나 가까운가

ForwardBase 패스 하나뿐이면 `자동`, ForwardAdd가 있으면 `자동, 근사`: Built-in은 추가 라이트마다 별도 패스로 블렌딩하고, URP는
한 패스에서 더합니다. Built-in이 ForwardBase에 넘기는 정점 라이트(`Shade4PointLights`)는 루프에서 픽셀 단위로 그립니다.
