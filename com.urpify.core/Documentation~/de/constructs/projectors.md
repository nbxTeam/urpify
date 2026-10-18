# Projektoren

![Stand C01: ein Schattenfleck eines Projektors, verschwindet nach dem Konverter von Unity, kommt nach Urpify zurück](../../images/C01.jpg)

*Stand C01: Eine `Projector`-Komponente wirft einen Schattenfleck unter einen Würfel. Nur nach dem Konverter von Unity ist
kein Schatten da.*

## Was kaputtgeht

URP rendert keine `Projector`-Komponenten. Schattenflecken, Auswahlringe und projizierte Decals verschwinden ohne Meldung.

Befunde: „Projector an ...“ (`SCENE.PROJECTOR`), „Projektor-Shader“ (`SHADER.PROJECTOR`).

## Was Urpify tut (Pro)

Der `Projector` bleibt ein Projektor und sein Shader bleibt, wie er ist. Urpifys Renderer Feature Built-in Camera Hooks
zeichnet das Material jedes aktivierten Projektors auf die Renderer in seinem Sichtkegel und seinen Ebenen, mit
`unity_Projector` und `unity_ProjectorClip` pro Objekt gesetzt, wie in Built-in. Größe, Ausrichtung, Reichweite,
ignorierte Ebenen und Material kommen aus der Komponente, daher bearbeitest du Projektoren wie bisher.

Das Feature hält einen Cache der Projektoren und der Renderer, die sie erreichen, statt jedes Bild die Szene zu
durchsuchen: Projektoren werden alle 30 Bilder aktualisiert, Renderer alle 60, beide beim Laden oder Entladen einer Szene.
Renderer werden gegen den Sichtkegel jedes Projektors und der Kamera geprüft. Gemessen unter Unity 6.5 mit einem
Projektor: 0,02 ms pro Bild bei 88 Renderern; mit 5000 zusätzlichen Renderern etwa 2,2 ms, wenn alle vor der Kamera sind,
und 0,09 ms, wenn sie hinter ihr sind.

Da sich weder am Projektor noch an seinem Shader etwas ändert, funktioniert ein mit „Built-in-Unterstützung behalten“
exportiertes Paket weiter in beiden Pipelines.

## Wie nah

`Auto, angenähert`: derselbe Shader auf denselben Objekten. Der eigene Decal Projector von URP wird nicht verwendet, daher
bleibt der Look wie in Built-in.
