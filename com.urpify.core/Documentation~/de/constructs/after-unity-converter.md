# Korrekturen nach dem Konverter von Unity

Der Render Pipeline Converter von Unity ist das Erste, was die Migration ausführt, und meistens liegt er richtig. Unten
die Fälle, in denen sein Ergebnis messbar falsch ist, und was das kostenlose Paket danach tut. Alles davon ist im
kostenlosen Paket, im Schritt „Urpify-Konverter“.

## Standard im Modus Transparent

![Stand STD04: Glas im Modus Standard Transparent, weiß nach dem Konverter von Unity, nach Urpify wieder Glas](../../images/STD04.jpg)

*STD04: Glas im Modus Transparent von Standard.*

Der Konverter von Unity bildet Transparent auf das Premultiply-Blending von URP ab, das bereits mit Alpha multiplizierte
Farben erwartet. Das Glas wird fast undurchsichtig und weiß. Urpify stellt das Material auf Alpha-Blending mit Preserve
Specular Lighting um, so wie Built-in es mischt. Befund: „..., Modus Transparent“ (`MATERIAL.STANDARD_TRANSPARENT`).

## Legacy Self-Illumin

![Stand LEG03: Legacy Self-Illumin, dunkel nach dem Konverter von Unity, leuchtet nach Urpify](../../images/LEG03.jpg)

*LEG03: `Legacy Shaders/Self-Illumin/Diffuse`.*

Der Konverter von Unity stellt diese Materialien auf Simple Lit um, das keine Emission aus dem Illumin-Alpha nimmt: Das
Leuchten ist weg. Urpify stellt sie auf seinen eigenen Self-Illumin-Shader für URP um, der wie in Built-in leuchtet.
Befund: `MATERIAL.SELF_ILLUMIN`.

## Eingebaute Materialien des Editors

Renderer mit den eingebauten `Default-Material` oder `Default-Diffuse` des Editors (ein neuer Würfel, eine Ebene) lenkt der
Konverter von Unity auf das Standardmaterial von URP, das grau statt weiß ist. Urpify lenkt sie auf URP-Kopien mit den
Werten des eingebauten Materials. Befund: `SCENE.BUILTIN_MATERIAL`.

## Materialien in Paketen

Die Dateien eines Pakets sind schreibgeschützt, daher ändert der Konverter von Unity nie ein Material, das in einem Paket
liegt. Ist sein Shader nur für Built-in, wird jeder Renderer damit pink: Die Wände von ProBuilder 5 nutzen
`ProBuilderDefault.mat`, dessen Shader `ProBuilder/Standard Vertex Color` ein Surface-Shader ist. Urpify stellt diese
Renderer in Szenen und Prefabs auf eine URP-Kopie in `Assets/Urpify/Materials/Packages/` um: Unitys eigene Umstellung für
einen eingebauten Shader (`Auto`), sonst URP Lit mit Farbe, Textur, Smoothness und Metallic des Materials
(`Auto, angenähert`: Was der Shader selbst beiträgt, etwa Vertexfarben, bleibt nicht erhalten). Befund:
`SCENE.PACKAGE_MATERIAL`.

## Eingebaute Shader ohne Umstellungsregel

Für einige eingebaute Shader hat der Konverter von Unity keine Regel („No upgrader found“): `Legacy Shaders/Decal`,
`Legacy Shaders/Transparent/VertexLit`, `Transparent/Parallax Diffuse` und `Specular`, und unter URP 14 zwei Regeln, die
unter alten Namen registriert sind. Urpify stellt diese Materialien auf den nächstliegenden URP-Shader aus seiner eigenen
Tabelle um, mit übernommenen Eigenschaften. Befund: `MATERIAL.BUILTIN_NO_UPGRADER`.

## Post-processing Stack v2

Der Konverter von Unity macht aus PPv2-Volumes und -Profilen URP-Volumes, wartet aber im Batch-Modus nicht auf die
Asset-Suche, die er braucht, und konvertiert keines davon: Bloom verschwindet ohne Fehlermeldung. Urpifys Schritt
„Unitys Konverter ausführen“ wartet auf die Suche und überträgt dann die Bloom-Parameter mit einer Kalibrierung
(Threshold und Scatter), damit das Leuchten näher an Built-in liegt. Der restliche Unterschied ist der Bloom von URP
selbst, siehe [Wie URP anders aussieht](../urp-differences.md). Befund: `SCENE.PPV2`.

## Einstellungen des URP Asset

- **Depth Texture und Opaque Texture** werden in jedem URP Asset eingeschaltet, wenn ein Shader, den URP so zeichnet, wie
  er ist, sie liest (siehe [Tiefentextur](depth-texture.md)).
- **Texturen des URP Asset** (URP 14): Der Konverter von Unity speichert seine URP Assets mit leeren Blue-Noise- und
  Bayer-Texturen. Der Editor füllt sie, ein gebauter Player nicht: Er wirft jedes Bild eine `NullReferenceException` und
  rendert schwarz. Urpify füllt sie aus dem URP-Paket. Befund: `SETTINGS.URP_ASSET_TEXTURES`.
- **Hauptlicht**: Auf Qualitätsstufen mit Pixel Light Count 0 beleuchtete Built-in das Hauptrichtungslicht trotzdem pro
  Pixel; Urpify stellt Main Light von URP dort auf Per Pixel.

## Material Variants

Der Konverter von Unity stellt ein Material Variant auf den URP-Shader seines Elternmaterials um, verwirft aber die
eigenen Werte der Variante: Sie wird zu einer stillen Kopie ihres Elternmaterials. Urpify liest die eigenen Werte der
Variante aus der Sicherung des Schritts „Unitys Konverter ausführen“ und setzt sie als Überschreibungen der
URP-Eigenschaften zurück. Befund: `MATERIAL.VARIANT`.

## Schreibgeschützte Materialien in Modellen

In FBX-Dateien eingebettete Materialien sind schreibgeschützt, und das Modul des Konverters von Unity für sie lief im
Batch-Modus nicht zuverlässig. Urpify führt dieses Modul Objekt für Objekt für die gefundenen Renderer und Terrains aus, in
beiden Unity-Versionen.
