# Unity 컨버터가 처리하는 것

Unity의 Render Pipeline Converter는 Unity 자체 내장 셰이더를 쓰는 머티리얼을 업그레이드합니다. Urpify는 이를 마이그레이션의
"Unity 컨버터 실행" 단계로, 스크립트에서 안정적으로 실행하는 데 필요한 우회 처리와 함께 실행하고, 그 로그를 보고합니다.

![부스 STD01: 노멀 맵과 메탈릭 맵을 쓰는 Standard 메탈릭](../../images/STD01.jpg)

*STD01: Standard, 메탈릭 워크플로, 노멀 맵과 메탈릭 맵. Unity 컨버터가 URP Lit로 업그레이드합니다.*

![부스 TER01: 레이어 두 개의 지형](../../images/TER01.jpg)

*TER01: 레이어 두 개의 지형이 URP의 지형 셰이더로 옮겨집니다.*

부스 STD01~STD06, LEG01~LEG05, PAR01, PAR02, FBX01, FBX02, TER01에서 측정한, 컨버터가 다루는 범위:

- Opaque, Cutout, Fade 모드의 Standard와 Standard (Specular setup). 노멀, 이미시브, 디테일 맵 포함. Transparent 모드는
  예외입니다. [Unity 컨버터 이후의 수정](after-unity-converter.md)을 참고하세요.
- Legacy Shaders(Diffuse, Specular, Bumped, Reflective, Transparent, Cutout, Lightmapped, Parallax, VertexLit).
  Self-Illumin은 예외입니다.
- Mobile 셰이더, Unlit/Texture, Particles/Standard Surface와 Unlit, 레거시 파티클.
- Nature/Terrain/Standard와 기본 지형 머티리얼, SpeedTree.
- 가져온 모델에 들어 있는 머티리얼.
- Built-in 머티리얼 프로퍼티를 애니메이션하는 애니메이션 클립(스캔이 목록으로 보여 줍니다. Urpify는 아직 결과를 확인하지
  않습니다).
- Post-processing Stack v2. [Unity 컨버터 이후의 수정](after-unity-converter.md)을 참고하세요.

Urpify의 스캔은 이 머티리얼들을 Unity 컨버터가 고치는 것으로 나열하므로, 개요는 Urpify가 필요한 것과 따로 셉니다.

## 안정적으로 실행하기

Unity 6.5에서 Unity 문서의 배치 명령은 `-typesFilter`가 없으면 `NullReferenceException`을 내고, 작업이 성공한 뒤 종료할 때
에디터가 자주 충돌합니다(종료 코드 21, 139). Urpify는 API로 컨버터를 호출하고, 종료 코드가 아니라 컨버터 로그와 재스캔으로
결과를 판단하며, 로그를 보고서에 포함합니다.
