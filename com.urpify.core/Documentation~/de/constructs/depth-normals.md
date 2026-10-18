# Depth-Normals-Textur und Replacement-Shader

Das Labor hat für diese Konstrukte keinen Stand; sie wurden an ToonWaterShader (IronWarrior, Unlicense) geprüft, dessen
Wasserschaum von beiden abhängt.

## Was kaputtgeht

- `_CameraDepthNormalsTexture` gibt es in URP nicht. Ein Shader, der sie liest, bekommt nichts: Umrisse aus Normalen und
  Schaum entlang einer Küste verschwinden.
- `Camera.SetReplacementShader` und `Camera.RenderWithShader` werden in URP nicht unterstützt. Ein Skript, das die Szene
  mit einem Replacement-Shader in eine Textur rendert (ein üblicher Weg, Normalen oder Tiefe für einen Effekt zu
  bekommen), rendert ohne Meldung nichts Brauchbares. In ToonWaterShader ging so der Schaum verloren.

Befunde: „Liest _CameraDepthNormalsTexture“ (`SHADER.DEPTHNORMALS_TEXTURE`), ein Replacement-Shader-Aufruf
(`SCRIPT.REPLACEMENT_SHADER`).

## Was Urpify tut (Pro)

- Urpifys Renderer Feature Built-in Camera Hooks schreibt `_CameraDepthNormalsTexture` aus den Tiefen- und
  Normalentexturen von URP in der Kodierung von Built-in (`EncodeDepthNormal`), sodass der Shader sie wie vorher liest. Das
  Feature fordert die Normalentextur von URP nur an, wenn ein Shader sie braucht.
- Eine Kamera mit Replacement-Shader wird zu einem Pass desselben Renderer Features, das die undurchsichtigen Objekte der
  Kamera mit dem Replacement-Shader in dasselbe Ziel zeichnet, mit derselben Tag-Zuordnung (`RenderType`).

## Wie nah

`Auto, angenähert`. Die Normalen von URP stammen aus seinem eigenen Depth-Normals-Pass, daher können Kanten leicht von
Built-in abweichen.
