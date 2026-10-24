# 빠른 시작

스캔, 마이그레이션, 비교의 전체 과정을 5분 안에 진행합니다. 먼저 데모 씬에서 해 본 다음, 내 프로젝트의 복사본에서 해
보세요.

## 1. 설치

아직 Built-in Render Pipeline을 쓰는 프로젝트에 설치합니다.

- **Urpify Pro:** 파일은 `Urpify-Pro-1.0.1.unitypackage` 하나입니다(Asset Store에서: Package Manager > My Assets > Import). Assets > Import Package > Custom Package에서 전부 가져옵니다. Urpify, Urpify Pro, 데모, 이
  문서가 모두 들어 있습니다. 무료 Urpify를 `.unitypackage`로 이미 설치했다면 그 위에 Pro를 가져오면 되고, 지울 것은
  없습니다. Package Manager로 설치했다면 먼저 거기서 제거하세요(Package Manager > Urpify > Remove). Urpify Pro에 이미
  들어 있기 때문입니다.
- **Urpify(무료):** [GitHub Releases](https://github.com/nbxTeam/urpify/releases/latest)의
  `Urpify-1.0.1.unitypackage`를 같은 방법으로 가져오거나, Package Manager > `+` > Add package from git URL에
  `https://github.com/nbxTeam/urpify.git?path=com.urpify.core#v1.0.1`을 입력합니다.

마이그레이션하기 전에 프로젝트를 버전 관리에 커밋하세요. Urpify는 바꾸는 모든 파일을 백업하지만, 가장 안전한 복원 지점은
커밋입니다.

**Asset Store 버전**은 패키지가 다른 패키지를 추가하거나 제거하지 않는다는 스토어 규칙을 따르므로 두 가지가 다르게
동작합니다. **Install URP**는 Package Manager에서 Universal RP를 엽니다. **Install**을 클릭하면 URP가 로드된 뒤 단계가
자동으로 계속됩니다. 롤백은 나머지를 모두 복원하지만 `Packages/manifest.json`과 `packages-lock.json`은 그대로 둡니다.
URP는 설치된 채 사용되지 않으며, 롤백 메시지에 제거용 Package Manager를 여는 버튼이 있습니다. URP가 설치되어 있는 동안 Unity는 프로젝트를 열 때 URP 자체 설정 파일(URP 전역 설정, Shader Graph 설정, Graphics 설정의 한 줄)을 추가합니다. 프로젝트는 계속 Built-in으로 렌더링됩니다. Urpify 설정에서 언어를
고르기 전까지 창은 영어입니다. 일부 프로젝트(Post-processing v2 사용, Unity 6000.3)에서는 Unity 컨버터가 자체 창에서
실행됩니다. 거기서 변환한 다음 단계에서 **계속**을 선택하세요.

## 2. 데모 열기(선택)

Help > Urpify > **Import Demo**를 실행하거나, 비어 있는 Urpify 창에서 **데모 가져오기**를 누릅니다. 데모는 `Assets/Samples/Urpify/1.0.1/Demo`에
들어가고 씬이 열립니다. Package Manager로 설치했다면 Package Manager > Urpify > Samples > **Demo** > Import도 같습니다. 씬에는 URP에서
깨지는 구성 요소마다 부스가 하나씩 있습니다: 서피스 셰이더, 라이팅이 있는 vert/frag 셰이더, GrabPass 유리, 깊이를 읽는 물,
프로젝터, 스크립트가 렌더링하는 거울, 코드로 만드는 머티리얼, Standard Transparent 모드의 유리. 각 부스에 Migration
Viewpoint가 있어서 비교할 때 하나하나를 가까이서 볼 수 있습니다.

## 3. 스캔

Window > Rendering > **Urpify**를 열고 **프로젝트 스캔**을 선택합니다.

스캔은 파일을 읽기만 합니다. 왼쪽 목록은 URP에서 깨지는 셰이더, 스크립트, 머티리얼, 씬, 설정을 종류별로 묶어 보여 줍니다.
각 행에는 아이콘이 있습니다.

- 채운 사각형: 분홍색으로 렌더링
- 채운 삼각형: 오류 없이 깨짐(포털이 벽이 되고, 물의 거품이 사라지고, 효과가 사라짐)
- 채운 마름모: 다르게 보임
- 고리: 눈에 띄는 변화 없음

행을 선택하면 무엇이 깨지는지, 왜 그런지, 누가 고치는지가 표시됩니다. 셰이더는 셰이더 패널에서 열리며, 변환 전후의 코드와
Built-in 및 URP 미리 보기를 나란히 보여 줍니다.

## 4. 마이그레이션

**마이그레이션...**을 선택합니다. 창에 단계가 나열됩니다.

1. **스캔**: 같은 읽기 전용 스캔.
2. **백업**: 프로젝트의 에셋, 설정, 패키지 목록을 `Urpify/Backup`에 복사합니다.
3. **이전 캡처**: 각 씬을 Play Mode에서 재생하고, 메인 카메라와 모든 Migration Viewpoint의 프레임을 저장합니다.
4. **URP 설치**: 에디터와 함께 제공되는 URP 버전을 추가합니다.
5. **Unity 컨버터 실행**: Urpify가 안정적으로 실행하는 데 필요한 우회 처리와 함께 Unity의 Render Pipeline Converter를
   실행합니다.
6. **Urpify 컨버터**: Unity 컨버터가 깨진 채로 남기는 것을 고칩니다. Pro에서는 셰이더, 스크립트, 컴포넌트도 변환합니다.
7. **이후 캡처 및 비교**: URP에서 같은 프레임을 찍고 이전 프레임과 비교합니다.
8. **검토**: 최종 보고서.

**모두 실행**을 선택합니다. 각 단계는 따로 실행할 수도 있고, 각각 롤백할 수 있습니다: 단계의 **여기까지 롤백**, 또는
**모두 롤백**.

## 5. 비교

**씬 비교**를 선택합니다. 각 씬은 전후 프레임을 사이의 슬라이더와 함께 보여 줍니다. **차이 표시**는 바뀐 픽셀에 표시를
합니다: 빨갈수록 변화가 큽니다. **변경된 것만**은 똑같아 보이는 씬을 숨깁니다.

메인 창에서 씬에는 판정이 두 가지 있습니다: 스캔에서 나온 **정적**, 프레임에서 나온 **관찰**. 분홍색 오브젝트가 어떤 캡처
프레임에도 잡히지 않을 수 있으므로, 정적 판정이 관찰보다 나쁠 수 있습니다. 더 가까이 보고 싶은 곳에 Migration Viewpoint
(`MigrationViewpoint` 컴포넌트가 있는 빈 GameObject)를 추가하고 다시 캡처하세요.

## 6. 완료 또는 롤백

**보고서** > HTML 내보내기로 팀에 보낼 수 있는 보고서를 만듭니다. 결과가 마음에 들지 않으면 **모두 롤백**이 마이그레이션이
바꾼 모든 파일을 되돌리고, 마이그레이션이 만든 파일을 백업으로 옮깁니다. 단계 사이에 생긴 파일은 절대 건드리지 않으므로
내 작업은 그대로 남습니다.
창에서 롤백하면 Unity가 바로 다시 시작되어 디스크에 있는 그대로 프로젝트를 불러옵니다.

다음: [게임 마이그레이션](scenario-game.md), [셰이더 변환](scenario-shader.md),
[Asset Store 패키지 변환](scenario-asset.md).
