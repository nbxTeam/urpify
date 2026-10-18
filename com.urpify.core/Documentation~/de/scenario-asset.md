# Ein Asset-Store-Paket für beide Pipelines konvertieren

Du veröffentlichst Shader oder Effekte im Asset Store und möchtest ein Paket, das für Käufer mit Built-in und mit URP
funktioniert. Die Hauptaktion ist **Für Built-in und URP exportieren** (Pro).

## Was du brauchst

- Ein Projekt mit installiertem URP. Die konvertierten Shader werden für URP kompiliert, bevor sie geschrieben werden,
  daher verweigert der Export den Lauf ohne URP. Dein Paket selbst kann ein Built-in-Paket bleiben; das Projekt dient nur
  als Gastgeber für den Export.
- Das `.unitypackage`, das du heute auslieferst.

## Das Paket analysieren

Im Urpify-Fenster: **Ändern** (Bereich) > **Unity-Paket (.unitypackage)...** und wähle deine Datei. Urpify importiert
sie in einen temporären Ordner, scannt sie und entfernt den Ordner, wenn du das Fenster schließt oder den Bereich
wechselst. Dein Projekt bleibt, wie es war.

Die Übersicht zeigt, was für einen Käufer mit URP kaputtgeht, genauso wie bei einem Projekt. Das kostenlose Paket hört
hier auf: Der Bericht (Bericht > HTML exportieren) ist das, was du zeigen oder anhängen kannst.

## Exportieren

In der Übersicht **Für Built-in und URP exportieren...** oder Bericht > **Konvertiertes Paket exportieren...**. Wähle
den Speicherort. Das neue `.unitypackage` behält jede GUID und jeden Pfad des Originals und enthält:

- die konvertierten Shader. Mit der Ausgabe **Built-in-Unterstützung behalten** (Project Settings > Urpify >
  Konvertierung > Ausgabe) behält jede Datei ihre ursprünglichen Built-in-SubShader unverändert und bekommt einen
  URP-SubShader davor, sodass eine Datei in beiden Pipelines funktioniert; Unity wählt den SubShader, der zur aktiven
  Pipeline passt;
- Materialien, die eingebaute Shader von Unity verwendeten, so umgestellt, wie es der Konverter von Unity im Projekt des
  Käufers täte, mit Urpifys Korrekturen danach;
- Skripte, konvertiert wie in einem Projekt, jedes gegen das Projekt kompiliert, bevor es geschrieben wird. Ein Skript,
  dessen Umschreibung nicht kompiliert, bleibt, wie es war, und wird aufgelistet;
- Urpifys Shader-Bibliothek und ihr kleiner Laufzeitteil, in einem Ordner `UrpCompat` in jedem Wurzelordner deines Pakets,
  mit neuen GUIDs bei jedem Export und der Laufzeit im Namespace deines Pakets. Zwei Pakete verschiedener Autoren teilen
  oder überschreiben nie die Kopie des anderen;
- eine kleine Editor-Prüfung, benannt nach deinem Paket. Nutzt das Projekt des Käufers URP, prüft sie die URP Assets auf
  das, was deine Shader brauchen (Depth Texture, Opaque Texture, das Renderer Feature Built-in Camera Hooks), und fragt
  einmal, bevor sie etwas einschaltet. In Built-in tut sie nichts.

Neben dem Paket wird ein Kompatibilitätsbericht gespeichert: jeder Shader mit seinem Ergebnis und die Einstellungen des
URP Asset, die Käufer brauchen.

## Wie ein Käufer prüfen

Importiere das exportierte Paket in ein sauberes Built-in-Projekt und in ein sauberes URP-Projekt. In Built-in rendert
die Ausgabe „Built-in-Unterstützung behalten“ genau wie das Original: Die ursprünglichen SubShader werden nicht angefasst.
In URP öffnest du die Demo-Szene des Pakets und vergleichst sie mit deinen Built-in-Screenshots, oder du lässt den
Szenenvergleich der Migration darauf laufen.

## Weitergabe

Du darfst die konvertierten Shader und den Ordner `UrpCompat` in deinen kostenpflichtigen Paketen ausliefern. Siehe
[Weitergabe konvertierter Shader](../license-redistribution.md) (Englisch).
