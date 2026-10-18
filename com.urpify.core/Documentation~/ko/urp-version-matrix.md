# Unity와 URP 버전

| Unity | URP | 상태 | 확인 환경 |
|---|---|---|---|
| 6000.6 | 17.6 | 지원 | 6000.6.5f1과 URP 17.6.0: 단위 테스트, 부스 37개, 데모, 실제 프로젝트 4개 |
| 6000.5 | 17.5 | 지원 | 6000.5.9f1과 URP 17.5.0: 단위 테스트, 부스 37개, 실제 프로젝트, 셰이더 코퍼스 |
| 6000.4 | 17.4 | 테스트하지 않음 | |
| 6000.3 LTS | 17.3 | 지원 | 6000.3.26f1과 URP 17.3.0: 단위 테스트, 부스 37개, 데모, 실제 프로젝트 4개. 이 버전에서는 Unity 자체의 일괄 컨버터가 실패하며, Urpify가 이를 우회합니다(아래) |
| 6000.1, 6000.2 | 17.1, 17.2 | 테스트하지 않음 | |
| 6000.0 LTS | 17.0 | 지원 | 6000.0.84f1과 URP 17.0.4: 단위 테스트, 부스 37개, 데모, 실제 프로젝트 4개 |
| 2022.3 LTS | 14 | 지원 | 2022.3.62f3과 URP 14.0.12: 6000.5와 같은 테스트 세트 |
| 2021.3 LTS | 12 | 1.0에서 지원 안 함 | 요청이 있으면 계획 |

출시할 때 쓸 에디터 버전에서 마이그레이션하세요: 전후 캡처는 같은 Unity 버전 안에서만 비교할 수 있습니다.

## 6000.0과 6000.3에서 Unity 컨버터와 종료 코드

- URP 17.0과 17.3에는 Unity 컨버터의 예전 일괄 API(`ConverterContainerId`)가 있습니다. URP 17.3.0(6000.3.26f1)에서는 어떤 프로젝트에서든
  아무것도 변환하기 전에 추상 클래스 `Base2DMaterialUpgrader` 때문에 `MissingMethodException`을 던집니다. 그러면 Urpify가 Built-in에서 URP로
  가는 컨버터 목록을 직접 만들어 같은 방식으로 실행하며, 단계의 세부 정보에 그렇게 표시됩니다.
- Unity 6000.0.84f1과 6000.3.26f1은 자주, 6000.5.9f1은 가끔 작업을 마친 뒤 일괄 모드를 종료하면서 충돌합니다(종료 코드 139). Urpify 유무와 관계없습니다.
  명령줄에서는 `Urpify/urpify-<command>-result.txt`를 읽으세요([명령줄](cli.md) 참고).

## URP 14와 URP 17의 차이

Urpify의 셰이더 라이브러리(`BirpCompat*.hlsl`)와 렌더러 기능은 아래의 모든 차이를 고려하므로, 변환된 셰이더는 두 버전 모두에서
컴파일되고 렌더링됩니다. 각 행은 URP 14.0.12(2022.3.62f3)와 URP 17.5.0(6000.5.9f1)의 패키지 소스에서 확인했습니다.

