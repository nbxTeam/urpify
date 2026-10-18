# Tessellation

![Stand S09: Tessellation mit Verschiebung](../../images/S09.jpg)

*Stand S09: ein Surface-Shader mit `tessellate:` und Verschiebung.*

## Was kaputtgeht

`tessellate:func` lässt Unity Hull- und Domain-Stufen für einen Surface-Shader erzeugen. URP hat keine Surface-Shader,
daher ist das Objekt pink.

Befund: „Tessellation (...)“ (`SHADER.SURFACE.TESSELLATION`).

## Was Urpify tut (Pro)

Der Konverter erzeugt die Hull- und Domain-Stufen nach einer Vorlage, die der Built-in-Pipeline folgt: Die
Tessellationsfunktion des Autors liefert die Faktoren, Unterteilung `fractional_odd`, dann verschiebt die
`vertex:`-Funktion die neuen Vertices. Jeder Pass, der das Objekt zeichnet (Forward, Schatten, Tiefe), wird tesselliert,
daher folgt der Schatten der verschobenen Oberfläche. `tessphong:` fügt Phong-Glättung mit der angegebenen Stärke hinzu.

Vert/Frag-Shader, die ihre eigenen Hull- und Domain-Stufen schreiben, behalten sie; die Attribute, die Built-in definiert
(`UNITY_domain`, `UNITY_partitioning`, `UNITY_outputtopology`, `UNITY_patchconstantfunc`, `UNITY_outputcontrolpoints`),
kommen aus Urpifys Bibliothek, und die Kompilierprüfung umfasst die Hull- und Domain-Stufen.

## Wie nah

`Auto, angenähert`: dieselben Faktoren und dieselbe Verschiebung, darüber die URP-Beleuchtung.
