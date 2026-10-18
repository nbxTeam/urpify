# Mantener compatibilidad con Built-in: un shader para ambas pipelines

Salida = **Mantener compatibilidad con Built-in** (Project Settings > Urpify > Conversión, Pro) escribe cada shader
convertido como un único archivo que funciona en Built-in y en URP. Está pensado para autores de la Asset Store que venden
a compradores de cualquiera de las dos pipelines, y para proyectos que cambian de pipeline según la plataforma.

## Cómo está hecho el archivo

```
Shader "Author/Water" {
    Properties { ... }                      // sin cambios
    SubShader {                             // añadido por Urpify, el primero
        Tags { "RenderPipeline" = "UniversalPipeline" }
        PackageRequirements { "com.unity.render-pipelines.universal" }
        ...                                 // las pasadas convertidas
    }
    SubShader { ... }                       // el SubShader original de Built-in, byte a byte
    Fallback "..."
}
```

Unity elige el primer SubShader que corresponde a la pipeline activa. Con URP activo toma el SubShader de URP; en Built-in
se lo salta por la etiqueta y toma el original. `PackageRequirements` evita que un proyecto de Built-in sin URP compile
siquiera la parte de URP. Comprobado en Unity 6000.5 y 2022.3.

Los SubShaders originales no se tocan, así que un material se renderiza en Built-in exactamente como antes. Esto está
comprobado byte a byte: cada shader del laboratorio convertido con Mantener compatibilidad con Built-in renderiza en
Built-in la misma imagen que el original.

## Qué más cambia con esta salida

- Los materiales se quedan en sus shaders, así que los mismos materiales funcionan en ambas pipelines.
- `Shader.Find` con un nombre de Built-in en un script elige el nombre al ejecutarse, según la pipeline activa.
- Los scripts y Renderer Features convertidos solo funcionan en URP y no hacen nada en Built-in.

## Coste

Un shader con dos SubShaders compila ambos, así que una build que lo incluye tiene más variantes. El tamaño y el tiempo de
build con shaders para ambas pipelines se midieron en el laboratorio: consulta las [preguntas frecuentes](../faq.md).
