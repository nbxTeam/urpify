# 서피스 셰이더의 옵션

`#pragma surface surf Standard <옵션>`은 Unity가 생성하는 것을 바꿉니다. Urpify는 각 옵션을 읽고, 쓰는 패스에서 그 의미를
유지합니다(Pro).

![부스 S03: vertex: 바람 함수가 있는 Lambert](../../images/S03.jpg)

*S03: `vertex:vert`가 바람처럼 메시를 휩니다. 정점 함수는 모든 패스에서 실행되므로 그림자도 휩니다.*

![부스 S08: clip()과 addshadow를 쓰는 디졸브](../../images/S08.jpg)

*S08: `clip()`과 `addshadow`를 쓰는 디졸브: 그림자도 같은 방식으로 잘립니다.*

![부스 S10: decal:add](../../images/S10.jpg)

*S10: `decal:add`, 불투명 오브젝트 위에 얹는 가산 레이어.*

![부스 S11: finalcolor:의 높이 안개](../../images/S11.jpg)

*S11: `finalcolor:`의 높이 안개. Built-in처럼 라이팅 다음에 적용됩니다.*

## 옵션과 그 결과

| 옵션 | URP에서 |
|---|---|
| `vertex:func` | 생성하는 모든 패스(포워드, 그림자, 깊이, depth-normals, meta)에서 실행됩니다. 함수가 채우는 추가 `Input` 필드는 서피스 함수로 전달됩니다. |
| `finalcolor:func` | 라이팅된 색으로, 안개 전에 호출됩니다. |
| `finalgbuffer:func` | 쓰지 않음: Built-in의 디퍼드 G-버퍼 패스에서만 실행됩니다. 메모가 붙습니다. |
| `alpha`, `alpha:blend`, `alpha:fade`, `alpha:premul` | 투명 큐와 해당 블렌딩. `premul`은 미리 곱한 스페큘러를 유지합니다. |
| `alphatest:prop` | 모든 패스에서 프로퍼티에 대한 `clip()`. |
| `keepalpha` | 서피스 함수의 알파를 불투명 패스에서도 씁니다. |
| `decal:add`, `decal:blend` | 불투명 오브젝트 다음에 가산 또는 알파 블렌딩으로, 그림자 없이 그립니다. |
| `addshadow` | 그림자 패스가 포워드 패스처럼 정점 함수와 서피스 클리핑을 실행합니다. |
| `noshadow` | 그림자 패스 없음. 포워드 패스는 그림자를 받지 않습니다. |
| `noambient`, `novertexlights`, `nolightmap`, `nofog`, `nometa`, `noforwardadd` | 해당 부분(주변광, 정점 라이트, 라이트맵, 안개, Meta 패스, 추가 라이트)을 뺍니다. |
| `halfasview`, `interpolateview`, `approxview`, `dualforward` | URP에 필요 없는 시선 방향 품질 스위치. 메모와 함께 무시합니다. |
| `fullforwardshadows`, `nodynlightmap`, `nodirlightmap`, `nolppv`, `noshadowmask`, `noinstancing`, `exclude_path:*`, `dithercrossfade` | 인식합니다. 이는 URP 자체 설정(추가 라이트 그림자, 라이트맵 모드, 라이트 프로브 프록시 볼륨, LOD 크로스페이드)이 정하므로 변환된 셰이더에서는 아무것도 바꾸지 않습니다. |
| `tessellate:func`, `tessphong:` | [테셀레이션](surface-tessellation.md)을 참고하세요. |

Urpify가 모르는 옵션은 발견 사항 "지원하지 않는 서피스 옵션"(`SHADER.SURFACE.UNKNOWN_OPTION`)에 나열되며, 셰이더의 나머지는
그래도 변환됩니다.

설정의 "그림자"(Project Settings > Urpify)로 변환된 모든 서피스 셰이더의 그림자를 강제로 켜거나 끌 수 있습니다.
