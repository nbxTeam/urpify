# Das Urpify-Fenster

`Window > Rendering > Urpify`. Das Fenster hält seine eigenen Texte kurz; diese Seite erklärt, was seine Symbole, Zähler
und Markierungen bedeuten. Fahr mit der Maus über ein Symbol oder einen Zähler im Fenster, um dieselbe Erklärung zu sehen.

## Symbole für den Schweregrad

Jedes Symbol hat seine eigene Form, daher ist die Farbe nie der einzige Hinweis.

| Symbol | Name | Was nach dem Wechsel zu URP passiert |
|---|---|---|
| Gefülltes Quadrat (pink) | Wird pink | Das Objekt wird mit dem Fehler-Shader von URP gerendert. In einem Release-Build ist es unsichtbar. |
| Gefülltes Dreieck (bernsteinfarben) | Bricht ohne Fehlermeldung | Kein Fehler und kein Pink, aber der Effekt fehlt oder ist falsch: Ein Portal wird zur grauen Wand, Wasser verliert seinen Schaum, ein Post-Effekt verschwindet. |
| Gefüllte Raute (blau) | Sieht anders aus | Rendert, mit einem sichtbaren Unterschied, etwa einer dunkleren Szene. |
| Ring (grau) | Keine sichtbare Änderung | Nichts zu tun, oder der Konverter von Unity erledigt es so, wie es ist. |

Eine Listenzeile, eine Szene und ein Asset bekommen das Symbol ihres schlimmsten Befunds.

## Die Leiste unter der Werkzeugleiste

Zahlen in benannten Gruppen. Vor der Migration: `Szenen` und `Assets`, jeweils mit einem Symbol und einer Zahl pro
Schweregrad: wie viele Szenen Objekte mit diesem Schweregrad haben und wie viele Assets ihn haben. Eine Szene oder ein
Asset zählt in jeder Kategorie, die es hat, daher überschneiden sich die Zahlen. Nach den Aufnahmen ergänzt `Aufgenommen`,
was die Frames zeigen: neues Pink, verändert, unverändert. Nach der Migration ist die erste Gruppe `Ergebnis`: behoben,
zu prüfen, offen. Jede Zahl ist eine Schaltfläche: Wählen Sie sie, damit die Liste nur zeigt, was sie zählt, und wählen
Sie sie erneut, um alles zu zeigen. Fahren Sie mit der Maus über eine Zahl, um zu sehen, was sie zählt, und über
`Letzter Scan` für Dateianzahl und Dauer des Scans.

## Urteile einer Szene

Eine Szene hat zwei Urteile:

- **Statisch**, aus dem Scan: der schlimmste Defekt unter allem, was die Szene nutzt. Ein pinkes Objekt kann außerhalb jedes
  aufgenommenen Frames liegen, daher kann ein statisches Pink neben einem beobachteten „Gleich“ stehen.
- **Beobachtet**, aus den Aufnahmen vor und nach der Migration: der schlimmste Frame. Jeder Frame zeigt den Anteil
  veränderter Pixel und den Anteil pinker Pixel danach; fahr mit der Maus über die Zeile, um die mittlere Abweichung und
  den Anteil Pink vorher zu sehen. Ein größerer veränderter Bereich (eine verlorene Spiegelung, ein verlorenes Leuchten)
  macht einen Frame „Verändert“, auch wenn sich insgesamt wenige Pixel ändern. Fehler in der Console, während die Szene
  nach der Migration lief, machen sie ebenfalls „Verändert“, mit dem ersten Fehler angezeigt. Hat der Scan einen Defekt
  ohne Fehlermeldung erwartet, den die Migration nicht behoben hat (nur der Konverter von Unity, das kostenlose Paket bei
  einem Defekt, den nur Pro behebt, oder ein Defekt, den Urpify nicht konvertieren konnte), und sehen die Frames trotzdem
  gleich aus, sagt der Bereich das: Prüfe die Differenzkarte.

