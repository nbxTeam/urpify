# Keep Built-in support: one shader for both pipelines

Output = **Keep Built-in support** (Project Settings > Urpify > Conversion, Pro) writes each converted shader as one file
that works in Built-in and in URP. It is meant for Asset Store authors who ship to buyers on either pipeline, and for
projects that switch pipelines per platform.

## How the file is built

```
Shader "Author/Water" {
    Properties { ... }                      // unchanged
    SubShader {                             // added by Urpify, first
        Tags { "RenderPipeline" = "UniversalPipeline" }
        PackageRequirements { "com.unity.render-pipelines.universal" }
        ...                                 // the converted passes
    }
    SubShader { ... }                       // the original Built-in SubShader, byte for byte
    Fallback "..."
}
```

Unity picks the first SubShader that matches the active pipeline. With URP active it takes the URP SubShader; in
Built-in it skips it, because of the tag, and takes the original. `PackageRequirements` keeps a Built-in project without
URP from compiling the URP part at all. Checked on Unity 6000.5 and 2022.3.

The original SubShaders are not touched, so a material renders in Built-in exactly as before. This is checked byte for
byte: every lab shader converted with Keep Built-in support renders the same image in Built-in as the original.

## What else changes with this output

- Materials stay on their shaders, so the same materials work in both pipelines.
- `Shader.Find` with a Built-in name in a script chooses the name when the script runs, by the active pipeline.
- Converted scripts and renderer features work only under URP and do nothing under Built-in.

## Cost

A shader with two SubShaders compiles both, so a build that includes it has more variants. Build size and time with
dual shaders were measured on the lab: see [FAQ](../faq.md#does-keep-built-in-support-make-builds-bigger).
