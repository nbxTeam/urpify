# Konstrukte

Urpify unterstützt Konstrukte, nicht einzelne Shader: Ein Shader wird konvertiert, wenn jedes Konstrukt, das er nutzt,
unterstützt wird; sonst nennt der Scan das fehlende. Jede Seite unten erklärt ein Konstrukt der Built-in Render Pipeline:
was in URP kaputtgeht, was Urpify dagegen tut und wie nah das Ergebnis ist.

Die Bilder stammen aus Urpifys Testlabor, einer Szene mit einem eigenen Stand pro Konstrukt. Jedes zeigt denselben Stand
dreimal: in Built-in, in URP nur nach dem Render Pipeline Converter von Unity, und in URP nach dem Konverter von Unity und
Urpify. Es sind die Frames, die Urpifys eigene Aufnahmeschritte gemacht haben, auf den Stand zugeschnitten.

| Konstrukt | Seite | Stand | Wer es behebt |
|---|---|---|---|
| Surface-Shader mit eingebauten Beleuchtungsmodellen | [Surface-Shader](surface-shaders.md) | S01–S04 | Pro |
| Eigene Beleuchtungsfunktionen | [Eigene Beleuchtung](surface-custom-lighting.md) | S05 | Pro |
| Optionen von `#pragma surface`: `vertex:`, `finalcolor:`, `decal:`, `alphatest:`, `addshadow` und weitere | [Surface-Optionen](surface-options.md) | S03, S06–S08, S10, S11 | Pro |
| Tessellation von Surface-Shadern | [Tessellation](surface-tessellation.md) | S09 | Pro |
| Zusätzliche Passes: Umrisse, Fell-Schichten, 3 oder mehr Passes | [Zusätzliche Passes](extra-passes.md) | S12 | Pro |
| Unbeleuchtete Vert/Frag-Shader | [Unbeleuchtete Vert/Frag-Shader](vertfrag-unlit.md) | V01 | Nichts zu tun |
| Beleuchtete Vert/Frag-Shader: ForwardBase, ForwardAdd, Schatten, Vertex-Lit | [Beleuchtete Vert/Frag-Shader](vertfrag-lit.md) | V02 | Pro |
| GrabPass | [GrabPass](grabpass.md) | G01, G02 | Pro |
| `_CameraDepthTexture` | [Tiefentextur](depth-texture.md) | D01 | Free und Pro |
| `_CameraDepthNormalsTexture`, Replacement-Shader | [Depth-Normals und Replacement-Shader](depth-normals.md) | keiner | Pro |
| `OnRenderImage`-Bildeffekte | [Bildeffekte](image-effects.md) | C02 | Pro |
| `CommandBuffer` an Kameraereignissen | [Command Buffer](command-buffers.md) | C03 | Pro |
| Skripte, die Kameras rendern: Spiegel, Portale, Wasserreflexionen | [Kamera-Skripte](camera-scripts.md) | M01 | Pro |
| `Projector`-Komponenten | [Projektoren](projectors.md) | C01 | Pro |
| `Shader.Find` mit Namen eingebauter Shader | [Shader.Find im Code](shader-find.md) | C04 | Free |
| Was der Konverter von Unity kaputtmacht: Standard Transparent, Self-Illumin, eingebaute Materialien, Post-processing v2, Texturen des URP Asset, Material Variants | [Korrekturen nach dem Konverter von Unity](after-unity-converter.md) | STD04, LEG03 | Free |
| Was der Konverter von Unity erledigt: Standard, Legacy, Mobile, Partikel, Terrain, importierte Modelle | [Der Konverter von Unity](unity-converter.md) | STD01–STD06, LEG01–LEG05, PAR01, PAR02, FBX01, FBX02, TER01 | Unity |
| Bäume aus dem Tree Creator und Soft-Occlusion-Bäume | [Bäume](trees.md) | keiner | Pro |
| Eine Shader-Datei für Built-in und URP | [Built-in-Unterstützung behalten](keep-builtin-support.md) | jeder Shader-Stand | Pro |

Konstrukte, die Urpify nicht konvertiert, stehen mit Gründen unter [Was nicht konvertiert wird](../not-converted.md).
