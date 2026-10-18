# Wie URP anders aussieht

Manche Unterschiede nach einer Migration sind weder Fehler noch Konvertierungsfehler: URP zeichnet dasselbe Material
einfach etwas anders als Built-in. Der Szenenvergleich markiert sie als „Verändert“, wenn sie groß genug sind, daher
lohnt es sich, sie zu kennen.

## Standard zu URP Lit

![Stand STD01: Standard und URP Lit nebeneinander](../images/STD01.jpg)

Unitys eigene Konvertierung von Standard zu URP Lit verändert Glanzlichter und Reflexionen leicht: URP hat ein anderes
Reflexionsmodell und andere Umgebungsreflexionen. In unseren Testszenen weicht es um bis zu etwa 7 % des Frames ab. Urpifys
Schwellen für „Gleich“ und „Verändert“ sind genau an dieser Konvertierung kalibriert, daher macht sie allein eine Szene
nicht „verändert“.

## Zusätzliche Lichter

Built-in zeichnet jedes zusätzliche Licht in einem eigenen additiven Pass, URP fasst sie in einem Pass zusammen, mit
eigener Abschwächungskurve und einer Grenze von Lichtern pro Objekt (im URP Asset einstellbar). Szenen mit vielen
Punktlichtern sehen etwas anders aus, und Lichter über der Grenze pro Objekt lässt URP weg.

## Bloom

Der Bloom von URP verteilt kleine helle Stellen breiter und weicher als der Post-processing Stack v2. Urpify überträgt die
Bloom-Parameter mit einer Kalibrierung, die das Leuchten annähert, aber nicht identisch macht; der Rest ist die andere
Bloom-Pyramide, die kein Parameter nachbildet.

## Umgebungslicht im Player unter Unity 6

Eine Szene, die ihr Umgebungslicht von der Skybox nimmt, aber keine gebackenen Lichtdaten hat, sieht im Player unter
Unity 6 dunkler aus als im Editor, in Built-in wie in URP. Das kommt nicht von der Migration, fällt aber auf, wenn du den
migrierten Player mit dem Editor vergleichst. Der Scan meldet es („Umgebungslicht der Skybox nicht gebacken“), und die
Schaltfläche **Umgebung backen** im Szenenbereich backt nur das Umgebungslicht und die Reflexionen, mit Sicherung.

## GrabPass mit Opaque Texture

Mit der Standard-GrabPass-Quelle fehlen transparente Objekte in der Brechung: URP kopiert den Bildschirm, bevor es sie
zeichnet. Wähle die GrabPass-Quelle „Nach Transparenten“, damit es wie in Built-in aussieht. Siehe
[GrabPass](constructs/grabpass.md).

## Transparente Objekte und Sortierung

URP sortiert transparente Objekte genauso, zeichnet aber die zusätzlichen Passes konvertierter Shader (den dritten und
weitere) nach den ersten beiden Passes aller Objekte, was die Reihenfolge überlappender transparenter Objekte ändern
kann. Siehe [Zusätzliche Passes](constructs/extra-passes.md).