In der Vorher-nachher-Ansicht ist der Teil links der Linie vorher, der Teil rechts davon nachher. `Unterschiede zeigen`
markiert veränderte Pixel: je größer die Änderung, desto röter der Pixel.

## Wer es behebt

Die Übersicht zählt die defekten Assets danach, wer sie behebt:

- **Unitys Konverter und Urpify** – der Render Pipeline Converter von Unity, danach die Korrekturen, die Urpify ausführt
  (beides im kostenlosen Paket).
- **Urpify Pro** (im kostenlosen Paket: **Braucht Pro**, mit Schloss) – Shader, Skripte und Komponenten, die nur die
  Pro-Konverter umsetzen.
- **Von Hand oder noch nicht unterstützt** – das Projekt war vor der Migration kaputt, das Konstrukt hat kein Gegenstück in
  URP, oder Urpify konvertiert es noch nicht. Der Befund sagt, was zu tun ist.

Eine Listenzeile sagt, wer ihr Asset behebt: `Unity` (Unitys Konverter), `Urpify` (die kostenlosen Korrekturen), `Pro`
(Urpify Pro; im kostenlosen Paket ein Schloss und `Pro`), `Von Hand`. Der Tooltip sagt, wie nah das Ergebnis ist: Es
entspricht dem Original, kommt ihm nahe oder ist konvertiert, sieht aber anders aus (prüfen Sie es). Der Filter `Wer
behebt` wählt dieselben Gruppen. Nach der Migration sagt die Zeile stattdessen, was aus dem Asset wurde: `Behoben`,
`Prüfen` oder `Offen`. Vor dem Schritt Urpify-Konverter ist das die Erwartung des Scans, danach das, was der Schritt
getan hat. Der Bericht behält die eigenen Stufen der Konverter (`Auto`, `Angen.`, `Prüfen`, `Noch nicht`, `Manuell`).

Ein Shader, den Urpify konvertiert, wird durch einen Render geprüft: Vor der Konvertierung wird eine Kugel mit den
Materialien des Projekts für diesen Shader in Built-in gerendert, danach in URP, und beide werden verglichen. Weicht ein
großer Teil der Kugel ab, ist der Shader trotzdem konvertiert, heißt aber nicht `Auto`: Er ist `Prüfen`, und das Bildpaar
liegt in `Urpify/Reports/self-check/`. „Ein großer Teil“ ist mehr als 25 % der Kugel: Unitys eigene Konvertierung von
Standard zu URP Lit verändert dunkle, gesättigte Farben um bis zu etwa 20 %, und so sieht URP Lit aus, kein Fehler. Pink
oder ein Objekt, das verdeckt, was dahinter ist, und die Löschfarbe der Kamera zeigt, ist bei jeder Größe `Prüfen`.

Manche Shader lassen sich nicht auf einer Kugel beurteilen und sind höchstens `Angen.`, mit dem Grund im Bericht; prüfe sie
im Szenenvergleich:

- ein Bildschirmeffekt ohne Material (Skripte wenden ihn auf das ganze Bild an);
- ein interner Shader unter `Hidden/` (Skripte zeichnen ihn mit eigenen Daten);
- ein Shader, der die Zeit liest (`_Time`): Der Built-in-Render entsteht vor der Konvertierung und der URP-Render danach,
  daher zeigen sie verschiedene Momente der Animation (nur Pink oder ein dunkel werdender Render zählen).

Unter Unity 2022.3 wird ein Shader mit `GrabPass` mit einer versteckten Kamera vom Typ Game statt einer Vorschaukamera
gerendert: URP 14 gibt einer Vorschaukamera keine Opaque Texture. Eine solche Kamera führt auch die Renderer Features
deines URP Asset aus.

## Zeigen, was kaputtgeht

`Zeigen, was kaputtgeht` in der Übersicht und über den Schritten im Migrationsfenster zeigt, was URP allein mit dem
Projekt macht, bevor Sie etwas beheben: Es sichert das Projekt, nimmt die Szenen in Built-in auf, installiert URP, führt
Unitys Konverter aus, nimmt die Szenen erneut auf und öffnet das Ergebnis. Die Korrekturen von Urpify sind noch nicht
gelaufen. Wählen Sie dann `Mit Urpify beheben` (Urpify-Konverter, erneute Aufnahmen und Bericht) oder `Zurücksetzen`.
Im kostenlosen Paket sind die Korrekturen die kostenlosen, und das Ergebnis sagt, was Urpify Pro braucht.

