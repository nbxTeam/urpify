# 카메라를 렌더링하는 스크립트: 거울, 포털, 물 반사

![부스 M01: 평면 거울. Unity 컨버터 후에는 검고, Urpify 후에는 다시 비침](../../images/M01.jpg)

*부스 M01: MirrorReflection과 Standard Assets Water와 같은 방식으로 쓴 평면 거울: `OnWillRenderObject`, `Camera.current`, 비스듬한
근거리 클리핑 평면을 가진 반사 카메라, `Camera.Render`. Unity 컨버터만 거치면 거울이 검습니다.*

## 무엇이 깨지는가

URP는 카메라의 `OnPreCull`, `OnPreRender`, `OnPostRender`를 호출하지 않으며, `OnWillRenderObject`는 다른 카메라를 렌더링할
수 없는 컬링 도중에 호출합니다. 이 메서드에서 두 번째 카메라를 렌더링하는 포털과 거울은 평평하거나 검은 면을 보여 줍니다.
오류는 나지 않습니다. URP 14와 17에서 확인했습니다.

발견 사항: "카메라의 OnPreCull" 등(`SCRIPT.CAMERA_CALLBACKS`), "OnWillRenderObject가 카메라를 렌더링함"
(`SCRIPT.WILLRENDER_CAMERA`).

## Urpify가 하는 일(Pro)

스크립트는 메서드와 로직을 유지합니다. Urpify는 이를 URP의 카메라 이벤트에 등록합니다.

- `OnPreCull`과 `OnPreRender`는 해당 카메라의 `RenderPipelineManager.beginCameraRendering`에서, `OnPostRender`는
  `endCameraRendering`에서 호출됩니다.
- `OnWillRenderObject`는 URP 카메라의 컬링 전에, 그 카메라가 보는 렌더러에 대해 호출되며, `Camera.current`는 그 카메라를
  가리킵니다.
- 그 안의 `Camera.Render()`는 렌더 요청을 통해 URP 프레임에 렌더링하므로, 반사나 포털 텍스처가 그것을 보여 주는 카메라가
  그리기 전에 준비됩니다.
- `OnRenderObject`는 카메라의 불투명·투명 오브젝트 다음에 호출됩니다.

Urpify의 카탈로그는 알려진 스크립트를 인식합니다: SebLague의 Portals, MirrorReflection, Standard Assets Water, ToonWaterShader의
노멀 카메라는 이 프로젝트들에서 확인한 방식으로 변환됩니다. 나머지는 같은 메커니즘을 쓰며 "자동, 근사"가 됩니다.

## 얼마나 가까운가

카탈로그에서 확인한 방식은 `자동`, 나머지는 `자동, 근사`: 그런 씬은 씬 비교에서 확인하세요. Built-in과 한 가지 다른 점이
있습니다: `OnWillRenderObject`는 오클루전 컬링 없이 카메라 절두체 안의 렌더러에 대해 호출되므로, 벽 뒤에 숨은 거울도 반사를
렌더링합니다.

`Camera.onPreCull`, `Camera.onPreRender`, `Camera.onPostRender` 델리게이트에 등록한 코드는 아직 변환하지 않습니다. 옮기는
방법은 발견 사항에 있습니다. 카메라를 렌더링하는 `OnRenderImage`는 URP에서 실행되지 않습니다.
[변환되지 않는 것](../not-converted.md)을 참고하세요.
