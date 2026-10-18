# 라이팅 없는 vert/frag 셰이더

![부스 V01: 안개와 UV 스크롤이 있는 라이팅 없는 CG 셰이더. 세 이미지 모두 같음](../../images/V01.jpg)

*부스 V01: 안개와 UV 스크롤이 있는 라이팅 없는 CG 셰이더. URP가 그대로 그립니다.*

## 무슨 일이 일어나는가

LightMode 태그가 없거나 `Always`인 vert/frag 셰이더는 URP에서 대개 그대로 렌더링됩니다: URP는 LightMode 태그가 없는 패스를
`SRPDefaultUnlit`으로 그리고, `UnityCG.cginc`를 쓰는 CG 코드도 그대로 컴파일됩니다. Urpify는 셰이더가 URP에서 컴파일되는지
확인하고, 컴파일되면 건드리지 않습니다.

발견 사항: 없음, 또는 URP가 셰이더를 그대로 그린다는 메모.

그래도 깨질 수 있고, 각각 전용 페이지가 있는 경우:
- LightMode가 없는 패스가 두 개 이상: [추가 패스](extra-passes.md)
- `_CameraDepthTexture` 읽기: [깊이 텍스처](depth-texture.md)
- `GrabPass`: [GrabPass](grabpass.md)
- 라이팅 없는 패스에서 쓰는 `_LightColor0` 같은 Built-in 라이트 변수: URP에서는 0입니다. Pro가 URP의 메인 라이트에
  연결합니다.
