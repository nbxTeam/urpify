# Shader.Find com nomes de shaders embutidos

![Estande C04: um material criado em código com Shader.Find("Standard"), rosa depois do conversor da Unity, corrigido pelo Urpify](../../images/C04.jpg)

*Estande C04: um script cria em tempo de execução um material com `new Material(Shader.Find("Standard"))` e define
`_Color`.*

## O que quebra

O conversor da Unity atualiza arquivos de materiais, não código. Um script que cria um material com
`Shader.Find("Standard")`, `"Diffuse"`, `"Legacy Shaders/..."` ou outro nome de shader embutido recebe um shader que o URP
não consegue desenhar, e o objeto fica rosa. Os nomes de propriedade que o script define (`_MainTex`, `_Color`,
`_Glossiness`) são propriedades ocultas no URP Lit, então, mesmo trocando o shader à mão, essas chamadas não mudam nada.

Achados: `Shader.Find("...")` (`SCRIPT.SHADER_FIND_BUILTIN`), «Nomes de propriedades do Built-in no código»
(`SCRIPT.BUILTIN_PROPERTY_NAMES`).

## O que o Urpify faz (Free)

É uma das correções depois do conversor da Unity, no pacote gratuito. No mesmo script:

- o nome do shader vira o shader do URP que o conversor da Unity escolhe para esse shader embutido (`Standard` para
  `Universal Render Pipeline/Lit`), ou o shader do URP mais próximo da tabela do Urpify para shaders sem regra da Unity;
- os nomes de propriedade que o mesmo método define viram os nomes do URP (`_MainTex` para `_BaseMap`, `_Color` para
  `_BaseColor`).

Com a saída Manter suporte ao Built-in, cada nome é escolhido em tempo de execução pela pipeline ativa, então o script
continua funcionando também no Built-in.

O script reescrito é compilado antes de ser salvo; se não compilar, o original é restaurado e informado.

## Quão perto

`Auto`.
