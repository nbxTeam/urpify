# Kommandozeile

Alles, was das Fenster tut, läuft auch im Batch-Modus, sodass ein Build-Server einen Branch scannen, konvertieren und den
Build fehlschlagen lassen kann, wenn eine Szene pink wird. Das kostenlose Paket führt Scan, Capture, Verify, Rollback, alle
Migrate-Schritte (sein Schritt „Urpify-Konverter“ führt die kostenlosen Korrekturen aus) und die Paketanalyse aus. Für die
Konvertierung von Shadern, Skripten und Komponenten und für den Export eines konvertierten Pakets braucht es Pro.

```
Unity -batchmode -projectPath <Projekt> -executeMethod Urpify.Cli.Commands.<Befehl> [Argumente]
```

**Übergib kein `-quit`.** Jeder Befehl beendet den Editor selbst mit seinem Exit-Code, nachdem er ihn in eine Datei
geschrieben hat: Manche alten Projekte bringen Unity beim Beenden zum Absturz, nachdem die Arbeit erledigt ist, daher
sollte CI die Ergebnisdatei lesen, statt sich nur auf den Exit-Code des Prozesses zu verlassen.

| Exit-Code | Bedeutung |
|---|---|
| 0 | Erledigt. |
| 1 | Fehlgeschlagen: falsches Argument, fehlende Datei, fehlgeschlagener Schritt. Der Grund steht im JSON-Ergebnis. |
| 2 | Erledigt, und Befunde auf oder über `-urpifyFailOn` wurden gefunden. |

Jeder Befehl schreibt bei Erfolg und bei jedem Fehler ein JSON-Ergebnis (`command`, `step`, `status`, `exitCode`,
`error`, `reports`) und ein Textergebnis: den Exit-Code, bei einem Migrate-Schritt den Schritt und seinen Status, etwa
`KitConverters Done` (`Done`, `Skipped`, `Failed`, `TimedOut`). Die Dateien:

| Befehl | JSON-Ergebnis (`Urpify/Reports/`) | Textergebnis |
|---|---|---|
| Scan | `urpify-scan-result.json` | `urpify-scan-result.txt` (beide in `-urpifyOutput`, Standard `Urpify/Reports`) |
| Migrate | `urpify-step-result.json` | `Urpify/urpify-step-result.txt` |
| Capture | | `Urpify/Captures/<label>/urpify-capture-result.txt` |
| Verify | `urpify-verify-result.json` | `Urpify/urpify-verify-result.txt` |
| Rollback | `urpify-rollback-result.json` | `Urpify/urpify-rollback-result.txt` |
| Package | `urpify-package-result.json` | `Urpify/urpify-package-result.txt` |
| BakeEnvironment | `urpify-bake-result.json` | `Urpify/urpify-bake-result.txt` |
| Capture, Migrate oder Package mit falschem Argument | `urpify-command-result.json` | `Urpify/urpify-command-result.txt` |

## Befehle

### Scan

```
-executeMethod Urpify.Cli.Commands.Scan
    [-urpifyOutput <Ordner>]               Standard Urpify/Reports
    [-urpifyScope <Pfad>[;<Pfad>...]]      Standard Assets
    [-urpifyFormat json,html]              Standard beide
    [-urpifyFailOn pink|silent|degraded]
    [-urpifyNoCache]
```

Nur lesend. Schreibt den JSON-Bericht, beschrieben im [Berichtsschema](../report-schema.md) (Englisch), und einen
eigenständigen HTML-Bericht. `urp-migration-report-latest.json` wird immer geschrieben, neben den Dateien mit Zeitstempel.
Der Scan speichert Ergebnisse pro Datei in `Library/Urpify/cache`; `-urpifyNoCache` umgeht den Cache.

### Capture und Verify

```
-executeMethod Urpify.Cli.Commands.Capture -urpifyLabel <Label>
    [-urpifyScenes build|all|<a.unity>;<b.unity>]   Standard: Project Settings > Urpify > Vergleich > Szenen
    [-urpifyWidth 960 -urpifyHeight 540 -urpifyFrames 30]
-executeMethod Urpify.Cli.Commands.Verify [-urpifyBefore before] [-urpifyAfter after] [-urpifyFailOn pink|changed]
```

Capture spielt jede Szene im Play Mode ab und speichert Frames der Hauptkamera und jedes Migration Viewpoint unter
`Urpify/Captures/<Label>/`. Verify vergleicht zwei Labels und schreibt einen Bericht mit dem beobachteten Urteil pro Szene.

### Migrate

```
-executeMethod Urpify.Cli.Commands.Migrate -urpifyStep <Schritt>
    [-urpifyShaderOutput replace|write-copy|keep-builtin]       kit-converters; Standard: die Projekteinstellung
    [-urpifyGrabPass opaque-texture|after-transparents]          kit-converters; Standard: die Projekteinstellung
    [-urpifyScenes ...] [-urpifyWidth ...] [-urpifyHeight ...] [-urpifyFrames ...]   Aufnahmeschritte
    [-urpifyStepTimeout 20]                                      Minuten ohne Fortschritt bis zum Abbruch; 0 = nie
```

Schritte in Reihenfolge, einer pro Editor-Prozess: `scan`, `backup`, `capture-before`, `install-urp`, `unity-converter`,
`kit-converters`, `capture-after`, `review`. Install URP fügt ein Paket hinzu, das der nächste Prozess kompiliert, daher
läuft jeder Schritt in einem eigenen Prozess. Backup läuft vor Capture before, und Capture before verweigert den Lauf
ohne Sicherung: Beim Abspielen von Szenen speichert Unity alte Assets neu und Pakete legen Dateien an, und die Sicherung
muss das Projekt so festhalten, wie es war.

