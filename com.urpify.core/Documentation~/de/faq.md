# FAQ

## Ändert Urpify mein Projekt beim Scannen?

Nein. Ein Scan liest nur Dateien. Änderungen machen nur die Schritte des Migrationsfensters, die es sagen, und die
Schaltfläche „Konvertieren“ im Shader-Bereich, jeweils zuerst mit Sicherung.

## Wie viel von meinem Projekt wird konvertiert?

Der Scan sagt es, bevor sich etwas ändert: Die Übersicht zählt die defekten Assets danach, wer sie behebt.
Die Produktseite nennt, was Urpify in einem unabhängigen Test an Shadern konvertiert hat, die seine Entwickler nicht
gesehen haben.

## Kann ich eine Migration rückgängig machen?

Ja. **Alle zurücksetzen** stellt jede Datei wieder her, die die Migration geändert hat, und verschiebt die Dateien, die sie
erstellt hat, in die Sicherung. Dateien, die du nach der Migration geändert hast, und Dateien, die zwischen den Schritten
entstanden sind, bleiben, wie sie sind, und werden aufgelistet. Ein unterbrochener Schritt wird wie ein abgeschlossener
zurückgesetzt. Committe dein Projekt trotzdem, bevor du anfängst: Einem Commit vertraust du am meisten.

## Brauche ich Pro?

Das kostenlose Paket findet alles, was kaputtgeht, führt den Konverter von Unity zuverlässig aus, behebt, was der
Konverter von Unity falsch macht, vergleicht Szenen vorher und nachher und setzt zurück. Pro konvertiert, was das
kostenlose Paket nicht kann: eigene Shader, Bildeffekte, Kamera-Skripte, Command Buffer und Projektoren. Die Übersicht
zeigt vor dem Kauf, wie viele deiner defekten Assets Pro brauchen.

## Schreibt Urpify den Code meines Shaders um?

Nur die Verdrahtung. Die Funktionen des Autors (`surf`, `vert`, `frag`, Beleuchtungsfunktionen, Hilfsfunktionen) werden
unverändert übernommen; was sich ändert, ist, wie der Shader Lichter, Schatten, Tiefe, das Bildschirmbild und Matrizen
bekommt. Built-in-Namen funktionieren über Urpifys Kompatibilitätsbibliothek weiter. Shadername und Eigenschaftsnamen
bleiben, sodass Materialien ihre Werte behalten.

## Was, wenn ein konvertierter Shader falsch aussieht?

Jeder konvertierte Shader wird in Built-in und URP auf einer Kugel mit deinen Materialien gerendert. Weicht er ab, wird er
als `Prüfen` markiert, und das Bildpaar liegt in `Urpify/Reports/self-check/`. Öffne ihn im Shader-Bereich: Der Reiter
„Vorschau“ zeigt Built-in und URP nebeneinander, der Reiter „Code“ jede Änderung mit Grund. Dort kannst du das Ergebnis
bearbeiten und neu kompilieren oder den Shader mit der Migration zurücksetzen.

## Funktioniert es mit meiner Unity-Version?

Getestet mit Unity 2022.3 LTS, 6000.0 LTS, 6000.3 LTS, 6000.5 und 6000.6; 6000.1, 6000.2 und 6000.4 sind nicht getestet. Siehe [Unity- und URP-Versionen](urp-version-matrix.md).

## Funktioniert es in CI?

Ja. Jeder Schritt läuft über die Kommandozeile und schreibt ein JSON-Ergebnis, siehe [Kommandozeile](cli.md).

## Macht „Built-in-Unterstützung behalten“ Builds größer?

Ein wenig. Auf dem Teststand (Unity 6.5, Mono) waren die Spieldaten 32,5 MB in Built-in, 38,2 MB in URP mit reinen
URP-Shadern und 38,5 MB mit Shadern für beide Pipelines: 0,34 MB oder 0,9 % mehr.

## Kosten konvertierte Bildeffekte und Projektoren in URP mehr?

Sie laufen über Urpifys Renderer Feature, das dieselben Zeichenaufrufe in das Bild von URP aufzeichnet. Ein Projektor
kostet in einer kleinen Szene etwa 0,02 ms pro Bild; mit 5000 sichtbaren Renderern etwa 2,2 ms. Gebaute Player behalten
die Effekte, in Mono- und IL2CPP-Builds.

## Was sendet „Bericht senden“?

Nichts, bis du auf „Senden“ drückst. Bericht > Bericht senden... packt den ausgewählten Shader, seine Include-Dateien, die
Befunde, die Versionen von Unity, URP und Urpify, Compilerfehler und, wenn du sie angibst, deinen Kommentar und Kontakt.
Das Fenster listet jede Datei vor dem Senden auf. **Senden** lädt das Archiv in unseren Support-Kanal hoch; **Als ZIP
speichern** speichert es auf deinem Rechner. Die Dateien dienen nur dazu, herauszufinden, was dem Konverter fehlt, und
werden danach gelöscht. Das ist der einzige Netzwerkaufruf in Urpify. Höchstens ein Bericht pro Minute.

## Darf ich Shader verkaufen, die Urpify konvertiert hat?

Ja, unter ihren eigenen Lizenzen. Siehe [Weitergabe konvertierter Shader](../license-redistribution.md) (Englisch). Ein
Shader, dessen Kopf eine nichtkommerzielle Lizenz nennt, bleibt unter ihr; der Shader-Bereich zeigt die Lizenzzeile.

## Warum sagt der Scan, mein Projekt sei vor der Migration kaputt gewesen?

Weil es so war: ein Shader, der in Built-in nicht kompiliert, Skripte, die nicht kompilieren, eine Include-Datei, die
nicht im Projekt ist, ein Material, das nicht im Projekt ist. Urpify zeigt das getrennt, weil die Migration es nicht
behebt. Behebe es zuerst, dann scanne erneut.

## Nach einem Rollback von URP zeigen Shader einmal Include-Fehler. Warum?

Nachdem URP aus einem Projekt entfernt wurde, das es hatte (Rollback oder manuelles Zurücksetzen), kann die erste
Editor-Sitzung noch die URP-SubShader von Shadern kompilieren, die mit „Built-in-Unterstützung behalten“ erstellt wurden,
und Include-Fehler melden, wegen Importergebnissen, die in `Library/Artifacts` übrig sind. In der zweiten Sitzung sind
sie weg. Das ist Unitys Importverhalten, nicht die Shader-Dateien: In einem Projekt, das nie URP hatte, gibt es keine
Fehler. Öffne das Projekt noch einmal oder führe Assets > Reimport All aus.

## Das Fenster ist in einer anderen Sprache.

Project Settings > Urpify > Persönlich > Sprache. Standardmäßig folgt die Sprache dem Editor und nutzt Englisch, wenn
Urpify diese Sprache nicht hat.
