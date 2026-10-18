# Unity- und URP-Versionen

| Unity | URP | Status | Geprüft mit |
|---|---|---|---|
| 6000.6 | 17.6 | Unterstützt | 6000.6.5f1 mit URP 17.6.0: Unit-Tests, 37 Stände, die Demo, 4 echte Projekte |
| 6000.5 | 17.5 | Unterstützt | 6000.5.9f1 mit URP 17.5.0: Unit-Tests, 37 Stände, echte Projekte, Shader-Korpus |
| 6000.4 | 17.4 | Nicht getestet | |
| 6000.3 LTS | 17.3 | Unterstützt | 6000.3.26f1 mit URP 17.3.0: Unit-Tests, 37 Stände, die Demo, 4 echte Projekte. Unitys eigener Batch-Konverter scheitert auf dieser Version; Urpify umgeht das (unten) |
| 6000.1, 6000.2 | 17.1, 17.2 | Nicht getestet | |
| 6000.0 LTS | 17.0 | Unterstützt | 6000.0.84f1 mit URP 17.0.4: Unit-Tests, 37 Stände, die Demo, 4 echte Projekte |
| 2022.3 LTS | 14 | Unterstützt | 2022.3.62f3 mit URP 14.0.12: dieselbe Testreihe wie für 6000.5 |
| 2021.3 LTS | 12 | In 1.0 nicht unterstützt | Auf Anfrage geplant |

Migriere in der Editor-Version, mit der du ausliefern wirst: Aufnahmen vorher und nachher sind nur innerhalb einer
Unity-Version vergleichbar.

## Unitys Konverter und der Exit-Code auf 6000.0 und 6000.3

- URP 17.0 und 17.3 haben die ältere Batch-API von Unitys Konverter (`ConverterContainerId`). Auf URP 17.3.0 (6000.3.26f1) wirft
  sie in jedem Projekt `MissingMethodException` für den abstrakten `Base2DMaterialUpgrader`, bevor sie etwas konvertiert. Urpify
  listet dann die Built-in-zu-URP-Konverter selbst auf und führt sie genauso aus; die Details des Schritts sagen das.
- Unity 6000.0.84f1 und 6000.3.26f1 häufig, 6000.5.9f1 gelegentlich stürzen beim Beenden im Batch-Modus ab (Exit-Code 139), nachdem
  die Arbeit erledigt ist, mit oder ohne Urpify. Lies auf der Kommandozeile `Urpify/urpify-<command>-result.txt` (siehe [Kommandozeile](cli.md)).

## Was sich zwischen URP 14 und URP 17 unterscheidet

Urpifys Shader-Bibliothek (`BirpCompat*.hlsl`) und sein Renderer Feature berücksichtigen jeden Unterschied unten, sodass
ein konvertierter Shader in beiden Versionen kompiliert und rendert. Jede Zeile ist an den Paketquellen von URP 14.0.12
(2022.3.62f3) und URP 17.5.0 (6000.5.9f1) geprüft.

| Thema | URP 14.0.12 | URP 17.5.0 | Wo geprüft |
|---|---|---|---|
| Hauptlicht | `GetMainLight()`, `(shadowCoord)`, `(shadowCoord, positionWS, shadowMask)`, `(inputData, shadowMask, aoFactor)` | Dieselben 4 Überladungen | `ShaderLibrary/RealtimeLights.hlsl` |
| Zusätzliche Lichter | `GetAdditionalLight(i, positionWS)`, `(i, positionWS, shadowMask)`, `(i, inputData, shadowMask, aoFactor)` | Dieselben 3 Überladungen | `ShaderLibrary/RealtimeLights.hlsl` |
| Forward+-Lichtschleife | `USE_FORWARD_PLUS` | `USE_CLUSTER_LIGHT_LOOP`; `USE_FORWARD_PLUS` bleibt als Alias in `ForwardPlusKeyword.deprecated.hlsl` | `ShaderLibrary/RealtimeLights.hlsl`, `Core.hlsl` |
| Schatten zusätzlicher Lichter | `AdditionalLightRealtimeShadow(lightIndex, positionWS, lightDirection)` | Dasselbe plus eine Überladung mit Schattenparametern und Sampling-Daten | `ShaderLibrary/Shadows.hlsl` |
| Sphärische Harmonische | `SampleSH(normalWS)` in `GlobalIllumination.hlsl` des URP-Pakets | `SampleSH(normalWS)` in `AmbientProbe.hlsl` des Core-Pakets | `ShaderLibrary` von URP und Core |
| Lightmaps | `LIGHTMAP_ON`, `DIRLIGHTMAP_COMBINED` | Dieselben Keywords | `ShaderLibrary/GlobalIllumination.hlsl` |
| LOD-Crossfade | `LODCrossFade.hlsl` | `LODCrossFade.hlsl` | `ShaderLibrary/` |
| Decals | `DBuffer.hlsl` | `DBuffer.hlsl` | `ShaderLibrary/` |
| Tiefen-, Normalen-, Opaque-Textur | `DeclareDepthTexture.hlsl`, `DeclareNormalsTexture.hlsl`, `DeclareOpaqueTexture.hlsl` | Dieselben Dateien | `ShaderLibrary/` |
| Renderer Feature | `ScriptableRenderPass.Execute`; Render Graph intern | Render Graph (`RecordRenderGraph`); seit Unity 6.4 gibt es keinen Kompatibilitätsmodus mehr: `RenderGraphSettings.enableRenderCompatibilityMode` gibt immer false zurück | `Runtime/Passes/ScriptableRenderPass.cs`, `Runtime/Settings/RenderGraphSettings.cs` |
| Opaque Texture für Vorschaukameras | Für eine Vorschaukamera nicht kopiert (`copyColorPass &= !isPreviewCamera`) | Kopiert | `Runtime/UniversalRenderer.cs`. Deshalb rendert Urpifys Selbstprüfung unter 2022.3 GrabPass-Shader mit einer Game-Kamera. |
| Texturen des URP Asset | Ein gebauter Player wirft jedes Bild eine `NullReferenceException` und rendert schwarz, wenn die `m_Textures` des URP Asset (Blue Noise, Bayer) leer sind, wie der Konverter von Unity sie speichert | In Playern unter 6000.5 aus denselben Projekten nicht beobachtet | `UniversalRenderPipeline.cs` (`SetupPerFrameShaderConstants`). Das kostenlose Paket füllt sie nach dem Konverter von Unity. |

## Was sich am Konverter von Unity unterscheidet

| Thema | 2022.3 / URP 14 | 6000.5 / URP 17.5 |
|---|---|---|
| Materialregeln für `Legacy Shaders/Particles/VertexLit Blended` und `Mobile/Bumped Specular (1 Directional Realtime Light)` | Unter alten Shadernamen registriert, daher überspringt der Konverter von Unity sie; Urpify stellt diese Materialien nach seiner eigenen Tabelle um | Unity stellt sie selbst um |
| Batch-Modus ohne `-typesFilter` | Nicht gemessen: Urpify nutzt den Batch-Befehl von Unity auch dort nicht | Wirft `NullReferenceException` (`Converters.cs:134`); Urpify ruft den Konverter über seine API auf |
| Exit-Code des Batch-Befehls von Unity nach erfolgreicher Arbeit | Nicht gemessen | 3 von 4 echten Projekten endeten mit Code 21 oder 139 (der Prozess stürzt beim Beenden ab); Urpify beurteilt das Ergebnis am Log des Konverters und an einem erneuten Scan, in beiden Versionen |
