# Shader.Find with Built-in shader names

![Lab booth C04: a material created in code with Shader.Find("Standard"), pink after Unity's converter, fixed by Urpify](../images/C04.jpg)

*Lab booth C04: a script creates its material at run time with `new Material(Shader.Find("Standard"))` and sets
`_Color`.*

## What breaks

Unity's converter upgrades material assets, not code. A script that creates a material with `Shader.Find("Standard")`,
`"Diffuse"`, `"Legacy Shaders/..."` or another Built-in name gets a shader URP cannot draw, so the object is pink. The
property names the script sets (`_MainTex`, `_Color`, `_Glossiness`) are hidden properties in URP Lit, so even after the
shader is fixed by hand those calls change nothing.

Findings: `Shader.Find("...")` (`SCRIPT.SHADER_FIND_BUILTIN`), "Built-in property names in code"
(`SCRIPT.BUILTIN_PROPERTY_NAMES`).

## What Urpify does (Free)

This is one of the fixes after Unity's converter, in the free package. In the same script:

- the shader name becomes the URP shader Unity's converter picks for that Built-in shader (`Standard` to
  `Universal Render Pipeline/Lit`), or the nearest URP shader from Urpify's table for shaders Unity has no upgrader for;
- the property names the same method sets become URP's (`_MainTex` to `_BaseMap`, `_Color` to `_BaseColor`).

With Output = Keep Built-in support, each name is chosen when the script runs, by the active render pipeline, so the
script keeps working in Built-in too.

The rewritten script is compiled before it is kept; if it does not compile, the original is put back and reported.

## How close

`Auto`.
