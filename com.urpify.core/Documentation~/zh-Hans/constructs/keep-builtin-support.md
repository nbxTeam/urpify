# 保留 Built-in 支持：两种管线通用的一个着色器

输出 = **保留 Built-in 支持**（Project Settings > Urpify > 转换，Pro）把每个转换后的着色器写成一个在 Built-in 和 URP 中都能
使用的文件。它面向向两种管线的买家销售的 Asset Store 作者，以及按平台切换管线的项目。

## 文件的组成

```
Shader "Author/Water" {
    Properties { ... }                      // 不变
    SubShader {                             // 由 Urpify 添加，放在最前
        Tags { "RenderPipeline" = "UniversalPipeline" }
        PackageRequirements { "com.unity.render-pipelines.universal" }
        ...                                 // 转换后的 Pass
    }
    SubShader { ... }                       // 原始的 Built-in SubShader，逐字节相同
    Fallback "..."
}
```

Unity 会选择与当前管线匹配的第一个 SubShader。URP 激活时使用 URP 的 SubShader；在 Built-in 中，因为标签而跳过它，使用原始
的 SubShader。`PackageRequirements` 让没有 URP 的 Built-in 项目根本不会编译 URP 部分。已在 Unity 6000.5 和 2022.3 上验证。

原始 SubShader 不会被改动，因此材质在 Built-in 中的渲染与以前完全相同。这一点经过逐字节验证：用“保留 Built-in 支持”转换的
每个实验室着色器，在 Built-in 中渲染出的图像都与原版相同。

## 此输出还会改变什么

- 材质仍使用各自的着色器，因此同一套材质可在两种管线中使用。
- 脚本中使用 Built-in 名称的 `Shader.Find` 会在运行时根据当前管线选择名称。
- 转换后的脚本和渲染器功能只在 URP 下工作，在 Built-in 下什么也不做。

## 代价

带两个 SubShader 的着色器会编译两者，因此包含它的构建会有更多变体。两种管线通用着色器的构建大小和时间已在实验室中测量：
参见 [常见问题](../faq.md)。
