# Текстура глубины-нормалей и replacement-шейдеры

На стенде для этих конструкций нет кабинки; они проверены на ToonWaterShader (IronWarrior, Unlicense), пена воды в котором
зависит от обеих.

## Что ломается

- `_CameraDepthNormalsTexture` в URP нет. Шейдер, который её читает, ничего не получает: обводки по нормалям и пена вдоль
  берега пропадают.
- `Camera.SetReplacementShader` и `Camera.RenderWithShader` в URP не поддерживаются. Скрипт, который рендерит сцену
  replacement-шейдером в текстуру (частый способ получить нормали или глубину для эффекта), молча не рендерит ничего
  полезного. В ToonWaterShader из-за этого пропала пена.

Находки: «Читает _CameraDepthNormalsTexture» (`SHADER.DEPTHNORMALS_TEXTURE`), вызов replacement-шейдера
(`SCRIPT.REPLACEMENT_SHADER`).

## Что делает Urpify (Pro)

- Renderer Feature Built-in Camera Hooks от Urpify записывает `_CameraDepthNormalsTexture` из текстур глубины и нормалей
  URP в кодировке Built-in (`EncodeDepthNormal`), поэтому шейдер читает её как раньше. Feature запрашивает у URP
  текстуру нормалей, только когда она нужна шейдеру.
- Камера с replacement-шейдером превращается в проход того же Renderer Feature, который рисует непрозрачные объекты камеры
  replacement-шейдером в ту же целевую текстуру, с тем же сопоставлением тегов (`RenderType`).

## Насколько близко

`Авто, приблизительно`. Нормали URP берутся из его собственного прохода глубины-нормалей, поэтому края могут немного
отличаться от Built-in.
