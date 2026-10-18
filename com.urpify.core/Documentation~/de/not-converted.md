# Was nicht konvertiert wird und warum

Urpify listet alles, was es nicht konvertiert, mit Grund auf, bevor die Migration beginnt. Jeder Eintrag unten gehört zu
einer von drei Gruppen:

- **Von Hand: Das Projekt war vor der Migration kaputt.** Urpify behebt keine Fehler des Projekts selbst; es sagt, was
  kaputt ist und was zuerst zu tun ist.
- **Von Hand: Es gibt nichts zu konvertieren.** URP hat kein Gegenstück, geprüft an den Quellen von URP.
- **Noch nicht unterstützt.** Möglich, aber diese Version von Urpify tut es nicht. Daran arbeiten wir als Nächstes; der
  Befund sagt, was du bis dahin von Hand tun kannst.

Welcher Anteil der Shader konvertiert wird, gemessen in einem unabhängigen Test an Shadern, die die Entwickler nicht
gesehen haben, steht auf der Produktseite. Diese Seite listet, was nicht konvertiert wird und warum.

## Das Projekt war vor der Migration kaputt

| Fall | Was du siehst | Was zu tun ist |
|---|---|---|
| Ein Shader kompiliert in der Built-in-Pipeline nicht | „Shader kompiliert bereits nicht“ | Behebe zuerst die Kompilierfehler in Built-in; die Objekte sind schon pink. |
| Die Skripte des Projekts kompilieren nicht | „Projektskripte kompilieren nicht“ | Behebe sie zuerst: Sie blockieren den Play Mode, die Aufnahmen und die Schritte, die Skripte umschreiben. Alte Projekte müssen oft Aufrufe veralteter APIs aktualisieren. |
| Ein Shader bindet eine Datei ein, die nicht im Projekt ist | „Include nicht gefunden“ | Füge die Datei hinzu oder korrigiere den Pfad, dann scanne erneut. |
| `UsePass` nennt einen Shader, der nicht im Projekt ist | „UsePass ...: Shader nicht gefunden“ | Füge den Shader hinzu oder entferne die Zeile `UsePass`. Das Objekt ist auch in Built-in pink. |
| Ein Renderer verweist auf ein Material, das nicht im Projekt ist | „Fehlendes Material an ... Renderern“ | Weise ein Material zu oder hole das fehlende Material oder Paket zurück. Das passiert, wenn Unity ein Paket aktualisiert (zum Beispiel ProBuilder), dessen altes Standardmaterial weg ist. |

## Es gibt nichts zu konvertieren

| Fall | Warum |
|---|---|
| Interne Shader des Built-in-Renderers: `Internal-DeferredShading`, `Internal-DeferredReflections`, `Internal-ScreenSpaceShadows`, `Internal-DepthNormalsTexture`, `Internal-MotionVectors`, `Internal-PrePassLighting`, `Internal-Flare`, `Internal-Halo` und ihre Kopien | Built-in führt sie aus den Slots für eingebaute Shader in den Graphics Settings aus. URP liest diese Slots nie: Die Quellen von URP 14.0.12 und 17.5.0 rufen nie `GraphicsSettings.GetCustomShader` auf und nutzen `BuiltinShaderType` nicht. Deferred-Beleuchtung, Schatten, Depth-Normals und Bewegungsvektoren rendert URP mit eigenen Shadern. |
| Passes mit LightMode `Deferred`, `PrepassBase`, `PrepassFinal` | Die Deferred- und Legacy-Deferred-Pfade von Built-in. URP rendert das Objekt in seinem Forward-Pass, den Urpify aus den Forward-Passes des Shaders baut. |
| `finalgbuffer:`-Funktionen von Surface-Shadern | Sie laufen nur im Deferred-G-Buffer-Pass von Built-in; der konvertierte Forward-Shader ruft sie nicht auf. |
| `Camera.depthTextureMode` | URP ignoriert es. Tiefen- und Normalentexturen erzeugt URP nach den Einstellungen des URP Asset, und Urpify schaltet sie ein, wenn ein Shader sie liest. |

## Noch nicht unterstützt

