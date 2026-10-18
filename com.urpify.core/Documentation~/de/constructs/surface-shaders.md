# Surface-Shader

![Stand S01: ein Standard-Surface-Shader, pink nach dem Konverter von Unity, nach Urpify wie in Built-in](../../images/S01.jpg)

*Stand S01: ein Surface-Shader mit dem Beleuchtungsmodell Standard, Albedo, Normal Map und Okklusion.*

## Was kaputtgeht

`#pragma surface` ist eine Funktion der Built-in-Pipeline: Unity erzeugt die Passes des Shaders (ForwardBase,
ForwardAdd, ShadowCaster, Deferred, Meta) aus der Surface-Funktion. URP hat keine Surface-Shader, und diese Passes werden
mit dem Fehler-Shader gezeichnet, daher wird jedes Objekt mit einem solchen Shader pink. Der Konverter von Unity lässt
eigene Shader in Ruhe.

Befund: „Surface-Shader, Beleuchtung ...“ (`SHADER.SURFACE`), Schweregrad „Wird pink“.

## Was Urpify tut (Pro)

Der Konverter schreibt die Passes, die URP braucht, um die Surface-Funktion des Autors herum, die er unverändert
übernimmt:

| Pass | Woraus |
|---|---|
| `UniversalForward` | die Surface-Funktion, dann die URP-Beleuchtung für das Beleuchtungsmodell |
| `ShadowCaster` | derselbe Vertex-Code mit dem Schatten-Bias von URP |
| `DepthOnly`, `DepthNormals` | derselbe Vertex-Code; für die Tiefentextur, Depth Priming und SSAO |
| `Meta` | Albedo und Emission der Surface-Funktion, für das Backen von Lightmaps |

| Beleuchtungsmodell | Beleuchtung in URP |
|---|---|
| `Standard`, `StandardSpecular` | `UniversalFragmentPBR`, Metallic- oder Specular-Workflow |
| `Lambert`, `BlinnPhong` | `LightingLambert` und `LightingBlinnPhong` von Built-in aus Urpifys Bibliothek, für das Hauptlicht und jedes zusätzliche Licht |
| Deine `Lighting<Name>` | deine Funktion, siehe [Eigene Beleuchtung](surface-custom-lighting.md) |

Die `Input`-Struktur wird wie in Built-in gefüllt: `uv_`- und `uv2_`-Koordinaten mit der Kachelung der Textur,
`worldPos`, `worldNormal`, `worldRefl`, `viewDir`, `screenPos`, `color : COLOR`, `facing : VFACE`, `INTERNAL_DATA` mit
`WorldNormalVector` und die Felder, die deine `vertex:`-Funktion füllt. Materialeigenschaften wandern für den SRP Batcher
in `UnityPerMaterial`, und Shadername und Eigenschaftsnamen bleiben, sodass Materialien ihre Werte behalten.

Mehrere `SubShader`-Blöcke und LOD behalten ihre Bedeutung. Ein `Fallback` auf einen eingebauten Shader zeigt auf den
passenden URP-Shader.

## Wie nah

`Auto` für Standard, StandardSpecular, Lambert und BlinnPhong: Das Ergebnis stimmt mit Built-in überein, bis auf die
Unterschiede der eigenen Beleuchtung von URP (siehe [Wie URP anders aussieht](../urp-differences.md)). Jeder konvertierte
Shader wird außerdem auf einer Kugel in Built-in und URP gerendert; ändern sich mehr als 25 % der Kugel, wird er als
`Prüfen` markiert.

![Stand S02: StandardSpecular](../../images/S02.jpg)

*S02: StandardSpecular.*

![Stand S04: BlinnPhong mit alpha:fade](../../images/S04.jpg)

*S04: BlinnPhong mit `alpha:fade`, eine transparente Kugel.*
