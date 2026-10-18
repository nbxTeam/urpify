# Ein Spiel migrieren

Du hast ein Spiel mit der Built-in Render Pipeline und möchtest es auf URP haben, mit demselben Aussehen.

## Bevor du anfängst

- Committe das Projekt. Urpify sichert, was es ändert, aber ein Commit ist der Wiederherstellungspunkt, dem du am
  meisten vertraust.
- Stell sicher, dass das Projekt kompiliert und in Built-in in den Play Mode geht. Urpify meldet Kompilierfehler in
  Skripten als Befund: Sie blockieren den Play Mode und damit die Aufnahmen.
- Leg fest, welche Szenen verglichen werden: Project Settings > Urpify > Vergleich > Szenen. Standardmäßig die
  Szenen aus den Build Settings.
- Platziere einige Migration Viewpoints in Szenen, in denen ein Effekt wichtig ist: ein Portal, ein Spiegel,
  Wasser, ein Post-Effekt. Füge die Komponente `MigrationViewpoint` zu einem leeren GameObject hinzu und richte
  es wie eine Kamera aus. Es ist als EditorOnly markiert und landet nie in einem Build.

## Scannen und die Übersicht lesen

Window > Rendering > Urpify > **Projekt scannen**. Die Übersicht beginnt mit den Szenen, die kaputtgehen, und zählt dann
die defekten Assets danach, wer sie behebt:

- **Unitys Konverter und Urpify**: eingebaute Shader, die der Render Pipeline Converter von Unity umstellt, und die
  Fälle, in denen sein Ergebnis falsch ist und die das kostenlose Paket danach behebt.
- **Urpify Pro**: eigene Shader, Bildeffekte, Kamera-Skripte, Command Buffer und Projektoren.
- **Von Hand oder noch nicht unterstützt**: Das Projekt war schon vor der Migration kaputt, URP hat kein Gegenstück für
  das Konstrukt, oder Urpify konvertiert es noch nicht. Siehe [Was nicht konvertiert wird](not-converted.md).

Die Liste links lässt sich nach Problem, Korrektur und Typ filtern und nach Dateiname oder Regel-ID durchsuchen. **Ohne Pro
behoben** zeigt, was das kostenlose Paket erledigt.

## Migrieren

**Migrieren...** > **Alle ausführen**, oder Schritt für Schritt. Schritte, die Dateien ändern, sichern sie zuerst. Während
ein Schritt läuft, zeigt das Fenster seinen Fortschritt, und jeder Schritt lässt sich erneut ausführen. Schließt sich der
Editor mitten in einem Schritt, bietet das Fenster beim nächsten Öffnen an, den Schritt erneut auszuführen oder
zurückzusetzen.

## Vergleichen und auswerten

Öffne nach **Nachher aufnehmen und vergleichen** die Ansicht **Szenen vergleichen**:

- **Gleich**: Die Frames stimmen innerhalb der Schwellen überein, die an Standard zu URP Lit kalibriert sind.
- **Verändert**: Einige Pixel weichen ab, oder ein Bereich hat sich stark verändert (eine verlorene Spiegelung, ein
  verlorenes Leuchten), oder die Szene hat beim Abspielen Fehler protokolliert.
- **Neues Pink**: Ein Objekt wird mit dem Fehler-Shader gerendert.

Sieh dir jede veränderte Szene mit **Unterschiede zeigen** an. Manche Unterschiede kommen von URP, nicht von der
Migration: siehe [Wie URP anders aussieht](urp-differences.md).

Der Abschlussbericht (**Auswertung**) listet jede konvertierte Datei mit ihrer Stufe: `Auto` (praktisch identisch),
`Angen.` (nah dran, nicht pixelgenau), `Prüfen` (konvertiert, aber Urpifys eigener Test-Render weicht vom Original ab:
prüf ihn) und was von Hand erledigt werden muss.

## Zurücksetzen

**Alle zurücksetzen** stellt das Projekt so wieder her, wie es vor dem ersten Schritt war. **Bis hierher zurücksetzen** an
einem Schritt macht jeden Schritt danach rückgängig. Dateien, die du nach der Migration geändert hast, bleiben, wie sie
sind, und werden in der Console aufgelistet.

## In einer Build-Pipeline

Jeder Schritt läuft über die Kommandozeile, sodass ein Build-Server einen Branch migrieren und den Build fehlschlagen
lassen kann, wenn eine Szene pink wird. Siehe [Kommandozeile](cli.md).
