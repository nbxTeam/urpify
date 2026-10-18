# Was der Konverter von Unity erledigt

Der Render Pipeline Converter von Unity stellt Materialien um, die Unitys eigene eingebaute Shader verwenden. Urpify führt
ihn als Schritt „Unitys Konverter ausführen“ der Migration aus, mit den Umgehungen, die er für einen zuverlässigen Lauf aus
einem Skript braucht, und meldet sein Log.

![Stand STD01: Standard Metallic mit Normal- und Metallic-Map](../../images/STD01.jpg)

*STD01: Standard, Metallic-Workflow, Normal- und Metallic-Map. Der Konverter von Unity stellt es auf URP Lit um.*

![Stand TER01: ein Terrain mit zwei Ebenen](../../images/TER01.jpg)

*TER01: Ein Terrain mit zwei Ebenen wechselt zum Terrain-Shader von URP.*

Was der Konverter abdeckt, gemessen an den Ständen STD01 bis STD06, LEG01 bis LEG05, PAR01, PAR02, FBX01, FBX02 und TER01:

- Standard und Standard (Specular setup) in den Modi Opaque, Cutout und Fade, mit Normal-, Emissions- und Detail-Maps.
  Der Modus Transparent ist die Ausnahme, siehe [Korrekturen nach dem Konverter von Unity](after-unity-converter.md).
- Legacy Shaders (Diffuse, Specular, Bumped, Reflective, Transparent, Cutout, Lightmapped, Parallax, VertexLit);
  Self-Illumin ist die Ausnahme.
- Mobile-Shader, Unlit/Texture, Particles/Standard Surface und Unlit, Legacy-Partikel.
- Nature/Terrain/Standard und das Standard-Terrainmaterial; SpeedTree.
- In importierte Modelle eingebettete Materialien.
- Animationsclips, die Materialeigenschaften von Built-in animieren (der Scan listet sie auf; Urpify prüft das Ergebnis
  noch nicht).
- Post-processing Stack v2, siehe [Korrekturen nach dem Konverter von Unity](after-unity-converter.md).

Urpifys Scan führt diese Materialien als vom Konverter von Unity behoben, daher zählt die Übersicht sie getrennt von dem,
was Urpify braucht.

## Zuverlässig ausführen

Unter Unity 6.5 wirft der dokumentierte Batch-Befehl von Unity ohne `-typesFilter` eine `NullReferenceException`, und der
Editor stürzt nach einem erfolgreichen Lauf beim Beenden oft ab (Exit-Codes 21, 139). Urpify ruft den Konverter über seine
API auf, beurteilt das Ergebnis am Log des Konverters und an einem erneuten Scan statt am Exit-Code und nimmt das Log in
den Bericht auf.
