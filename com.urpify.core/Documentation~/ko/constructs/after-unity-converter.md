# Unity 컨버터 이후의 수정

Unity의 Render Pipeline Converter는 마이그레이션에서 가장 먼저 실행되며, 대부분 맞게 동작합니다. 여기서는 그 결과가 측정상
틀린 경우와, 그 뒤에 무료 패키지가 하는 일을 설명합니다. 모두 무료 패키지의 "Urpify 컨버터" 단계에 들어 있습니다.

## Transparent 모드의 Standard

![부스 STD04: Standard Transparent 모드의 유리. Unity 컨버터 후에는 하얗고, Urpify 후에는 다시 유리](../../images/STD04.jpg)

*STD04: Standard의 Transparent 모드 유리.*

Unity 컨버터는 Transparent를 URP의 Premultiply 블렌딩으로 대응시키는데, 이는 알파를 이미 곱한 색을 전제로 합니다. 유리가
거의 불투명하고 하얗게 됩니다. Urpify는 머티리얼을 Preserve Specular Lighting이 켜진 Alpha 블렌딩으로 바꿉니다. 이것이
Built-in의 블렌딩 방식입니다. 발견 사항: "..., Transparent 모드"(`MATERIAL.STANDARD_TRANSPARENT`).

## Legacy Self-Illumin

![부스 LEG03: Legacy Self-Illumin. Unity 컨버터 후에는 어둡고, Urpify 후에는 빛남](../../images/LEG03.jpg)

*LEG03: `Legacy Shaders/Self-Illumin/Diffuse`.*

Unity 컨버터는 이 머티리얼을 Simple Lit로 옮기는데, Simple Lit는 Illumin 알파에서 이미시브를 가져오지 않아 빛이 사라집니다.
Urpify는 Built-in처럼 빛나는 자체 URP용 Self-Illumin 셰이더로 옮깁니다. 발견 사항: `MATERIAL.SELF_ILLUMIN`.

## 에디터의 내장 머티리얼

에디터의 내장 `Default-Material`이나 `Default-Diffuse`를 쓰는 렌더러(새 큐브, 평면)를 Unity 컨버터는 URP의 기본 머티리얼로
연결하는데, 이는 흰색이 아니라 중간 회색입니다. Urpify는 내장 머티리얼의 값을 가진 URP 사본으로 연결합니다. 발견 사항:
`SCENE.BUILTIN_MATERIAL`.

## 패키지 안의 머티리얼

패키지 파일은 읽기 전용이라 Unity 컨버터는 패키지 안의 머티리얼을 절대 바꾸지 않습니다. 그 셰이더가 Built-in 전용이면,
그것을 쓰는 모든 렌더러가 분홍색이 됩니다: ProBuilder 5의 벽은 `ProBuilderDefault.mat`을 쓰는데, 그 셰이더
`ProBuilder/Standard Vertex Color`는 서피스 셰이더입니다. Urpify는 씬과 프리팹의 그런 렌더러를
`Assets/Urpify/Materials/Packages/`의 URP 사본으로 연결합니다: 내장 셰이더는 Unity 자체 업그레이더로(`자동`), 그 밖에는
머티리얼의 색, 텍스처, 스무스니스, 메탈릭을 가진 URP Lit로(`자동, 근사`: 정점 색처럼 셰이더 자체가 더하는 것은 유지되지
않습니다). 발견 사항: `SCENE.PACKAGE_MATERIAL`.

## 업그레이더가 없는 내장 셰이더

일부 내장 셰이더는 Unity 컨버터에 업그레이더가 없습니다("No upgrader found"): `Legacy Shaders/Decal`,
`Legacy Shaders/Transparent/VertexLit`, `Transparent/Parallax Diffuse`와 `Specular`, 그리고 URP 14에서는 예전 이름으로 등록된
업그레이더 두 개. Urpify는 이 머티리얼을 자체 표에서 가장 가까운 URP 셰이더로, 프로퍼티를 옮겨서 바꿉니다. 발견 사항:
`MATERIAL.BUILTIN_NO_UPGRADER`.

## Post-processing Stack v2

Unity 컨버터는 PPv2 볼륨과 프로필을 URP Volume으로 바꾸지만, 배치 모드에서는 필요한 에셋 검색을 기다리지 않아 하나도 바꾸지
못합니다: 블룸이 오류 없이 사라집니다. Urpify의 "Unity 컨버터 실행" 단계는 검색을 기다린 다음, 광채가 Built-in에 더 가까워지도록
블룸 파라미터를 보정(threshold와 scatter)해서 옮깁니다. 나머지 차이는 URP 블룸 자체입니다.
[URP에서 달라 보이는 점](../urp-differences.md)을 참고하세요. 발견 사항: `SCENE.PPV2`.

## URP Asset 설정

- **Depth Texture와 Opaque Texture**: URP가 그대로 그리는 셰이더가 이를 읽으면 모든 URP Asset에서 켭니다
  ([깊이 텍스처](depth-texture.md) 참고).
- **URP Asset 텍스처**(URP 14): Unity 컨버터는 URP Asset을 blue noise와 Bayer 텍스처가 빈 채로 저장합니다. 에디터는 이를
  채우지만 빌드한 플레이어는 채우지 않아, 매 프레임 `NullReferenceException`을 내고 검게 렌더링합니다. Urpify는 URP
  패키지에서 이를 채웁니다. 발견 사항: `SETTINGS.URP_ASSET_TEXTURES`.
- **메인 라이트**: Pixel Light Count가 0인 품질 수준에서도 Built-in은 메인 디렉셔널 라이트를 픽셀 단위로 비췄습니다.
  Urpify는 그 수준에서 URP의 Main Light를 Per Pixel로 설정합니다.

## Material Variant

Unity 컨버터는 Material Variant를 부모의 URP 셰이더로 옮기지만, 변형 자체의 값을 버립니다: 부모의 조용한 사본이 됩니다.
Urpify는 "Unity 컨버터 실행" 단계의 백업에서 변형 자체의 값을 읽어, URP 프로퍼티의 오버라이드로 되돌립니다. 발견 사항:
`MATERIAL.VARIANT`.

## 모델 안의 읽기 전용 머티리얼

FBX 파일에 들어 있는 머티리얼은 읽기 전용이며, 이를 처리하는 Unity 컨버터의 모듈은 배치 모드에서 안정적으로 동작하지
않았습니다. Urpify는 찾은 렌더러와 지형에 대해 이 모듈을 오브젝트별로 실행하며, 두 Unity 버전 모두에서 그렇게 합니다.
