# Skripte, die Kameras rendern: Spiegel, Portale, Wasserreflexionen

![Stand M01: ein ebener Spiegel, schwarz nach dem Konverter von Unity, spiegelt nach Urpify wieder](../../images/M01.jpg)

*Stand M01: ein ebener Spiegel, geschrieben wie MirrorReflection und Standard Assets Water: `OnWillRenderObject`,
`Camera.current`, eine Spiegelkamera mit schräger naher Schnittebene und `Camera.Render`. Nur nach dem Konverter von
Unity ist der Spiegel schwarz.*

## Was kaputtgeht

URP ruft `OnPreCull`, `OnPreRender` und `OnPostRender` einer Kamera nie auf und `OnWillRenderObject` während des Cullings,
wo keine andere Kamera rendern kann. Portale und Spiegel, die in diesen Methoden eine zweite Kamera rendern, zeigen eine
flache oder schwarze Fläche. Es gibt keinen Fehler. Geprüft mit URP 14 und 17.

Befunde: „OnPreCull an einer Kamera“ und weitere (`SCRIPT.CAMERA_CALLBACKS`), „OnWillRenderObject rendert eine Kamera“
(`SCRIPT.WILLRENDER_CAMERA`).

## Was Urpify tut (Pro)

Das Skript behält seine Methoden und seine Logik. Urpify meldet es bei den Kameraereignissen von URP an:

- `OnPreCull` und `OnPreRender` werden aus `RenderPipelineManager.beginCameraRendering` ihrer Kamera aufgerufen,
  `OnPostRender` aus `endCameraRendering`.
- `OnWillRenderObject` wird vor dem Culling der URP-Kamera für die Renderer aufgerufen, die die Kamera sieht, wobei
  `Camera.current` auf diese Kamera zeigt.
- `Camera.Render()` darin rendert über eine Render-Anfrage in das Bild von URP, sodass die Spiegel- oder Portaltextur
  gefüllt ist, bevor die anzeigende Kamera sie zeichnet.
- `OnRenderObject` wird nach den undurchsichtigen und transparenten Objekten der Kamera aufgerufen.

Bekannte Skripte erkennt Urpifys Katalog: Portals von SebLague, MirrorReflection, Standard Assets Water und die
Normalen-Kamera von ToonWaterShader werden mit Rezepten konvertiert, die an diesen Projekten geprüft sind. Die übrigen
nutzen denselben Mechanismus und bekommen „Auto, angenähert“.

## Wie nah

`Auto` für die geprüften Rezepte des Katalogs, `Auto, angenähert` für die übrigen: Prüfe diese Szenen im Szenenvergleich.
Ein Unterschied zu Built-in: `OnWillRenderObject` wird für Renderer im Sichtkegel der Kamera ohne Occlusion Culling
aufgerufen, daher rendert ein hinter einer Wand verborgener Spiegel trotzdem seine Spiegelung.

Code, der an die Delegates `Camera.onPreCull`, `Camera.onPreRender` und `Camera.onPostRender` angehängt ist, wird noch
nicht konvertiert; der Befund sagt, wie du ihn verschiebst. Ein `OnRenderImage`, das eine Kamera rendert, läuft unter URP
nicht, siehe [Was nicht konvertiert wird](../not-converted.md).
