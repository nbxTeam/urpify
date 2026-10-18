# Tree Creator와 소프트 오클루전 나무

랩에는 나무 부스가 없습니다. ShaderTutorials(ronja-tutorials)와 Nature Starter Kit의 나무로 확인했습니다.

## 무엇이 깨지는가

URP에는 Tree Creator 셰이더(`Nature/Tree Creator Bark`, `Leaves`, `Leaves Fast`, `Optimized`)도, 소프트 오클루전 나무
셰이더도 없습니다. 이것들로 만든 나무는 분홍색으로 렌더링됩니다. 지형의 풀과 디테일 메시는 영향을 받지 않습니다: URP가 자체
디테일 셰이더로 그립니다.

발견 사항: "..., URP 버전 없음"(`MATERIAL.BUILTIN_TREE`).

## Urpify가 하는 일(Pro)

Unity는 내장 셰이더의 소스를 MIT 라이선스로 공개합니다. Urpify Pro는 Unity 자체의 나무 셰이더 8개를 일반 컨버터로 변환하고,
이들이 필요로 하는 `TerrainEngine.cginc`의 함수(바람, 휘어짐, 빌보드)를 라이브러리에 더하며, 프리팹 안의 것까지 포함해 나무
껍질과 잎 머티리얼을 변환된 셰이더로 옮깁니다. 라이선스 전문은 Pro의 `Third Party Notices.md`에 있습니다.

## 얼마나 가까운가

`자동, 근사`. 나무는 더 이상 분홍색이 아니며 머티리얼도 유지됩니다. URP에서의 바람 애니메이션과 빌보드는 몇 초 분량의
프레임으로 눈으로 확인했을 뿐, 픽셀 단위로 측정하지는 않았습니다. 내 씬은 씬 비교에서 비교하세요. SpeedTree 에셋은 Urpify가
아니라 Unity 컨버터가 업그레이드합니다.
