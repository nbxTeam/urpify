# Urpify

Urpify überführt ein Unity-Projekt, einen Shader oder ein Asset-Store-Paket von der Built-in Render Pipeline nach URP.
Es zeigt alles, was der Wechsel kaputt macht, bevor sich irgendetwas ändert, auch das, was ohne Fehlermeldung kaputtgeht.
Dann konvertiert es, was es kann, sichert jede Datei, die es anfasst, und vergleicht jede Szene vorher und nachher anhand
aufgenommener Frames. Jeder Schritt lässt sich zurücksetzen.

Urpify gibt es in zwei Paketen:

| | Urpify (kostenlos, MIT) | Urpify Pro |
|---|---|---|
| Projekt, Ordner, Datei oder `.unitypackage` scannen | Ja | Ja |
| Szenen vorher und nachher anhand aufgenommener Frames vergleichen | Ja | Ja |
| Render Pipeline Converter von Unity zuverlässig ausführen, aus dem Fenster oder der Kommandozeile | Ja | Ja |
| Korrekturen nach dem Konverter von Unity (Glas, Self-Illumin, eingebaute Materialien, Bloom aus Post-processing v2, Einstellungen des URP Asset, Paketmaterialien) | Ja | Ja |
| Sicherung und Zurücksetzen | Ja | Ja |
| Shader-Bereich: Code, Vorschau in Built-in und URP im aktuellen Zustand | Ja | Ja |
| Eigene Shader, Bildeffekte, Kamera-Skripte, Command Buffer und Projektoren konvertieren | Nein | Ja |
| Ergebnis: eine Shader-Datei für beide Pipelines (Built-in-Unterstützung behalten) | Nein | Ja |
| Konvertiertes `.unitypackage` exportieren | Nur Bericht | Ja |
| Kommandozeile | Scan, Aufnahme, Vergleich, Konverter von Unity und die kostenlosen Korrekturen | Alles |

## Inhalt

Hier anfangen:
- [Schnellstart](quick-start.md): der ganze Ablauf in 5 Minuten, mit der Demo-Szene oder deinem Projekt.
- [Ein Spiel migrieren](scenario-game.md)
- [Einen Shader konvertieren](scenario-shader.md)
- [Ein Asset-Store-Paket für beide Pipelines konvertieren](scenario-asset.md)

Wie Urpify jedes Konstrukt der Built-in Render Pipeline behandelt:
- [Konstrukte: Übersicht](constructs/index.md), eine Seite pro Konstrukt mit Bildern vorher und nachher.
- [Was nicht konvertiert wird und warum](not-converted.md)
- [Wie URP anders aussieht](urp-differences.md)

Referenz:
- [Das Urpify-Fenster](window.md): Symbole, Zähler, Urteile.
- [Einstellungen](settings.md)
- [Kommandozeile](cli.md)
- [Berichtsschema](../report-schema.md) (Englisch)
- [Unity- und URP-Versionen](urp-version-matrix.md)
- [FAQ](faq.md)
- [Weitergabe konvertierter Shader](../license-redistribution.md) (Englisch)

## Voraussetzungen

- Unity 2022.3 LTS (URP 14) oder Unity 6000.0 LTS, 6000.3 LTS, 6000.5 oder 6000.6 (URP 17), die getesteten Versionen. Siehe [Unity- und URP-Versionen](urp-version-matrix.md).
- Ein Projekt mit der Built-in Render Pipeline oder ein `.unitypackage`, das dafür erstellt wurde. Urpify installiert URP
  selbst, wenn die Migration es braucht.
- Keine weiteren Pakete. Urpify sendet nichts über das Netzwerk, außer dem Shader-Archiv, das du mit `Bericht senden`
  ausdrücklich sendest (siehe [FAQ](faq.md)).

## Sprachen

Das Fenster spricht English, 简体中文, 日本語, 한국어, Русский, Español, Português (Brasil) und Deutsch. Standardmäßig folgt
es der Sprache des Editors (Preferences > Languages) und nutzt Englisch, wenn Urpify diese Sprache nicht hat. Ändere
sie unter Project Settings > Urpify > Persönlich > Sprache. Hinweise der Konverter in Berichten, die Kommandozeile und die
technischen Details des HTML-Berichts bleiben auf Englisch.
