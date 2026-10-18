# Schnellstart

Der ganze Ablauf in 5 Minuten: scannen, migrieren, vergleichen. Probier es zuerst mit der Demo-Szene, dann mit
einer Kopie deines Projekts.

## 1. Installieren

Installiere Urpify und, falls vorhanden, Urpify Pro in ein Projekt, das noch die Built-in Render Pipeline nutzt:

- **Package Manager:** `+` > Add package from disk, wähle `package.json` von `com.urpify.core`, dann von
  `com.urpify.pro`.
- **`.unitypackage`:** Assets > Import Package > Custom Package, importiere Urpify, dann Urpify Pro.

Committe dein Projekt in die Versionsverwaltung, bevor du migrierst. Urpify sichert jede Datei, die es ändert, aber
ein Commit ist der sicherste Wiederherstellungspunkt.

## 2. Demo öffnen (optional)

Help > Urpify > **Import Demo** oder **Demo importieren** im leeren Urpify-Fenster. Die Demo kommt nach `Assets/Samples/Urpify/1.0.0/Demo`,
und ihre Szene öffnet sich. Mit dem Package Manager installiert, macht Package Manager > Urpify > Samples > **Demo** > Import dasselbe. Die Szene hat
einen Stand pro Konstrukt, das in URP kaputtgeht: Surface-Shader, ein beleuchteter Vert/Frag-Shader, Glas mit GrabPass,
Wasser, das die Tiefe liest, ein Projektor, ein Spiegel, den ein Skript rendert, ein im Code erzeugtes Material und Glas im
Modus Standard Transparent. Jeder Stand hat einen Migration Viewpoint, sodass der Vergleich jeden aus der Nähe zeigt.

## 3. Scannen

Window > Rendering > **Urpify**, dann **Projekt scannen**.

Der Scan liest nur Dateien. Die Liste links zeigt jeden Shader, jedes Skript, Material, jede Szene und Einstellung, die in
URP kaputtgeht, nach Typ gruppiert. Jede Zeile hat ein Symbol:

- gefülltes Quadrat: wird pink;
- gefülltes Dreieck: bricht ohne Fehlermeldung (ein Portal wird zur Wand, Wasser verliert seinen Schaum, ein Effekt
  verschwindet);
- gefüllte Raute: sieht anders aus;
- Ring: keine sichtbare Änderung.

Wähle eine Zeile, um zu sehen, was kaputtgeht, warum und wer es behebt. Ein Shader öffnet sich im Shader-Bereich: sein
Code vor und nach der Konvertierung und eine Vorschau in Built-in und URP nebeneinander.

## 4. Migrieren

Wähle **Migrieren...**. Das Fenster listet die Schritte:

1. **Scannen**: derselbe Scan, nur lesend.
2. **Sichern**: kopiert Assets, Einstellungen und Paketliste des Projekts nach `Urpify/Backup`.
3. **Vorher aufnehmen**: spielt jede Szene im Play Mode ab und speichert Frames der Hauptkamera und jedes Migration
   Viewpoint.
4. **URP installieren**: fügt die URP-Version hinzu, die zu deinem Editor gehört.
5. **Unitys Konverter ausführen**: der Render Pipeline Converter von Unity, mit den Umgehungen, die Urpify für einen
   zuverlässigen Lauf braucht.
6. **Urpify-Konverter**: behebt, was der Konverter von Unity kaputt lässt. Mit Pro werden außerdem Shader, Skripte und
   Komponenten konvertiert.
7. **Nachher aufnehmen und vergleichen**: dieselben Frames in URP, verglichen mit den Frames von vorher.
8. **Auswertung**: der Abschlussbericht.

Wähle **Alle ausführen**. Jeder Schritt lässt sich auch einzeln ausführen und zurücksetzen: **Bis hierher
zurücksetzen** an einem Schritt oder **Alle zurücksetzen**.

## 5. Vergleichen

Wähle **Szenen vergleichen**. Jede Szene zeigt ihre Frames vorher und nachher mit einem Schieberegler dazwischen.
**Unterschiede zeigen** markiert veränderte Pixel: je röter, desto größer die Änderung. **Nur veränderte** blendet die
Szenen aus, die gleich aussehen.

Im Hauptfenster hat eine Szene zwei Urteile: **Statisch** aus dem Scan und **Beobachtet** aus den Frames. Ein pinkes
Objekt kann außerhalb jedes aufgenommenen Frames liegen, daher kann das statische Urteil schlechter sein als das
beobachtete. Füge einen Migration Viewpoint (ein leeres GameObject mit der Komponente `MigrationViewpoint`) dort
hinzu, wo du genauer hinsehen möchtest, und nimm erneut auf.

## 6. Fertig oder zurücksetzen

Öffne **Bericht** > HTML exportieren für einen Bericht, den du deinem Team schicken kannst. Gefällt dir das
Ergebnis nicht, stellt **Alle zurücksetzen** jede Datei wieder her, die die Migration geändert hat, und verschiebt die
Dateien, die sie erstellt hat, in die Sicherung. Dateien, die zwischen den Schritten entstanden sind, werden nie
angefasst, deine eigene Arbeit bleibt also erhalten.
Nach einem Rollback im Fenster startet Unity sofort neu und lädt das Projekt so, wie es auf der Festplatte ist.

Weiter: [Ein Spiel migrieren](scenario-game.md), [Einen Shader konvertieren](scenario-shader.md),
[Ein Asset-Store-Paket konvertieren](scenario-asset.md).
