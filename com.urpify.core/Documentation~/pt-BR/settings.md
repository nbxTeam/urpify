# Configurações

Project Settings > **Urpify**, ou o botão **Configurações** na janela do Urpify.

As configurações da equipe ficam em `ProjectSettings/UrpifySettings.asset`: faça commit do arquivo e toda a equipe
converte do mesmo jeito. As configurações pessoais ficam em `UserSettings/Urpify.json` e permanecem na sua máquina.

## Conversão (equipe)

Estas configurações decidem o que os conversores do Pro gravam.

| Configuração | Valores | Padrão | O que faz |
|---|---|---|---|
| Saída | Substituir o original, Gravar uma cópia, Manter suporte ao Built-in | Substituir o original | **Substituir o original**: o arquivo do shader passa a ser só de URP; o original fica no backup. **Gravar uma cópia**: o shader de URP é gravado ao lado do original como `Name_URP.shader` com o nome `Name (URP)`, e os materiais passam a usá-lo; o original fica. **Manter suporte ao Built-in**: um arquivo para as duas pipelines; os SubShaders originais ficam para o Built-in e um SubShader de URP é adicionado na frente. |
| Origem do GrabPass | Opaque Texture, Depois dos transparentes | Opaque Texture | **Opaque Texture**: um GrabPass convertido lê a cópia de tela do URP feita antes dos objetos transparentes, então eles não aparecem na refração. **Depois dos transparentes**: o Renderer Feature do Urpify copia a tela depois dos objetos transparentes, como o GrabPass fazia no Built-in, e desenha os objetos com GrabPass depois disso. Custa uma cópia de tela a mais por quadro. |
| Inserir efeitos de tela em | Depois do pós-processamento, Antes do pós-processamento, Depois dos opacos | Depois do pós-processamento | Onde, no quadro do URP, rodam os efeitos `OnRenderImage` convertidos. Efeitos com `[ImageEffectOpaque]` sempre rodam depois do céu, como no Built-in. |
| Sombras | Igual ao original, Sempre, Nunca | Igual ao original | Se os surface shaders convertidos projetam e recebem sombras. |
| SRP Batcher | Ligado, Desligado | Ligado | Coloca as propriedades do material num buffer `UnityPerMaterial` para que o SRP Batcher do URP agrupe os shaders convertidos. Um shader que declara o próprio `UnityPerMaterial` o mantém; uma propriedade que não pode ser movida com segurança fica de fora, com uma nota. |

## Comparação (equipe)

| Configuração | Padrão | O que faz |
|---|---|---|
| Tamanho da captura | Janela Game | O tamanho de cada quadro capturado: o da janela Game, para que efeitos que dependem da resolução fiquem como você os vê, ou um personalizado. A linha de comando usa `-urpifyWidth` e `-urpifyHeight`. |
| Largura da captura, Altura da captura | 960 × 540 | O tamanho com Tamanho da captura em Personalizado, e quando não há janela Game. |
| Quadros antes da captura | 30 | Quantos quadros rodam com passo de tempo fixo antes de cada captura, para que animações e partículas estejam no mesmo estado antes e depois. |
| Cenas | Build Settings | Quais cenas Capturar antes e Capturar depois usam: as do Build Settings, todas as cenas em `Assets` ou uma lista. |
| Caminhos de cenas | vazio | Para Cenas selecionadas: caminhos separados por ponto e vírgula, por exemplo `Assets/Scenes/Main.unity;Assets/Scenes/Menu.unity`. |

Os limites de «Rosa novo», «Mudou» e «Igual» são calibrados na conversão da própria Unity de Standard para URP Lit e
valem para qualquer projeto: uma área rosa nova acima de 0,02 % do quadro é «Rosa novo»; mais pixels alterados do que
Standard → URP Lit produz, ou uma área grande alterada, é «Mudou».

Cada cena pode ter **Migration Viewpoints**: adicione o componente `MigrationViewpoint` a um GameObject vazio para capturar
da posição e direção dele, além da câmera principal. Um viewpoint pode ter campo de visão e plano de corte distante
próprios. Ele é marcado como EditorOnly e nunca vai para um build.

## Pessoal

| Configuração | Valores | Padrão | O que faz |
|---|---|---|---|
| Idioma | Idioma do Editor, ou um dos 8 idiomas | Idioma do Editor | Idioma das janelas do Urpify. «Idioma do Editor» segue Preferences > Languages e usa inglês se esse idioma não estiver disponível. |
| Prévia do Built-in | Ao vivo, Das capturas | Ao vivo | **Ao vivo** desliga a render pipeline durante o render da prévia do Built-in e a liga de volta, numa chamada; nunca no Play Mode nem durante uma importação ou compilação. **Das capturas** mostra as capturas das cenas no lugar. Se a prévia ao vivo falhar ou as configurações da pipeline não voltarem exatamente, o Urpify muda sozinho para Das capturas e avisa. |
| Marcas na janela Project | Ligado, Desligado | Ligado | Uma pequena marca nos assets que a última análise encontrou quebrados. |

## Mudanças para uma única execução

A linha de comando pode trocar a Saída (`-urpifyShaderOutput`) e a Origem do GrabPass (`-urpifyGrabPass`) para uma
execução, veja [Linha de comando](cli.md).
