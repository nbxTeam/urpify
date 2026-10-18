# Eigene Beleuchtungsfunktionen

![Stand S05: eine Toon-Ramp-Beleuchtungsfunktion, pink nach dem Konverter von Unity, nach Urpify gleich](../../images/S05.jpg)

*Stand S05: ein Surface-Shader mit eigener Beleuchtungsfunktion, einer Toon-Ramp.*

## Was kaputtgeht

Ein Surface-Shader kann sein eigenes Beleuchtungsmodell nennen, `#pragma surface surf Toon`, und `LightingToon`
definieren. Built-in ruft sie einmal pro Licht auf: in ForwardBase für das Hauptlicht und in ForwardAdd für jedes
zusätzliche Licht, mit `_LightColor0` und der Abschwächung des Lichts. URP hat keine Surface-Shader, daher ist das Objekt
pink.

Befund: „Eigene Beleuchtungsfunktion ...“ (`SHADER.SURFACE.CUSTOM_LIGHTING`).

## Was Urpify tut (Pro)

Die Funktion des Autors wird unverändert übernommen. Der erzeugte Forward-Pass ruft sie für das Hauptlicht von URP auf,
dann für jedes zusätzliche Licht, mit den Schatten und der Abschwächung von URP, und addiert die Ergebnisse, wie es die
zwei Passes von Built-in taten. In der Funktion ist `_LightColor0` die Farbe des Lichts, für das sie aufgerufen wird.

Alle Signaturen von Built-in werden erkannt:

| Signatur | Beispiel |
|---|---|
| Ohne Blickrichtung | `half4 LightingToon(SurfaceOutput s, half3 lightDir, half atten)` |
| Mit Blickrichtung | `half4 LightingToon(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)` |
| Mit globaler Beleuchtung | `half4 LightingToon(SurfaceOutput s, half3 viewDir, UnityGI gi)` und ihr `LightingToon_GI` |
| Deferred- und Prepass-Varianten | `_Deferred`, `_PrePass`: nicht verwendet, URP rendert forward |

Die Funktion wird überall gefunden, wo der Compiler sie finden würde: im Programm, in `CGINCLUDE`-Blöcken, in
Include-Dateien des Projekts in beliebiger Tiefe und in `#define`-Makros, die Funktionen deklarieren.

## Wie nah

`Auto, angenähert`. Built-in addiert jedes zusätzliche Licht in einem eigenen Pass mit additivem Blending; URP beleuchtet
sie in einem Pass. Für undurchsichtige Objekte sind die Summen gleich; transparente Shader können dort abweichen, wo sich
Lichter überlappen. Umgebungslicht kommt aus den sphärischen Harmonischen von URP, wie in Built-in.

Ist die Funktion weder im Shader noch in den gescannten Include-Dateien, nennt der Befund sie, und der Shader wird nicht
konvertiert, bis ihre Include-Datei im Scanbereich ist.
