# 이미지 효과: OnRenderImage

![부스 C02: 채도를 낮추고 비네트를 거는 이미지 효과. Unity 컨버터 후에는 사라지고, Urpify 후에는 돌아옴](../../images/C02.jpg)

*부스 C02: `OnRenderImage`를 쓰는 포스트 효과(채도 감소와 비네트). Unity 컨버터만 거치면 효과가 오류 없이 사라집니다.*

## 무엇이 깨지는가

URP는 `OnRenderImage`를 호출하지 않으므로, Built-in용으로 작성한 모든 이미지 효과가 동작하지 않게 됩니다. 오류도 분홍색도
없고, 게임이 그냥 다르게 보입니다. URP 14와 17에서 확인했습니다.

발견 사항: "OnRenderImage(Blit 호출 {0}회)"(`SCRIPT.ONRENDERIMAGE`), 심각도 "오류 없이 깨짐". 효과의 셰이더에는 발견 사항
"이미지 효과 셰이더"가 붙습니다.

## Urpify가 하는 일(Pro)

스크립트는 카메라의 MonoBehaviour로 남아 계속 조작판 역할을 합니다: 필드, 매 프레임 설정하는 값, 머티리얼은 바뀌지 않습니다.
Urpify의 Built-in Camera Hooks 렌더러 기능이 설정에서 고른 URP 프레임의 위치("화면 효과 삽입 위치": 기본값은 포스트 프로세싱
후)에서, 카메라 색을 소스로 스크립트의 `OnRenderImage`를 호출합니다. 스크립트의 `Graphics.Blit` 호출은 `ImageEffectGraphics`를
거쳐 같은 머티리얼, 패스, 임시 렌더 텍스처로, 같은 순서로 URP 프레임에 기록됩니다. `[ImageEffectOpaque]`가 붙은 효과는
Built-in처럼 하늘 다음에 실행됩니다. 한 카메라의 여러 효과는 순서를 유지합니다. `OnRenderImage` 안의 `GL` 그리기(쿼드, 행렬,
클리어)도 기록됩니다.

효과의 셰이더는 그대로입니다: 효과가 Built-in처럼 화면을 `_MainTex`로 계속 넘기므로 `vert_img`, `v2f_img`, `_MainTex`가
동작합니다.

## 얼마나 가까운가

Blit이 하나면 `자동`, 체인이나 커맨드 버퍼면 `자동, 근사`. `Graphics.DrawMeshNow` 등으로 즉시 그리거나 카메라를 렌더링하는
(`Camera.Render`) 효과는 URP 프레임에 기록할 수 없습니다: 발견 사항이 구조를 바꾸는 방법을 알려 줍니다.

효과가 호출하는 다른 스크립트의 `Graphics.Blit` 호출(카메라 스크립트 뒤의 ScriptableObject 효과, 체인을 실행하는
매니저)도 같은 방식으로 기록됩니다. 체인이 카메라를 렌더링하거나, 컴퓨트 셰이더를 디스패치하거나, Built-in의
G-버퍼(`_CameraGBufferTexture0-3`)를 읽는 효과는 그대로 둡니다(아직 지원 안 함). URP에서는 효과가 빠지고 나머지
프레임은 그려집니다. 마이그레이션 후 씬의 프레임이 거의 한 가지 색이 되면 그 씬의 효과는 검토 대상으로 표시됩니다.
카메라에서 꺼서 원인을 찾거나 롤백하세요.

빌드한 플레이어도 효과를 유지합니다: Urpify가 이름으로 호출하는 모든 것은 코드 스트리핑으로부터 보호되며, Mono와 IL2CPP
빌드에서 확인했습니다.
