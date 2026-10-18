# GrabPass

![Estande G01: vidro com refração por GrabPass, some depois do conversor da Unity, volta a refratar depois do Urpify](../../images/G01.jpg)

*Estande G01: vidro com refração e um `GrabPass {}` sem nome. Só depois do conversor da Unity, o vidro some, sem nenhum
erro.*

![Estande G02: distorção de calor com um GrabPass com nome](../../images/G02.jpg)

*Estande G02: distorção de calor com um `GrabPass { "_HazeBackground" }` com nome.*

## O que quebra

O URP não tem GrabPass. A passada que captura a tela é pulada, a textura fica vazia e o objeto some ou perde a refração.
Nem rosa nem erro: uma quebra silenciosa.

Achado: «GrabPass (...)» (`SHADER.GRABPASS`), gravidade «Quebra sem erro».

## O que o Urpify faz (Pro)

O GrabPass é removido, e a textura que ele preenchia (`_GrabTexture` ou a com nome) passa a ler uma cópia da tela:

- **Origem do GrabPass = Opaque Texture** (padrão): `_CameraOpaqueTexture` do URP, feita depois dos objetos opacos e do
  céu, antes dos transparentes. O Urpify liga a Opaque Texture em cada URP Asset, sem reduzir a resolução.
- **Origem do GrabPass = Depois dos transparentes**: o Renderer Feature Built-in Camera Hooks do Urpify copia a tela depois
  dos objetos transparentes para `_UrpifyGrabTexture` e então desenha os objetos com GrabPass (tag `UrpifyGrabPass`), como
  o GrabPass fazia no Built-in. Uma cópia de tela a mais por quadro.

Um GrabPass que vem depois de uma ou duas passadas do mesmo shader (neblina e depois um desfoque que lê a captura) lê uma
cópia feita depois dessas passadas, então vê o que a captura do Built-in via.

## Quão perto

`Automático, aproximado` com Opaque Texture: objetos transparentes atrás do vidro não aparecem na refração. Com Depois dos
transparentes, o resultado bate com o Built-in. Vários GrabPass com texturas diferentes num shader ainda não têm suporte:
o URP tem uma só Opaque Texture.

No 2022.3, a autoverificação do Urpify renderiza shaders com GrabPass com uma câmera de jogo: o URP 14 não copia a Opaque
Texture para uma câmera de prévia.
