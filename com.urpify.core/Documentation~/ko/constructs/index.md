# 구성 요소

Urpify는 개별 셰이더가 아니라 구성 요소를 지원합니다: 셰이더가 쓰는 모든 구성 요소가 지원되면 변환되고, 그렇지 않으면 스캔이
빠진 것을 알려 줍니다. 아래 각 페이지는 Built-in Render Pipeline의 구성 요소 하나를 설명합니다: URP에서 무엇이 깨지는지,
Urpify가 어떻게 처리하는지, 결과가 얼마나 가까운지.

이미지는 Urpify의 테스트 랩에서 가져왔습니다. 랩은 구성 요소마다 전용 부스가 있는 씬입니다. 각 이미지는 같은 부스를 세 번
보여 줍니다: Built-in, Unity의 Render Pipeline Converter만 거친 URP, Unity 컨버터와 Urpify를 거친 URP. Urpify 자체의 캡처
단계가 찍은 프레임을 부스에 맞춰 잘라 낸 것입니다.

| 구성 요소 | 페이지 | 부스 | 고치는 쪽 |
|---|---|---|---|
| 내장 라이팅 모델의 서피스 셰이더 | [서피스 셰이더](surface-shaders.md) | S01–S04 | Pro |
| 커스텀 라이팅 함수 | [커스텀 라이팅](surface-custom-lighting.md) | S05 | Pro |
| `#pragma surface` 옵션: `vertex:`, `finalcolor:`, `decal:`, `alphatest:`, `addshadow` 등 | [서피스 옵션](surface-options.md) | S03, S06–S08, S10, S11 | Pro |
| 서피스 셰이더의 테셀레이션 | [테셀레이션](surface-tessellation.md) | S09 | Pro |
| 추가 패스: 외곽선, 털 셸, 3개 이상의 패스 | [추가 패스](extra-passes.md) | S12 | Pro |
| 라이팅 없는 vert/frag 셰이더 | [라이팅 없는 vert/frag](vertfrag-unlit.md) | V01 | 할 일 없음 |
| 라이팅 있는 vert/frag 셰이더: ForwardBase, ForwardAdd, 그림자, 정점 라이팅 | [라이팅 있는 vert/frag](vertfrag-lit.md) | V02 | Pro |
| GrabPass | [GrabPass](grabpass.md) | G01, G02 | Pro |
| `_CameraDepthTexture` | [깊이 텍스처](depth-texture.md) | D01 | Free와 Pro |
| `_CameraDepthNormalsTexture`, 대체 셰이더 | [depth-normals와 대체 셰이더](depth-normals.md) | 없음 | Pro |
| `OnRenderImage` 이미지 효과 | [이미지 효과](image-effects.md) | C02 | Pro |
| 카메라 이벤트의 `CommandBuffer` | [커맨드 버퍼](command-buffers.md) | C03 | Pro |
| 카메라를 렌더링하는 스크립트: 거울, 포털, 물 반사 | [카메라 스크립트](camera-scripts.md) | M01 | Pro |
| `Projector` 컴포넌트 | [프로젝터](projectors.md) | C01 | Pro |
| 내장 셰이더 이름을 쓰는 `Shader.Find` | [코드의 Shader.Find](shader-find.md) | C04 | Free |
| Unity 컨버터가 망가뜨리는 것: Standard Transparent, Self-Illumin, 내장 머티리얼, 패키지 머티리얼, Post-processing v2, URP Asset 텍스처, Material Variant | [Unity 컨버터 이후의 수정](after-unity-converter.md) | STD04, LEG03 | Free |
| Unity 컨버터가 처리하는 것: Standard, Legacy, Mobile, 파티클, 지형, 가져온 모델 | [Unity 컨버터](unity-converter.md) | STD01–STD06, LEG01–LEG05, PAR01, PAR02, FBX01, FBX02, TER01 | Unity |
| Tree Creator와 소프트 오클루전 나무 | [나무](trees.md) | 없음 | Pro |
| Built-in과 URP 모두에서 쓰는 셰이더 파일 하나 | [Built-in 지원 유지](keep-builtin-support.md) | 모든 셰이더 부스 | Pro |

Urpify가 변환하지 않는 구성 요소는 이유와 함께 [변환되지 않는 것](../not-converted.md)에 있습니다.
