# 변환되지 않는 것과 그 이유

Urpify는 변환하지 않는 모든 것을 이유와 함께, 마이그레이션을 시작하기 전에 나열합니다. 아래 각 항목은 세 그룹 중 하나에
속합니다.

- **수동: 마이그레이션 전부터 프로젝트가 깨져 있었음.** Urpify는 프로젝트 자체의 오류는 고치지 않습니다. 무엇이 깨졌고
  먼저 무엇을 해야 하는지 알려 줍니다.
- **수동: 변환할 것이 없음.** URP에 대응하는 것이 없으며, URP 소스에서 확인했습니다.
- **아직 지원 안 함.** 가능하지만 이 버전의 Urpify는 하지 않습니다. 다음에 작업할 내용이며, 그동안 직접 할 일은 발견 사항에
  적혀 있습니다.

개발자가 보지 않은 셰이더로 한 독립 테스트에서 측정한, Urpify가 변환하는 셰이더의 비율은 제품 페이지에 있습니다.
이 페이지는 변환되지 않는 것과 그 이유를 나열합니다.

## 마이그레이션 전부터 프로젝트가 깨져 있었음

| 경우 | 보이는 내용 | 할 일 |
|---|---|---|
| 내장 파이프라인에서 셰이더가 컴파일되지 않음 | "셰이더가 이미 컴파일되지 않음" | 먼저 Built-in에서의 컴파일 오류를 고치세요. 오브젝트는 이미 분홍색입니다. |
| 프로젝트 스크립트가 컴파일되지 않음 | "프로젝트 스크립트가 컴파일되지 않음" | 먼저 고치세요: Play Mode, 캡처, 스크립트를 다시 쓰는 단계를 막습니다. 오래된 프로젝트는 폐기된 API 호출을 업데이트해야 하는 경우가 많습니다. |
| 셰이더가 프로젝트에 없는 파일을 include함 | "include를 찾을 수 없음" | 파일을 추가하거나 경로를 고친 뒤 다시 스캔하세요. |
| `UsePass`가 프로젝트에 없는 셰이더를 가리킴 | "UsePass ...: 셰이더를 찾을 수 없음" | 셰이더를 추가하거나 `UsePass` 줄을 지우세요. 오브젝트는 Built-in에서도 분홍색입니다. |
| 렌더러가 프로젝트에 없는 머티리얼을 참조함 | "렌더러 ...개에 머티리얼 누락" | 머티리얼을 지정하거나, 빠진 머티리얼이나 패키지를 되찾으세요. Unity가 패키지(예: ProBuilder)를 업그레이드하면서 예전 기본 머티리얼이 사라졌을 때 생깁니다. |

## 변환할 것이 없음

| 경우 | 이유 |
|---|---|
| 내장 렌더러의 내부 셰이더: `Internal-DeferredShading`, `Internal-DeferredReflections`, `Internal-ScreenSpaceShadows`, `Internal-DepthNormalsTexture`, `Internal-MotionVectors`, `Internal-PrePassLighting`, `Internal-Flare`, `Internal-Halo`와 그 사본 | Built-in은 이들을 Graphics Settings의 내장 셰이더 슬롯에서 실행합니다. URP는 그 슬롯을 읽지 않습니다: URP 14.0.12와 17.5.0의 소스는 `GraphicsSettings.GetCustomShader`를 호출하지 않고 `BuiltinShaderType`도 쓰지 않습니다. URP는 디퍼드 라이팅, 그림자, depth-normals, 모션 벡터를 자체 셰이더로 렌더링합니다. |
| LightMode가 `Deferred`, `PrepassBase`, `PrepassFinal`인 패스 | Built-in의 디퍼드와 레거시 디퍼드 경로입니다. URP는 오브젝트를 포워드 패스로 렌더링하며, Urpify는 셰이더의 포워드 패스로부터 그 패스를 만듭니다. |
| 서피스 셰이더의 `finalgbuffer:` 함수 | Built-in의 디퍼드 G-버퍼 패스에서만 실행됩니다. 변환된 포워드 셰이더는 이를 호출하지 않습니다. |
| `Camera.depthTextureMode` | URP는 이를 무시합니다. URP는 URP Asset 설정에 따라 깊이·노멀 텍스처를 만들고, 셰이더가 읽을 때 Urpify가 이를 켭니다. |

## 아직 지원 안 함

