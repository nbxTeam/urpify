# GrabPass

![부스 G01: GrabPass로 굴절하는 유리. Unity 컨버터 후에는 사라지고, Urpify 후에는 다시 굴절](../../images/G01.jpg)

*부스 G01: 이름 없는 `GrabPass {}`로 굴절하는 유리. Unity 컨버터만 거치면 유리가 오류 없이 사라집니다.*

![부스 G02: 이름 있는 GrabPass를 쓰는 열 아지랑이](../../images/G02.jpg)

*부스 G02: 이름 있는 `GrabPass { "_HazeBackground" }`를 쓰는 열 아지랑이.*

## 무엇이 깨지는가

URP에는 GrabPass가 없습니다. 화면을 잡는 패스는 건너뛰고, 텍스처는 빈 채로 남으며, 오브젝트는 사라지거나 굴절을 잃습니다.
분홍색도 오류도 없는 조용한 깨짐입니다.

발견 사항: "GrabPass(...)"(`SHADER.GRABPASS`), 심각도 "오류 없이 깨짐".

## Urpify가 하는 일(Pro)

GrabPass를 없애고, 그것이 채우던 텍스처(`_GrabTexture` 또는 이름 있는 텍스처)가 대신 화면 사본을 읽게 합니다.

- **GrabPass 소스 = Opaque Texture**(기본값): URP의 `_CameraOpaqueTexture`. 불투명 오브젝트와 하늘 다음, 투명 오브젝트 전에
  찍습니다. Urpify는 모든 URP Asset에서 Opaque Texture를 해상도 축소 없이 켭니다.
- **GrabPass 소스 = 투명 오브젝트 후**: Urpify의 Built-in Camera Hooks 렌더러 기능이 투명 오브젝트 다음에 화면을
  `_UrpifyGrabTexture`로 복사하고, 그 뒤 GrabPass가 있는 오브젝트(태그 `UrpifyGrabPass`)를 그립니다. Built-in의 GrabPass와
  같습니다. 프레임마다 화면 복사가 한 번 늘어납니다.

같은 셰이더의 패스 한두 개 뒤에 오는 GrabPass(안개 다음에, 잡은 화면을 읽는 블러)는 그 패스들 다음에 찍은 사본을 읽으므로,
Built-in의 잡기가 보던 것과 같은 것을 봅니다.

## 얼마나 가까운가

Opaque Texture에서는 `자동, 근사`: 유리 뒤의 투명 오브젝트는 굴절에 나오지 않습니다. "투명 오브젝트 후"에서는 결과가
Built-in과 일치합니다. 한 셰이더 안에 텍스처가 다른 GrabPass 여러 개는 아직 지원하지 않습니다: URP의 Opaque Texture는
하나뿐입니다.

2022.3에서는 Urpify의 자체 검사가 GrabPass 셰이더를 게임 카메라로 렌더링합니다: URP 14는 미리 보기 카메라에 Opaque Texture를
복사하지 않기 때문입니다.
