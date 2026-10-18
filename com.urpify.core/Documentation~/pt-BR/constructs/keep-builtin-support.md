# Manter suporte ao Built-in: um shader para as duas pipelines

Saída = **Manter suporte ao Built-in** (Project Settings > Urpify > Conversão, Pro) grava cada shader convertido como um
único arquivo que funciona no Built-in e no URP. É pensado para autores da Asset Store que vendem para compradores de
qualquer uma das pipelines, e para projetos que trocam de pipeline conforme a plataforma.

## Como o arquivo é montado

```
Shader "Author/Water" {
    Properties { ... }                      // sem mudança
    SubShader {                             // adicionado pelo Urpify, primeiro
        Tags { "RenderPipeline" = "UniversalPipeline" }
        PackageRequirements { "com.unity.render-pipelines.universal" }
        ...                                 // as passadas convertidas
    }
    SubShader { ... }                       // o SubShader original do Built-in, byte a byte
    Fallback "..."
}
```

A Unity escolhe o primeiro SubShader que corresponde à pipeline ativa. Com o URP ativo, pega o SubShader de URP; no
Built-in, pula esse por causa da tag e pega o original. `PackageRequirements` impede que um projeto de Built-in sem URP
sequer compile a parte de URP. Conferido na Unity 6000.5 e 2022.3.

Os SubShaders originais não são tocados, então um material renderiza no Built-in exatamente como antes. Isso é conferido
byte a byte: cada shader do laboratório convertido com Manter suporte ao Built-in renderiza no Built-in a mesma imagem que
o original.

## O que mais muda com esta saída

- Os materiais continuam nos seus shaders, então os mesmos materiais funcionam nas duas pipelines.
- `Shader.Find` com um nome do Built-in num script escolhe o nome em tempo de execução, pela pipeline ativa.
- Scripts e Renderer Features convertidos só funcionam no URP e não fazem nada no Built-in.

## Custo

Um shader com dois SubShaders compila os dois, então um build que o inclui tem mais variantes. O tamanho e o tempo de build
com shaders para as duas pipelines foram medidos no laboratório: veja as [perguntas frequentes](../faq.md).
