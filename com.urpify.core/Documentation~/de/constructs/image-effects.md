# Bildeffekte: OnRenderImage

![Stand C02: ein Bildeffekt mit Entsättigung und Vignette, verschwindet nach dem Konverter von Unity, kommt nach Urpify zurück](../../images/C02.jpg)

*Stand C02: ein Post-Effekt mit `OnRenderImage`, Entsättigung mit Vignette. Nur nach dem Konverter von Unity verschwindet
der Effekt, ohne Fehlermeldung.*

## Was kaputtgeht

URP ruft `OnRenderImage` nie auf, daher hört jeder für Built-in geschriebene Bildeffekt auf zu wirken. Kein Fehler, kein
Pink: Das Spiel sieht einfach anders aus. Geprüft mit URP 14 und 17.

Befund: „OnRenderImage ({0} Blit-Aufrufe)“ (`SCRIPT.ONRENDERIMAGE`), Schweregrad „Bricht ohne Fehlermeldung“. Der Shader
des Effekts bekommt den Befund „Bildeffekt-Shader“.

## Was Urpify tut (Pro)

Das Skript bleibt ein MonoBehaviour an der Kamera und dient weiter als Steuerpult: Seine Felder, die Werte, die es jedes
Bild setzt, und seine Materialien ändern sich nicht. Urpifys Renderer Feature Built-in Camera Hooks ruft das
`OnRenderImage` des Skripts an der Stelle im Bild von URP auf, die in den Einstellungen gewählt ist („Bildschirmeffekte
einfügen bei“: standardmäßig nach dem Post-Processing), mit der Farbe der Kamera als Quelle. Die `Graphics.Blit`-Aufrufe
des Skripts gehen über `ImageEffectGraphics`, das sie mit denselben Materialien, Passes und temporären Render Textures in
derselben Reihenfolge in das Bild von URP aufzeichnet. Effekte mit `[ImageEffectOpaque]` laufen nach dem Himmel, wie in
Built-in. Mehrere Effekte an einer Kamera behalten ihre Reihenfolge. Zeichnen mit `GL` in `OnRenderImage` (Quads, Matrizen,
Löschen) wird ebenfalls aufgezeichnet.

Der Shader des Effekts bleibt, wie er ist: `vert_img`, `v2f_img` und `_MainTex` funktionieren, weil der Effekt den
Bildschirm weiter in `_MainTex` übergibt, wie in Built-in.

## Wie nah

`Auto` für einen einzelnen Blit, `Auto, angenähert` für Ketten und Command Buffer. Ein Effekt, der sofort mit
`Graphics.DrawMeshNow` oder Ähnlichem zeichnet oder eine Kamera rendert, lässt sich nicht in das Bild von URP aufzeichnen:
Der Befund sagt, wie du ihn umbaust.

Die `Graphics.Blit`-Aufrufe anderer Skripte, die der Effekt aufruft (ein ScriptableObject-Effekt hinter dem
Kameraskript, ein Manager, der eine Kette ausführt), werden genauso aufgezeichnet. Ein Effekt, dessen Kette eine Kamera
rendert, einen Compute-Shader startet oder den G-Buffer von Built-in liest (`_CameraGBufferTexture0-3`), bleibt
unverändert (noch nicht unterstützt): Unter URP fehlt er, und der Rest des Bildes wird gezeichnet. Wird das Bild einer
Szene nach der Migration fast einfarbig, werden die Effekte dieser Szene als „Prüfen“ markiert: Schalte sie an der Kamera
aus, um die Ursache zu finden, oder setze zurück.

Gebaute Player behalten den Effekt: Alles, was Urpify über den Namen aufruft, ist vor dem Code-Stripping geschützt,
geprüft in Mono- und IL2CPP-Builds.
