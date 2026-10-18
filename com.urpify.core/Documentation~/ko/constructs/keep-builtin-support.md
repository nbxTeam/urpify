# Built-in 지원 유지: 두 파이프라인용 셰이더 하나

출력 = **Built-in 지원 유지**(Project Settings > Urpify > 변환, Pro)는 변환한 각 셰이더를 Built-in과 URP 모두에서 동작하는
파일 하나로 씁니다. 두 파이프라인 중 어느 쪽 구매자에게나 판매하는 Asset Store 작성자와, 플랫폼에 따라 파이프라인을 바꾸는
프로젝트를 위한 것입니다.

## 파일의 구성

```
Shader "Author/Water" {
    Properties { ... }                      // 변경 없음
    SubShader {                             // Urpify가 맨 앞에 추가
        Tags { "RenderPipeline" = "UniversalPipeline" }
        PackageRequirements { "com.unity.render-pipelines.universal" }
        ...                                 // 변환한 패스
    }
    SubShader { ... }                       // 원래의 Built-in SubShader, 바이트 단위로 동일
    Fallback "..."
}
```

Unity는 활성 파이프라인에 맞는 첫 번째 SubShader를 고릅니다. URP가 활성이면 URP SubShader를 쓰고, Built-in에서는 태그 때문에
그것을 건너뛰고 원본을 씁니다. `PackageRequirements` 덕분에 URP가 없는 Built-in 프로젝트는 URP 부분을 아예 컴파일하지
않습니다. Unity 6000.5와 2022.3에서 확인했습니다.

원래 SubShader는 건드리지 않으므로, Built-in에서 머티리얼은 예전과 똑같이 렌더링됩니다. 이는 바이트 단위로 확인했습니다:
"Built-in 지원 유지"로 변환한 랩의 모든 셰이더가 Built-in에서 원본과 같은 이미지를 렌더링합니다.

## 이 출력으로 바뀌는 다른 것

- 머티리얼은 원래 셰이더를 그대로 쓰므로, 같은 머티리얼이 두 파이프라인에서 동작합니다.
- 스크립트에서 Built-in 이름으로 쓰는 `Shader.Find`는 실행 중에 활성 파이프라인에 따라 이름을 고릅니다.
- 변환된 스크립트와 렌더러 기능은 URP에서만 동작하고, Built-in에서는 아무것도 하지 않습니다.

## 비용

SubShader가 두 개인 셰이더는 둘 다 컴파일하므로, 그것을 포함한 빌드는 배리언트가 늘어납니다. 두 파이프라인용 셰이더로 만든
빌드의 크기와 시간은 랩에서 측정했습니다: [자주 묻는 질문](../faq.md)을 참고하세요.
