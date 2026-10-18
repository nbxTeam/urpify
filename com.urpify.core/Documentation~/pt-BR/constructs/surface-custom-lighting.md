# Funções de iluminação próprias

![Estande S05: uma função de iluminação com rampa toon, rosa depois do conversor da Unity, igual depois do Urpify](../../images/S05.jpg)

*Estande S05: um surface shader com função de iluminação própria, uma rampa toon.*

## O que quebra

Um surface shader pode nomear o próprio modelo de iluminação, `#pragma surface surf Toon`, e definir `LightingToon`. O
Built-in a chama uma vez por luz: em ForwardBase para a luz principal e em ForwardAdd para cada luz adicional, com
`_LightColor0` e a atenuação da luz. O URP não tem surface shaders, então o objeto fica rosa.

Achado: «Função de iluminação própria ...» (`SHADER.SURFACE.CUSTOM_LIGHTING`).

## O que o Urpify faz (Pro)

A função do autor é copiada sem mudanças. A passada forward gerada a chama para a luz principal do URP e depois para cada
luz adicional, com as sombras e a atenuação do URP, e soma os resultados, como as duas passadas do Built-in faziam. Dentro
da função, `_LightColor0` é a cor da luz para a qual ela é chamada.

Todas as assinaturas do Built-in são reconhecidas:

| Assinatura | Exemplo |
|---|---|
| Sem direção de visão | `half4 LightingToon(SurfaceOutput s, half3 lightDir, half atten)` |
| Com direção de visão | `half4 LightingToon(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)` |
| Com iluminação global | `half4 LightingToon(SurfaceOutput s, half3 viewDir, UnityGI gi)` e a sua `LightingToon_GI` |
| Variantes deferred e prepass | `_Deferred`, `_PrePass`: não usadas, o URP renderiza em forward |

A função é encontrada em qualquer lugar onde o compilador a encontraria: no programa, em blocos `CGINCLUDE`, em arquivos
include do projeto em qualquer profundidade e em macros `#define` que declaram funções.

## Quão perto

`Automático, aproximado`. O Built-in soma cada luz adicional numa passada própria com mistura aditiva; o URP as ilumina
numa passada. Para objetos opacos as somas batem; shaders transparentes podem diferir onde as luzes se sobrepõem. A luz
ambiente vem dos harmônicos esféricos do URP, como no Built-in.

Se a função não estiver no shader nem nos arquivos include analisados, o achado a nomeia, e o shader não é convertido até
que o arquivo include dela esteja no escopo da análise.
