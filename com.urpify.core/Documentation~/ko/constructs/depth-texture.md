# 깊이 텍스처

![부스 D01: 교차부에 거품이 있는 물. Unity 컨버터 후에는 하얗고, Urpify 후에는 Built-in과 같음](../../images/D01.jpg)

*부스 D01: 깊이 색과 바위 주변 거품을 위해 `_CameraDepthTexture`를 읽는 투명한 물. Unity 컨버터만 거치면 물이 하얗게
됩니다.*

## 무엇이 깨지는가

`_CameraDepthTexture`를 읽는 셰이더는 URP Asset에서 Depth Texture가 켜져 있을 때만 깊이를 받습니다. Unity 컨버터는 Soft
Particles를 쓰던 품질 수준에서만 이를 켭니다. 투명 셰이더에는 두 번째 문제가 있습니다: URP는 기본적으로 투명 오브젝트 다음에
깊이를 복사하며, Unity 컨버터는 이를 그대로 둡니다. Unity 6에서는 그런 셰이더가 깊이를 전혀 받지 못해 물이 하얗게 되고,
2022.3에서는 이전 프레임의 깊이를 읽어 정지한 프레임은 맞아 보여도 움직이는 오브젝트가 자국을 남깁니다.

발견 사항: "_CameraDepthTexture 읽기"(`SHADER.DEPTH_TEXTURE`), "투명, ... 읽기"(`SHADER.DEPTH_TEXTURE_TRANSPARENT`).

## Urpify가 하는 일

- **Free와 Pro:** 모든 URP Asset에서 Depth Texture를 켜고, 투명 셰이더가 읽는 경우 렌더러의 Depth Texture Mode를 After
  Opaques로 설정합니다. URP가 그대로 그리는 셰이더는 코드를 바꾸지 않고도 동작하게 됩니다: 부스 D01은 무료 패키지만으로
  고쳐집니다.
- **Pro(셰이더를 변환할 때):** 작성자의 `sampler2D _CameraDepthTexture` 선언을 URP의 `DeclareDepthTexture.hlsl`로 바꾸고,
  읽기(`tex2D`, `tex2Dproj`, `SAMPLE_DEPTH_TEXTURE`, `SAMPLE_DEPTH_TEXTURE_PROJ`)를 `SampleSceneDepth`로 바꿉니다. 그래서 깊이
  텍스처가 배열인 싱글 패스 인스턴스드 XR에서도 동작합니다. `LinearEyeDepth`, `Linear01Depth`, `COMPUTE_EYEDEPTH`는 Urpify
  라이브러리에서 제공합니다.

## 얼마나 가까운가

`자동`. 다시 쓸 수 없는 읽기가 있으면 작성자의 선언이 남고, 발견 사항이 그 셰이더에서는 싱글 패스 인스턴스드 XR을 지원하지
않는다고 알려 줍니다.
