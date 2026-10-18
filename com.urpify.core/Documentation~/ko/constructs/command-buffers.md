# 카메라 이벤트의 커맨드 버퍼

![부스 C03: 커맨드 버퍼가 그리는 빛나는 셸. Unity 컨버터 후에는 사라지고, Urpify 후에는 돌아옴](../../images/C03.jpg)

*부스 C03: `CameraEvent.AfterForwardAlpha`에서 카메라에 추가한 `CommandBuffer`가 그리는 빛나는 셸.*

## 무엇이 깨지는가

URP는 `Camera.AddCommandBuffer`를 무시합니다: 버퍼가 그리는 것은 아무 알림 없이 사라집니다.

발견 사항: "... 의 CommandBuffer"(`SCRIPT.COMMANDBUFFER.CAMERA`).

## Urpify가 하는 일(Pro)

`camera.AddCommandBuffer(evt, cb)`는 `camera.AddCommandBufferInUrp(evt, cb)`가 됩니다(`RemoveCommandBuffer`도 같음). Urpify의
Built-in Camera Hooks 렌더러 기능이 Built-in 이벤트에 대응하는 URP 이벤트에서 버퍼를 실행합니다.

| Built-in의 `CameraEvent` | URP의 `RenderPassEvent` |
|---|---|
| `BeforeDepthTexture`, `AfterDepthTexture` | `BeforeRenderingPrePasses`, `AfterRenderingPrePasses` |
| `BeforeForwardOpaque`, `AfterForwardOpaque` | `BeforeRenderingOpaques`, `AfterRenderingOpaques` |
| `BeforeSkybox`, `AfterSkybox` | `BeforeRenderingSkybox`, `AfterRenderingSkybox` |
| `BeforeForwardAlpha`, `AfterForwardAlpha` | `BeforeRenderingTransparents`, `AfterRenderingTransparents` |
| `BeforeImageEffectsOpaque`, `AfterImageEffectsOpaque` | `AfterRenderingSkybox` |
| `BeforeImageEffects`, `AfterImageEffects`, `AfterEverything` | `BeforeRenderingPostProcessing`, 포스트 프로세싱 후, `AfterRendering` |
| 디퍼드 경로의 이벤트(`BeforeGBuffer`, `AfterLighting` 등) | 불투명 오브젝트 전후로, URP 포워드 렌더러에 있는 가장 가까운 위치 |

`DrawRenderer`, `DrawMesh`, `Blit`, `SetGlobal*`, 임시 렌더 텍스처, `SetRenderTarget`, `ClearRenderTarget`은 기록된 대로
실행됩니다.

## 얼마나 가까운가

`자동, 근사`. 디퍼드 경로의 이벤트는 성격상 근사입니다. 라이트의 커맨드 버퍼(`Light.AddCommandBuffer`)는 아직 지원하지
않습니다. [변환되지 않는 것](../not-converted.md)을 참고하세요.