| Konstrukt | Was jetzt passiert | Was du von Hand tun kannst |
|---|---|---|
| Effekte, die von `Light.AddCommandBuffer` gespeist werden (volumetrisches Licht und Ähnliches) | Der Effekt fehlt in URP; der Rest des Bildes rendert. | Verschiebe die Befehle des Lichts in ein Renderer Feature bei `BeforeRenderingShadows` oder `AfterRenderingShadows`; URP rendert Shadow Maps anders, prüfe also, was die Befehle erwarten. |
| Bildeffekte, deren `OnRenderImage` ein Kamera-Rendering, einen Compute-Dispatch oder anderes sofortiges Zeichnen erreicht | Der Effekt fehlt unter URP; der Rest des Bildes wird gezeichnet. | Verschiebe diesen Aufruf aus dem Effekt: nach `OnPreRender` oder in einen Command Buffer, den `ImageEffectGraphics.ExecuteCommandBuffer` ausführt. |
| Bildeffekte, deren Shader den G-Buffer von Built-in lesen (`_CameraGBufferTexture0-3`: Screen-Space-Reflexionen und Ähnliches) | Der Effekt fehlt unter URP; der Rest des Bildes wird gezeichnet. | Baue die Eingaben aus Tiefen-, Normalen- und Opaque-Textur von URP neu auf oder nutze den Deferred-Renderer von URP mit seiner G-Buffer-Anordnung. |
| Delegates `Camera.onPreCull`, `Camera.onPreRender`, `Camera.onPostRender` | Code, der dort angehängt ist, läuft nicht mehr. | Verschiebe ihn nach `RenderPipelineManager.beginCameraRendering` oder `endCameraRendering`. |
| `OnRenderImage`, das sofort zeichnet (`Graphics.DrawMeshNow` und Ähnliches) | Der Effekt fehlt. | Zeichne mit einem Command Buffer (`CommandBuffer.Blit`, `DrawMesh`) und führe ihn mit `ImageEffectGraphics.ExecuteCommandBuffer` aus. |
| `OnRenderImage`, das eine Kamera rendert (`Camera.Render`, auch über eine Hilfsmethode) | Der Effekt läuft unter URP nicht: Im Bild seiner eigenen Kamera kann keine andere Kamera rendern, und das Bild würde schwarz. | Rendere die andere Kamera in `OnPreRender` oder `OnPreCull` und lass in `OnRenderImage` nur die Blit-Aufrufe. Ein Blit, der den Stencil-Puffer der Kamera testet, sieht ihn unter URP ebenfalls nicht. |
| Command Buffer bei den Deferred-Beleuchtungsereignissen von Built-in an einem Licht | Fehlen. | Ein Renderer Feature beim passenden URP-Ereignis. |
| Shader, die den G-Buffer von Built-in lesen (`_CameraGBufferTexture0` bis `3`), etwa Screen-Space-GI | Der Effekt verliert seine Eingabe ohne Fehlermeldung: Der Forward-Renderer von URP hat keinen G-Buffer, und der Deferred-Pfad von URP packt seinen eigenen anders (`_GBuffer0` bis `_GBuffer6`). | Baue die Eingaben aus den Tiefen-, Normalen- und Opaque-Texturen von URP neu, oder stelle den URP-Renderer auf Deferred um und schreibe die Zugriffe für `UnityGBuffer.hlsl` um. |
| Mehrere GrabPass mit verschiedenen Texturen in einem Shader | Der Shader wird nicht konvertiert. | URP hat eine Opaque Texture: Fasse die Aufnahmen zusammen oder behalte eine und lies sie zweimal. |
| Mehr als 32 gezeichnete Passes in einem SubShader | Nicht konvertiert. | Fasse Passes zusammen oder verschiebe einige in einen eigenen SubShader oder ein eigenes Material. |
| `UsePass` aus einem Shader, dessen Pass in einem `CGINCLUDE`-Block steht | Nicht konvertiert. | Kopiere den Pass in den Shader. |
| Ein eingebautes Include, für das Urpify noch kein Gegenstück hat | „Stopped: ... includes ...“, der Shader wird nicht konvertiert. | Portiere die Funktionen des Includes von Hand oder schick den Shader über „Bericht senden“. |
| Ein `ForwardAdd`-Pass, der einige Lichtvariablen von Built-in direkt liest, pro Vertex beleuchtet oder keine Farbe zurückgibt | Nicht konvertiert. | Schreib den Pass mit `UNITY_LIGHT_ATTENUATION` um oder verschiebe ihn in die Schleife der zusätzlichen Lichter von URP. |
| Eine Beleuchtungsfunktion mit einer Signatur, die der Konverter nicht kennt | Nicht konvertiert. | Nutze eine der dokumentierten `Lighting<Name>`-Signaturen von Built-in. |
| Passes der Fixed-Function-Pipeline (ohne Shader-Programm) | Nicht konvertiert: URP zeichnet nur Passes mit Programm. | Schreib den Pass als Vert/Frag-Programm um. |
| Ein Material Variant, dessen Datei vor dem Konverter von Unity nicht in der Sicherung ist | Eigene Werte gehen im Konverter von Unity verloren. | Migriere mit den Urpify-Schritten, die die Datei sichern, oder setze die Werte erneut. |
| Ein konvertierter Shader oder ein Skript, das nicht kompiliert | Das Original wird zurückgestellt, mit der Compilermeldung im Befund. | Korrigiere das Ergebnis im Shader-Bereich und speichere es mit „Speichern unter“, oder schick den Shader über „Bericht senden“. |

## Konvertiert, aber nicht identisch

Das wird konvertiert und funktioniert, mit einem Unterschied, der von URP selbst kommt, nicht von der Konvertierung.
Bilder auf der Seite [Wie URP anders aussieht](urp-differences.md).

- **GrabPass mit Opaque Texture** (Standard): URP kopiert den Bildschirm vor den transparenten Objekten, daher fehlen sie
  in der Brechung. Wähle die GrabPass-Quelle „Nach Transparenten“, um danach zu kopieren.
- **Eigene Beleuchtungsfunktionen und ForwardAdd-Passes:** Die Funktion des Autors läuft für das Hauptlicht und jedes
  zusätzliche Licht in einem Pass, mit den Schatten und der Abschwächung von URP.
- **Bloom aus dem Post-processing Stack v2:** wird zum Bloom von URP, dessen Leuchten weicher und breiter ist; die
  Bloom-Pyramide von URP verteilt kleine helle Stellen anders.
- **Standard zu URP Lit** (Unitys eigene Konvertierung): weicht in unseren Testszenen um bis zu etwa 7 % des Frames ab.
- **Spiegelungen in `OnWillRenderObject`** werden vor dem Culling von URP für die Renderer aufgerufen, die die Kamera
  sieht, ohne Occlusion Culling, daher rendert eine Spiegelung hinter einer Wand trotzdem.