| 구성 요소 | 지금 일어나는 일 | 직접 할 일 |
|---|---|---|
| `Light.AddCommandBuffer`로 동작하는 효과(볼류메트릭 라이트 등) | URP에서 효과가 빠집니다. 나머지 프레임은 렌더링됩니다. | 라이트의 명령을 `BeforeRenderingShadows` 또는 `AfterRenderingShadows`의 렌더러 기능으로 옮기세요. URP는 섀도 맵을 다르게 렌더링하므로, 명령이 무엇을 전제로 하는지 확인하세요. |
| `OnRenderImage`가 카메라 렌더링, 컴퓨트 디스패치 등 즉시 실행되는 그리기에 닿는 이미지 효과 | URP에서는 효과가 빠지고 나머지 프레임은 그려집니다. | 그 호출을 효과 밖으로 옮깁니다: `OnPreRender`로, 또는 `ImageEffectGraphics.ExecuteCommandBuffer`로 실행하는 커맨드 버퍼로. |
| 셰이더가 Built-in의 G-버퍼(`_CameraGBufferTexture0-3`: 스크린 스페이스 반사 등)를 읽는 이미지 효과 | URP에서는 효과가 빠지고 나머지 프레임은 그려집니다. | URP의 깊이·노멀·불투명 텍스처로 입력을 다시 만들거나, URP의 Deferred 렌더러와 그 G-버퍼 배치를 사용합니다. |
| `Camera.onPreCull`, `Camera.onPreRender`, `Camera.onPostRender` 델리게이트 | 여기에 등록한 코드가 더 이상 실행되지 않습니다. | `RenderPipelineManager.beginCameraRendering` 또는 `endCameraRendering`으로 옮기세요. |
| 즉시 그리는 `OnRenderImage`(`Graphics.DrawMeshNow` 등) | 효과가 빠집니다. | 커맨드 버퍼(`CommandBuffer.Blit`, `DrawMesh`)로 그리고 `ImageEffectGraphics.ExecuteCommandBuffer`로 실행하세요. |
| 카메라를 렌더링하는 `OnRenderImage`(`Camera.Render`, 도우미 메서드를 거치는 경우 포함) | URP에서 효과를 실행하지 않습니다: 자기 카메라의 프레임 안에서는 다른 카메라를 렌더링할 수 없어 프레임이 검게 나오기 때문입니다. | 다른 카메라는 `OnPreRender`나 `OnPreCull`에서 렌더링하고, `OnRenderImage`에는 Blit 호출만 남기세요. 카메라의 스텐실 버퍼를 테스트하는 Blit도 URP에서는 이를 볼 수 없습니다. |
| 라이트에 붙인 Built-in 디퍼드 라이팅 이벤트의 커맨드 버퍼 | 빠집니다. | 대응하는 URP 이벤트의 렌더러 기능을 쓰세요. |
| Built-in의 G-버퍼(`_CameraGBufferTexture0`~`3`)를 읽는 셰이더(스크린 스페이스 GI 등) | 효과가 오류 없이 입력을 잃습니다: URP의 Forward 렌더러에는 G-버퍼가 없고, URP의 Deferred 경로는 자체 G-버퍼를 다른 배치(`_GBuffer0`~`_GBuffer6`)로 씁니다. | URP의 깊이, 노멀, 불투명 텍스처로 입력을 다시 만들거나, URP 렌더러를 Deferred로 바꾸고 `UnityGBuffer.hlsl`에 맞게 읽기를 다시 작성하세요. |
| 한 셰이더 안에 텍스처가 다른 GrabPass 여러 개 | 셰이더가 변환되지 않습니다. | URP의 Opaque Texture는 하나입니다: 캡처를 합치거나, 하나만 남기고 두 번 읽으세요. |
| 한 SubShader에 그리는 패스가 32개 초과 | 변환되지 않습니다. | 패스를 합치거나, 일부를 별도 SubShader나 머티리얼로 옮기세요. |
| 패스가 `CGINCLUDE` 블록에 있는 셰이더에서의 `UsePass` | 변환되지 않습니다. | 패스를 셰이더에 복사하세요. |
| Urpify에 아직 대응하는 것이 없는 내장 include | "Stopped: ... includes ..."가 표시되고 셰이더가 변환되지 않습니다. | include의 함수를 직접 옮기거나, 보고서 보내기로 셰이더를 보내 주세요. |
| Built-in 라이트 변수 일부를 직접 읽거나, 정점 단위로 라이팅하거나, 색을 반환하지 않는 `ForwardAdd` 패스 | 변환되지 않습니다. | `UNITY_LIGHT_ATTENUATION`으로 패스를 다시 쓰거나, URP의 추가 라이트 루프로 옮기세요. |
| 컨버터가 모르는 시그니처의 라이팅 함수 | 변환되지 않습니다. | Built-in 문서에 있는 `Lighting<Name>` 시그니처 중 하나를 쓰세요. |
| 고정 기능 파이프라인의 패스(셰이더 프로그램 없음) | 변환되지 않습니다: URP는 프로그램이 있는 패스만 그립니다. | 패스를 vert/frag 프로그램으로 다시 쓰세요. |
| Unity 컨버터 전에 파일이 백업에 없는 Material Variant | 고유 값이 Unity 컨버터에서 사라집니다. | 그 파일을 저장하는 Urpify 단계로 마이그레이션하거나, 값을 다시 설정하세요. |
| 변환 후 컴파일되지 않는 셰이더나 스크립트 | 원본이 복원되고, 컴파일러 메시지가 발견 사항에 담깁니다. | 셰이더 패널에서 결과를 고쳐 다른 이름으로 저장하거나, 보고서 보내기로 셰이더를 보내 주세요. |

## 변환되지만 똑같지는 않은 것

다음은 변환되어 동작하지만, 변환이 아니라 URP 자체에서 오는 차이가 있습니다. 이미지는
[URP에서 달라 보이는 점](urp-differences.md)에 있습니다.

- **Opaque Texture를 쓰는 GrabPass**(기본값): URP는 투명 오브젝트 전에 화면을 복사하므로, 굴절에 투명 오브젝트가 보이지
  않습니다. 나중에 복사하려면 GrabPass 소스를 "투명 오브젝트 후"로 선택하세요.
- **커스텀 라이팅 함수와 ForwardAdd 패스:** 작성자의 함수가 메인 라이트와 각 추가 라이트에 대해 한 패스 안에서, URP의
  그림자와 감쇠를 써서 실행됩니다.
- **Post-processing Stack v2의 블룸:** URP 블룸이 되며, 광채가 더 부드럽고 넓습니다. URP의 블룸 피라미드는 작은 밝은 점을
  다르게 퍼뜨립니다.
- **Standard에서 URP Lit**(Unity 자체의 변환): 저희 테스트 씬에서 프레임의 최대 약 7 %가 다릅니다.
- **`OnWillRenderObject`의 반사**는 카메라가 보는 렌더러에 대해 오클루전 컬링 없이 URP 컬링 전에 호출되므로, 벽 뒤의
  반사도 렌더링됩니다.
