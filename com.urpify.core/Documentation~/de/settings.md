# Einstellungen

Project Settings > **Urpify** oder die Schaltfläche **Einstellungen** im Urpify-Fenster.

Team-Einstellungen liegen in `ProjectSettings/UrpifySettings.asset`: Committe die Datei, und das ganze Team konvertiert
gleich. Persönliche Einstellungen liegen in `UserSettings/Urpify.json` und bleiben auf deinem Rechner.

## Konvertierung (Team)

Diese Einstellungen bestimmen, was die Pro-Konverter schreiben.

| Einstellung | Werte | Standard | Was sie tut |
|---|---|---|---|
| Ausgabe | Original ersetzen, Kopie schreiben, Built-in-Unterstützung behalten | Original ersetzen | **Original ersetzen**: Die Shader-Datei wird reines URP; das Original liegt in der Sicherung. **Kopie schreiben**: Der URP-Shader wird neben das Original geschrieben, als `Name_URP.shader` mit dem Namen `Name (URP)`, und die Materialien wechseln zu ihm; das Original bleibt. **Built-in-Unterstützung behalten**: eine Datei für beide Pipelines; die ursprünglichen SubShader bleiben für Built-in, und ein URP-SubShader kommt davor. |
| GrabPass-Quelle | Opaque Texture, Nach Transparenten | Opaque Texture | **Opaque Texture**: Ein konvertierter GrabPass liest die Bildschirmkopie von URP, aufgenommen vor transparenten Objekten, daher fehlen sie in der Brechung. **Nach Transparenten**: Urpifys Renderer Feature kopiert den Bildschirm nach den transparenten Objekten, wie es GrabPass in Built-in tat, und zeichnet die Objekte mit GrabPass danach. Kostet eine weitere Bildschirmkopie pro Bild. |
| Bildschirmeffekte einfügen bei | Nach Post-Processing, Vor Post-Processing, Nach Opaken | Nach Post-Processing | Wo im Bild von URP konvertierte `OnRenderImage`-Effekte laufen. Effekte mit `[ImageEffectOpaque]` laufen immer nach dem Himmel, wie in Built-in. |
| Schatten | Wie im Original, Immer, Nie | Wie im Original | Ob konvertierte Surface-Shader Schatten werfen und empfangen. |
| SRP Batcher | An, Aus | An | Legt Materialeigenschaften in einen `UnityPerMaterial`-Puffer, damit der SRP Batcher von URP konvertierte Shader bündelt. Ein Shader, der seinen eigenen `UnityPerMaterial` deklariert, behält ihn; eine Eigenschaft, die sich nicht sicher verschieben lässt, bleibt draußen, mit einem Hinweis. |

## Vergleich (Team)

| Einstellung | Standard | Was sie tut |
|---|---|---|
| Aufnahmegröße | Game-Ansicht | Größe jedes aufgenommenen Frames: die der Game-Ansicht, damit auflösungsabhängige Effekte so aussehen, wie Sie sie sehen, oder eine eigene. Die Befehlszeile nutzt `-urpifyWidth` und `-urpifyHeight`. |
| Aufnahmebreite, Aufnahmehöhe | 960 × 540 | Die Größe, wenn Aufnahmegröße auf Eigene steht, und wenn es keine Game-Ansicht gibt. |
| Bilder vor der Aufnahme | 30 | Wie viele Bilder mit festem Zeitschritt vor jeder Aufnahme abgespielt werden, damit Animationen und Partikel vorher und nachher im selben Zustand sind. |
| Szenen | Build Settings | Welche Szenen „Vorher aufnehmen“ und „Nachher aufnehmen“ verwenden: die aus den Build Settings, alle Szenen unter `Assets` oder eine Liste. |
| Szenenpfade | leer | Für „Ausgewählte Szenen“: Pfade, durch Semikolon getrennt, etwa `Assets/Scenes/Main.unity;Assets/Scenes/Menu.unity`. |

Die Schwellen für „Neues Pink“, „Verändert“ und „Gleich“ sind an Unitys eigener Konvertierung von Standard zu URP Lit
kalibriert und gelten für jedes Projekt: Ein neuer pinker Bereich über 0,02 % des Frames ist „Neues Pink“; mehr
veränderte Pixel, als Standard → URP Lit erzeugt, oder ein großer veränderter Bereich ist „Verändert“.

Jede Szene kann **Migration Viewpoints** haben: Füge die Komponente `MigrationViewpoint` zu einem leeren GameObject hinzu,
um zusätzlich zur Hauptkamera aus seiner Position und Richtung aufzunehmen. Ein Viewpoint kann ein eigenes Sichtfeld und
eine eigene ferne Schnittebene haben. Er ist als EditorOnly markiert und landet nie in einem Build.

## Persönlich

| Einstellung | Werte | Standard | Was sie tut |
|---|---|---|---|
| Sprache | Editor-Sprache oder eine der 8 Sprachen | Editor-Sprache | Sprache der Urpify-Fenster. „Editor-Sprache“ folgt Preferences > Languages und nutzt Englisch, wenn es die Sprache nicht gibt. |
| Built-in-Vorschau | Live, Aus Aufnahmen | Live | **Live** schaltet die Render Pipeline für den Moment des Built-in-Vorschau-Renders aus und wieder ein, in einem Aufruf; nie im Play Mode und nie während eines Imports oder Kompilierens. **Aus Aufnahmen** zeigt stattdessen die Szenenaufnahmen. Schlägt die Live-Vorschau fehl oder kommen die Pipeline-Einstellungen nicht exakt zurück, wechselt Urpify selbst zu „Aus Aufnahmen“ und sagt es. |
| Overlays im Project-Fenster | An, Aus | An | Eine kleine Markierung an Assets, die der letzte Scan als defekt gefunden hat. |

## Überschreiben für einen Lauf

Die Kommandozeile kann „Ausgabe“ (`-urpifyShaderOutput`) und „GrabPass-Quelle“ (`-urpifyGrabPass`) für einen Lauf
überschreiben, siehe [Kommandozeile](cli.md).
