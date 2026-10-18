# depth-normals 텍스처와 대체 셰이더

이 구성 요소에는 랩의 부스가 없습니다. 둘 다에 의존하는 물 거품이 있는 ToonWaterShader(IronWarrior, Unlicense)로
확인했습니다.

## 무엇이 깨지는가

- URP에는 `_CameraDepthNormalsTexture`가 없습니다. 이를 읽는 셰이더는 아무것도 받지 못합니다: 노멀 기반 외곽선과 물가의
  거품이 사라집니다.
- URP는 `Camera.SetReplacementShader`와 `Camera.RenderWithShader`를 지원하지 않습니다. 대체 셰이더로 씬을 텍스처에 렌더링하는
  스크립트(효과에 쓸 노멀이나 깊이를 얻는 흔한 방법)는 아무 알림 없이 쓸모 있는 것을 렌더링하지 않습니다. ToonWaterShader는
  이 때문에 거품을 잃었습니다.

발견 사항: "_CameraDepthNormalsTexture 읽기"(`SHADER.DEPTHNORMALS_TEXTURE`), 대체 셰이더 호출(`SCRIPT.REPLACEMENT_SHADER`).

## Urpify가 하는 일(Pro)

- Urpify의 Built-in Camera Hooks 렌더러 기능이 URP의 깊이·노멀 텍스처로부터 Built-in의 인코딩(`EncodeDepthNormal`)으로
  `_CameraDepthNormalsTexture`를 쓰므로, 셰이더는 예전처럼 읽을 수 있습니다. 이 기능은 셰이더가 필요로 할 때만 URP에 노멀
  텍스처를 요청합니다.
- 대체 셰이더를 쓰는 카메라는 같은 렌더러 기능의 패스가 됩니다. 이 패스는 카메라의 불투명 오브젝트를 대체 셰이더로 같은
  대상에 그리며, 태그 대응(`RenderType`)도 같습니다.

## 얼마나 가까운가

`자동, 근사`. URP의 노멀은 URP 자체의 depth-normals 패스에서 오므로, 가장자리가 Built-in과 조금 다를 수 있습니다.
