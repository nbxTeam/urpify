# Einen Shader konvertieren

Du schreibst oder pflegst Shader und möchtest einen davon auf URP haben, ohne die Formel anzutasten.

## Den Shader öffnen

Jeder dieser Wege öffnet den Shader-Bereich:
- den Shader nach einem Scan in der Liste des Urpify-Fensters auswählen;
- Rechtsklick auf den Shader im Project-Fenster > Urpify > Analyze;
- eine `.shader`-Datei in das Urpify-Fenster ziehen, aus dem Project-Fenster oder direkt von der Festplatte. Eine Datei
  von der Festplatte bleibt, wo sie ist; nichts wird importiert.

## Den Code lesen

Der Reiter **Code** zeigt links das Original und rechts den konvertierten Shader. Geänderte Zeilen sind markiert; fahr
mit der Maus über eine, um zu sehen, warum sie geändert wurde: ein Pass mit neuem Tag für URP, ein ersetztes
Built-in-Include, Materialeigenschaften, die für den SRP Batcher in `UnityPerMaterial` verschoben wurden.

Was Urpify ändert und was es in Ruhe lässt:
- Der Name des Shaders und die Namen seiner Eigenschaften bleiben, sodass Materialien weiter funktionieren und ihre Werte
  behalten.
- Die Funktionen des Autors (`surf`, `vert`, `frag`, Beleuchtungsfunktionen, Hilfsfunktionen) werden unverändert
  übernommen. Nur die Verdrahtung ändert sich: wie der Shader Lichter, Schatten, Tiefe, das Bildschirmbild und Matrizen
  bekommt.
- Built-in-Namen wie `UnityObjectToClipPos`, `_LightColor0` oder `UNITY_LIGHT_ATTENUATION` funktionieren weiter über
  Urpifys Kompatibilitätsbibliothek `BirpCompat.hlsl`, die sie auf URP abbildet.

Mit **Ergebnis bearbeiten** kannst du den konvertierten Code ändern und mit den Keywords seiner Materialien
**Kompilieren**; Fehler erscheinen an ihren Zeilen. **Speichern unter...** schreibt das Ergebnis in eine neue Datei;
**Konvertieren** ersetzt das Original, nachdem es nach `Urpify/Backup/ShaderPanel` gesichert wurde.

Kann der Konverter nicht zu Ende kommen, zeigt die rechte Spalte einen schreibgeschützten Entwurf mit der Zeile, an der er
angehalten hat, und dem Grund. Du kannst den Entwurf speichern, um ihn von Hand fertigzustellen; Konvertieren und die
Migration verwenden ihn nie.

## Vorschau

Der Reiter **Vorschau** rendert den Shader auf einer Kugel, einem Würfel, einer Ebene oder deinem eigenen Mesh, in
Built-in und in URP, mit denselben Materialwerten in beiden. **Ansicht** wechselt zwischen Built-in, URP, einem Vorhang
mit Schieberegler und einer Differenzkarte. **Lichtrichtung** dreht das Hauptlicht. Ändere eine Materialeigenschaft,
und beide Vorschauen folgen.

Die Built-in-Vorschau schaltet die Render Pipeline für den Moment des Renderns aus und wieder ein, innerhalb eines
Aufrufs. Schlägt das fehl, wechselt der Bereich zu **Aus Aufnahmen** und zeigt stattdessen die Szenenaufnahmen. Bei einer
bereits konvertierten Datei rendert die Built-in-Hälfte das Original aus der Sicherung der Migration.

## Das Ergebnis prüfen

Jeder Shader, den die Migration konvertiert, wird außerdem durch einen Render geprüft: eine Kugel mit den eigenen
Materialien des Projekts für diesen Shader, in Built-in vor der Konvertierung und in URP danach. Weicht mehr als 25 % der
Kugel ab, wird der Shader trotzdem konvertiert, aber als `Prüfen` markiert, und das Bildpaar bleibt in
`Urpify/Reports/self-check/`. Ein Shader, dessen konvertierter Code nicht kompiliert, wird in den alten Zustand
zurückversetzt und gemeldet.

## Wenn die Konvertierung anhält

Der Bereich nennt das Konstrukt, das der Konverter nicht beherrscht, und was zu tun ist. [Was nicht konvertiert
wird](not-converted.md) listet alle auf. Sollte der Shader konvertiert werden und wird es nicht, packt **Bericht > Bericht
senden...** den Shader, seine Include-Dateien und die Befunde in ein Archiv: Speichere es oder sende es uns (nur
wenn du auf Senden drückst).

## Lizenz des Shaders

Nennt die Datei in ihrem Kopf eine Lizenz, zeigt der Bereich sie an. Eine konvertierte Kopie bleibt unter derselben
Lizenz; mit einer nichtkommerziellen Lizenz darf sie nicht verkauft werden.