## Schritte in der Reihenfolge

Ein Schritt, dessen vorherige Schritte nicht erledigt sind, läuft nicht und sagt unter seiner Beschreibung, warum:
Vorher aufnehmen braucht die Sicherung, Unitys Konverter ein installiertes URP, die Urpify-Konverter Unitys Konverter
(außer mit Keep Built-in support, dann bleibt das Projekt auf Built-in), und Nachher aufnehmen braucht die Bilder von
vorher und ein Projekt, das mit URP rendert. Zwei Aufnahmen aus derselben Pipeline werden nie verglichen: Das Ergebnis
sagt, dass es nichts zu vergleichen gibt.

## Das Ergebnis nach der Migration

Sobald Unitys Konverter gelaufen ist, zeigt die rechte Seite statt der Übersicht das Ergebnis: eine Zeile mit dem
Wichtigsten (zum Beispiel `Fertig. Ihre Szenen sehen aus wie vor der Umstellung`), die am stärksten veränderte Szene
vorher und nachher, dann nur, was Sie braucht, jeweils mit einer Schaltfläche, und die behobenen Assets eingeklappt unter
`Automatisch behoben`. Das Fenster scannt das Projekt nach jedem Schritt erneut, damit die Liste zeigt, was behoben und
was offen ist. Dateien, die Urpify dem Projekt hinzufügt (`Assets/Urpify/`, der Ordner `UrpCompat` eines Pakets,
Skripte `UrpSettingsCheck_*`), erscheinen nie in der Liste.

## Szenen vergleichen

Jede aufgenommene Szene mit ihrem Ergebnis in Worten. Wählen Sie eine Szene, um ihre Bilder direkt dort mit dem Schieber
zu vergleichen; `Szene öffnen` öffnet sie. Bei einer einzigen Szene ist der Vergleich von Anfang an offen. Das Fenster
zeigt immer den letzten Vergleich, auch nach einem späteren Scan. Die Bilder werden so gespeichert, wie der Bildschirm sie
zeigt, ohne Transparenz, in der Größe der Game-Ansicht, sofern `Aufnahmegröße` in den Einstellungen nichts anderes sagt.

## Lizenzmarkierung

Ein Shader, dessen Kopf eine Lizenz nennt (ein SPDX-Bezeichner wie `MIT` oder `CC-BY-NC-4.0`), zeigt sie als Markierung
neben seinem Namen. Eine konvertierte Kopie ist ein abgeleitetes Werk und bleibt unter derselben Lizenz. Eine
nichtkommerzielle Lizenz (`-NC`) hat eine bernsteinfarbene Markierung: Eine konvertierte Kopie eines solchen Shaders darf
nicht verkauft werden.

## Umgebungslicht (Szenenbereich)

Unter Unity 6 rendert ein Player eine Szene, die ihr Umgebungslicht von der Skybox nimmt und keine gebackenen Lichtdaten
hat, ohne dieses Licht, in Built-in wie in URP, daher sieht die Szene dunkler aus als im Editor. Die Migration verursacht
das nicht, daher ist der Befund informativ (ein Ring-Symbol). (Unter Unity 2022.3 bekommt der Player das Umgebungslicht
auch ohne gebackene Daten, und Urpify meldet diese Szenen nicht.) `Umgebung backen` backt nur das Umgebungslicht und die
Reflexionen der Skybox in die Lichtdaten der Szene (Lightmaps werden nicht gebacken, und Auto Generate wird für die Szene
ausgeschaltet), damit der Player zum Editor passt. Das Projekt wird vorher gesichert; `Backen zurücksetzen` an derselben
Stelle oder das Zurücksetzen im Migrationsfenster macht es rückgängig.
