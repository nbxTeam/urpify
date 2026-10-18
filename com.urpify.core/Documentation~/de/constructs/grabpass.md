# GrabPass

![Stand G01: Glas mit GrabPass-Brechung, verschwindet nach dem Konverter von Unity, bricht nach Urpify wieder](../../images/G01.jpg)

*Stand G01: Glas mit Brechung und einem unbenannten `GrabPass {}`. Nur nach dem Konverter von Unity verschwindet das Glas,
ohne Fehlermeldung.*

![Stand G02: Hitzeflimmern mit einem benannten GrabPass](../../images/G02.jpg)

*Stand G02: Hitzeflimmern mit einem benannten `GrabPass { "_HazeBackground" }`.*

## Was kaputtgeht

URP hat keinen GrabPass. Der Pass, der den Bildschirm aufnimmt, wird übersprungen, die Textur bleibt leer, und das Objekt
verschwindet oder verliert seine Brechung. Kein Pink, kein Fehler: ein Defekt ohne Fehlermeldung.

Befund: „GrabPass (...)“ (`SHADER.GRABPASS`), Schweregrad „Bricht ohne Fehlermeldung“.

## Was Urpify tut (Pro)

Der GrabPass wird entfernt, und die Textur, die er füllte (`_GrabTexture` oder die benannte), liest stattdessen eine Kopie
des Bildschirms:

- **GrabPass-Quelle = Opaque Texture** (Standard): `_CameraOpaqueTexture` von URP, aufgenommen nach den undurchsichtigen
  Objekten und dem Himmel, vor den transparenten. Urpify schaltet Opaque Texture in jedem URP Asset ein, ohne
  Herunterskalieren.
- **GrabPass-Quelle = Nach Transparenten**: Urpifys Renderer Feature Built-in Camera Hooks kopiert den Bildschirm nach den
  transparenten Objekten in `_UrpifyGrabTexture` und zeichnet dann die Objekte mit GrabPass (Tag `UrpifyGrabPass`), wie es
  GrabPass in Built-in tat. Eine weitere Bildschirmkopie pro Bild.

Ein GrabPass, der nach einem oder zwei Passes desselben Shaders kommt (Nebel, dann eine Unschärfe, die die Aufnahme liest),
liest eine Kopie, die nach diesen Passes aufgenommen wurde, und sieht daher, was die Aufnahme in Built-in sah.

## Wie nah

`Auto, angenähert` mit Opaque Texture: Transparente Objekte hinter dem Glas fehlen in der Brechung. Mit „Nach
Transparenten“ stimmt das Ergebnis mit Built-in überein. Mehrere GrabPass mit verschiedenen Texturen in einem Shader
werden noch nicht unterstützt: URP hat eine Opaque Texture.

Unter 2022.3 rendert Urpifys Selbstprüfung GrabPass-Shader mit einer Game-Kamera: URP 14 kopiert für eine
Vorschaukamera keine Opaque Texture.