Die Asset-Store-Version installiert kein Paket: Installieren Sie URP vor `install-urp` im Package Manager, der Schritt prüft es dann nur. Ein Projekt, dessen Unity-Konverter sein Fenster braucht (Post-processing v2 in Gebrauch, Unity 6000.3), startet `unity-converter` im Urpify-Fenster.

Schritte, die Dateien ändern, warnen im Log, wenn das Git-Arbeitsverzeichnis nicht committete Änderungen hat.

### Rollback

```
-executeMethod Urpify.Cli.Commands.Rollback [-urpifyToStep backup]
```

Ohne `-urpifyToStep` werden alle Schritte zurückgesetzt. Dateien, die nach der Migration geändert wurden, und Dateien, die
zwischen den Schritten entstanden sind, bleiben, wie sie sind, und werden im Log aufgelistet.

### Package

```
-executeMethod Urpify.Cli.Commands.Package -urpifyPackage <Datei.unitypackage> [-urpifyOutput <Ordner>]
    [-urpifyExport <konvertiert.unitypackage>] [-urpifyShaderOutput replace|keep-builtin] [-urpifyKeepScope]
```

Importiert das Paket in einen temporären Ordner, scannt es und schreibt mit `-urpifyExport` (Pro) das konvertierte Paket,
wie unter [Ein Asset-Store-Paket konvertieren](scenario-asset.md) beschrieben. Der Analysebericht wird immer geschrieben,
auch wenn der Export fehlschlägt oder verweigert wird; der Exit-Code ist dann 1. Der Export kompiliert die konvertierten
Shader für URP, daher muss URP im Projekt installiert sein. `-urpifyKeepScope` behält den temporären Ordner zur Ansicht.

### Bake environment

```
-executeMethod Urpify.Cli.Commands.BakeEnvironment -urpifyScene <Assets/a.unity> [-urpifyUndo]
```

Backt Umgebungslicht und Reflexionen der Skybox in eine Szene, der sie fehlen, mit Sicherung; `-urpifyUndo` setzt das
Backen zurück. Siehe den Befund „Umgebungslicht der Skybox nicht gebacken“.

## Unterbrochene Schritte und Zeitlimits

Ein Schritt, der mittendrin stoppt (der Editor wurde beendet oder ist abgestürzt, der Schritt schlug fehl), behält eine
vollständige Aufzeichnung dessen, was er geändert hat: Der nächste Editor-Start schließt ihn als unterbrochen ab, und
seine Änderungen sind im Rollback enthalten. Während ein Schritt läuft, sagt `Urpify/progress.json`, was er zuletzt getan
hat (`Step`, `What`, `Done`, `Total`, `UpdatedUtc`). Im Batch-Modus wird ein Schritt, der `-urpifyStepTimeout` Minuten
keinen Fortschritt macht, wie ein beendeter Editor gestoppt, mit dem Grund in `progress.json` und in der Meldung des
Schritts. Fortschritt ist, was der Schritt meldet, und die Dateien, die er schreibt, nicht das Unity-Log. Ein Editor mit
Fenster wird nie gestoppt.

## Projekte, deren Skripte nicht kompilieren

Unity bricht einen Batch-Lauf ab, bevor irgendein Befehl startet („Scripts have compiler errors“). Füge
`-ignorecompilererrors` hinzu: Der Scan meldet die Kompilierfehler dann selbst (`PROJECT.SCRIPT_COMPILATION_FAILED`), und
Schritte, die Skripte ändern, lassen sie in Ruhe.

## Beispiel: Build fehlschlagen lassen, wenn eine Szene pink wird

```
U="/pfad/zu/Unity"; P=/pfad/zum/projekt
for step in scan backup capture-before install-urp unity-converter kit-converters capture-after review; do
  "$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Migrate -urpifyStep $step -logFile -
  grep -q " Done\| Skipped" "$P/Urpify/urpify-step-result.txt" || { echo "Schritt $step fehlgeschlagen"; exit 1; }
done
"$U" -batchmode -projectPath "$P" -executeMethod Urpify.Cli.Commands.Verify -urpifyFailOn pink -logFile -
exit $(cat "$P/Urpify/urpify-verify-result.txt")
```

## Dateien, die die Schritte schreiben

- `Urpify/state.json`: der Zustand der Migration, außerhalb von `Assets`.
- `Urpify/Backup/<id>/`: eine Sicherung pro Schritt, der Dateien ändert, mit einem Manifest des Hashes jeder Datei vorher
  und nachher.
- `Urpify/Captures/<Label>/`: die aufgenommenen Frames und `captures.json`.
- `Urpify/Reports/`: Scan-, Vergleichs- und Auswertungsberichte; `kit-converters.json`, der eigene Bericht des Schritts
  „Urpify-Konverter“ (pro Datei: `Path`, `Converter`, `Outcome`, `Notes`; Änderungen am URP Asset; URP-Einstellungen, die
  die konvertierten Dateien brauchen); `self-check/`, die Renders jedes konvertierten Shaders in Built-in und URP.

Nimm `Urpify/` in die Versionsverwaltung auf, wenn du den Verlauf der Migration behalten willst; der Build liest es nicht.
