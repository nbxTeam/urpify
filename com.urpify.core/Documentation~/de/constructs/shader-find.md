# Shader.Find mit Namen eingebauter Shader

![Stand C04: ein Material, das im Code mit Shader.Find("Standard") erzeugt wird, pink nach dem Konverter von Unity, von Urpify behoben](../../images/C04.jpg)

*Stand C04: Ein Skript erzeugt zur Laufzeit ein Material mit `new Material(Shader.Find("Standard"))` und setzt `_Color`.*

## Was kaputtgeht

Der Konverter von Unity stellt Materialdateien um, keinen Code. Ein Skript, das ein Material mit `Shader.Find("Standard")`,
`"Diffuse"`, `"Legacy Shaders/..."` oder einem anderen Namen eines eingebauten Shaders erzeugt, bekommt einen Shader, den
URP nicht zeichnen kann, und das Objekt ist pink. Die Eigenschaftsnamen, die das Skript setzt (`_MainTex`, `_Color`,
`_Glossiness`), sind in URP Lit versteckte Eigenschaften, daher ändern diese Aufrufe nichts, selbst wenn der Shader von
Hand getauscht wird.

Befunde: `Shader.Find("...")` (`SCRIPT.SHADER_FIND_BUILTIN`), „Built-in-Eigenschaftsnamen im Code“
(`SCRIPT.BUILTIN_PROPERTY_NAMES`).

## Was Urpify tut (Free)

Das ist eine der Korrekturen nach dem Konverter von Unity, im kostenlosen Paket. Im selben Skript:

- wird der Shadername zu dem URP-Shader, den der Konverter von Unity für diesen eingebauten Shader wählt (`Standard` zu
  `Universal Render Pipeline/Lit`), oder zum nächstliegenden URP-Shader aus Urpifys Tabelle für Shader ohne Unity-Regel;
- werden die Eigenschaftsnamen, die dieselbe Methode setzt, zu den URP-Namen (`_MainTex` zu `_BaseMap`, `_Color` zu
  `_BaseColor`).

Mit der Ausgabe „Built-in-Unterstützung behalten“ wird jeder Name zur Laufzeit nach der aktiven Pipeline gewählt, daher
funktioniert das Skript auch in Built-in weiter.

Das umgeschriebene Skript wird vor dem Speichern kompiliert; kompiliert es nicht, wird das Original zurückgestellt und
gemeldet.

## Wie nah

`Auto`.
