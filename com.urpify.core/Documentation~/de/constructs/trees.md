# Bäume aus dem Tree Creator und Soft-Occlusion-Bäume

Das Labor hat keinen Stand für Bäume; sie wurden an ShaderTutorials (ronja-tutorials) mit den Bäumen des Nature Starter
Kit geprüft.

## Was kaputtgeht

URP hat keine Tree-Creator-Shader (`Nature/Tree Creator Bark`, `Leaves`, `Leaves Fast`, `Optimized`) und keine
Soft-Occlusion-Baum-Shader. Bäume damit werden pink. Gras und Detail-Meshes des Terrains sind nicht betroffen: URP zeichnet
sie mit seinen eigenen Detail-Shadern.

Befund: „..., keine URP-Version“ (`MATERIAL.BUILTIN_TREE`).

## Was Urpify tut (Pro)

Unity veröffentlicht die Quellen seiner eingebauten Shader unter der MIT-Lizenz. Urpify Pro konvertiert Unitys eigene 8
Baum-Shader mit seinem normalen Konverter, fügt die nötigen Funktionen von `TerrainEngine.cginc` (Wind, Biegen,
Billboards) zu seiner Bibliothek hinzu und stellt die Rinden- und Blattmaterialien, auch die in Prefabs, auf die
konvertierten Shader um. Der Lizenztext steht in `Third Party Notices.md` von Pro.

## Wie nah

`Auto, angenähert`. Die Bäume sind nicht mehr pink und behalten ihre Materialien. Windanimation und Billboards unter URP
wurden an einigen Sekunden Frames angesehen, nicht pixelgenau gemessen; vergleiche deine Szenen im Szenenvergleich.
SpeedTree-Assets stellt der Konverter von Unity um, nicht Urpify.