| 주제 | URP 14.0.12 | URP 17.5.0 | 확인 위치 |
|---|---|---|---|
| 메인 라이트 | `GetMainLight()`, `(shadowCoord)`, `(shadowCoord, positionWS, shadowMask)`, `(inputData, shadowMask, aoFactor)` | 같은 4개 오버로드 | `ShaderLibrary/RealtimeLights.hlsl` |
| 추가 라이트 | `GetAdditionalLight(i, positionWS)`, `(i, positionWS, shadowMask)`, `(i, inputData, shadowMask, aoFactor)` | 같은 3개 오버로드 | `ShaderLibrary/RealtimeLights.hlsl` |
| Forward+ 라이트 루프 | `USE_FORWARD_PLUS` | `USE_CLUSTER_LIGHT_LOOP`. `USE_FORWARD_PLUS`는 `ForwardPlusKeyword.deprecated.hlsl`에 별칭으로 남음 | `ShaderLibrary/RealtimeLights.hlsl`, `Core.hlsl` |
| 추가 라이트 그림자 | `AdditionalLightRealtimeShadow(lightIndex, positionWS, lightDirection)` | 같은 것에 더해 그림자 파라미터와 샘플링 데이터를 받는 오버로드 | `ShaderLibrary/Shadows.hlsl` |
| 구면 조화 함수 | URP 패키지 `GlobalIllumination.hlsl`의 `SampleSH(normalWS)` | Core 패키지 `AmbientProbe.hlsl`의 `SampleSH(normalWS)` | URP와 Core의 `ShaderLibrary` |
| 라이트맵 | `LIGHTMAP_ON`, `DIRLIGHTMAP_COMBINED` | 같은 키워드 | `ShaderLibrary/GlobalIllumination.hlsl` |
| LOD 크로스페이드 | `LODCrossFade.hlsl` | `LODCrossFade.hlsl` | `ShaderLibrary/` |
| 데칼 | `DBuffer.hlsl` | `DBuffer.hlsl` | `ShaderLibrary/` |
| 깊이·노멀·불투명 텍스처 | `DeclareDepthTexture.hlsl`, `DeclareNormalsTexture.hlsl`, `DeclareOpaqueTexture.hlsl` | 같은 파일 | `ShaderLibrary/` |
| 렌더러 기능 | `ScriptableRenderPass.Execute`. Render Graph는 내부용 | Render Graph(`RecordRenderGraph`). Unity 6.4부터 호환 모드 없음: `RenderGraphSettings.enableRenderCompatibilityMode`는 항상 false 반환 | `Runtime/Passes/ScriptableRenderPass.cs`, `Runtime/Settings/RenderGraphSettings.cs` |
| 미리 보기 카메라의 Opaque Texture | 미리 보기 카메라에는 복사하지 않음(`copyColorPass &= !isPreviewCamera`) | 복사함 | `Runtime/UniversalRenderer.cs`. 그래서 2022.3에서 Urpify의 자체 검사는 GrabPass 셰이더를 게임 카메라로 렌더링합니다. |
| URP Asset 텍스처 | URP Asset의 `m_Textures`(blue noise, Bayer)가 Unity 컨버터가 저장한 대로 비어 있으면, 빌드한 플레이어가 매 프레임 `NullReferenceException`을 내고 검게 렌더링 | 같은 프로젝트의 6000.5 플레이어에서는 관찰되지 않음 | `UniversalRenderPipeline.cs`(`SetupPerFrameShaderConstants`). 무료 패키지가 Unity 컨버터 다음에 이를 채웁니다. |

## Unity 컨버터의 차이

| 주제 | 2022.3 / URP 14 | 6000.5 / URP 17.5 |
|---|---|---|
| `Legacy Shaders/Particles/VertexLit Blended`와 `Mobile/Bumped Specular (1 Directional Realtime Light)`의 머티리얼 변환 규칙 | 예전 셰이더 이름으로 등록되어 있어 Unity 컨버터가 건너뜀. Urpify가 자체 표로 이 머티리얼을 변환 | Unity가 직접 변환 |
| `-typesFilter` 없는 배치 모드 | 측정하지 않음: Urpify는 여기서도 Unity의 배치 명령을 쓰지 않음 | `NullReferenceException`(`Converters.cs:134`) 발생. Urpify는 API로 컨버터를 호출 |
| 작업 성공 후 Unity 배치 명령의 종료 코드 | 측정하지 않음 | 실제 프로젝트 4개 중 3개가 코드 21 또는 139로 종료(종료 시 프로세스 충돌). Urpify는 두 버전 모두에서 컨버터 로그와 재스캔으로 결과를 판단 |
