# Textura depth-normals y shaders de reemplazo

El laboratorio no tiene puesto para estas construcciones; se comprobaron con ToonWaterShader (IronWarrior, Unlicense),
cuya espuma del agua depende de ambas.

## Qué se rompe

- `_CameraDepthNormalsTexture` no existe en URP. Un shader que la lee no recibe nada: los contornos basados en normales y
  la espuma a lo largo de una orilla desaparecen.
- `Camera.SetReplacementShader` y `Camera.RenderWithShader` no son compatibles con URP. Un script que renderiza la escena
  con un shader de reemplazo en una textura (una forma habitual de obtener normales o profundidad para un efecto) no
  renderiza nada útil, sin avisar. En ToonWaterShader así se perdía la espuma.

Hallazgos: «Lee _CameraDepthNormalsTexture» (`SHADER.DEPTHNORMALS_TEXTURE`), una llamada a shader de reemplazo
(`SCRIPT.REPLACEMENT_SHADER`).

## Qué hace Urpify (Pro)

- El Renderer Feature Built-in Camera Hooks de Urpify escribe `_CameraDepthNormalsTexture` a partir de las texturas de
  profundidad y normales de URP con la codificación de Built-in (`EncodeDepthNormal`), así que el shader la lee como
  antes. El feature solo pide a URP la textura de normales cuando un shader la necesita.
- Una cámara con shader de reemplazo pasa a ser una pasada del mismo Renderer Feature, que dibuja los objetos opacos de la
  cámara con el shader de reemplazo en el mismo destino, con la misma correspondencia de etiquetas (`RenderType`).

## Cuánto se acerca

`Automático, aproximado`. Las normales de URP vienen de su propia pasada de depth-normals, así que los bordes pueden
diferir un poco de Built-in.
