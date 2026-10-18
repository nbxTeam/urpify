# Zusätzliche Passes: Umrisse, Hüllen, Schichten

![Stand S12: ein Toon-Shader mit einem Umriss-Pass aus invertierter Hülle](../../images/S12.jpg)

*Stand S12: ein Toon-Surface-Shader mit einem handgeschriebenen Umriss-Pass. Nur nach dem Konverter von Unity bleibt
davon ein Umriss um eine pinke Kugel.*

## Was kaputtgeht

URP zeichnet höchstens zwei Passes eines SubShaders für ein Objekt: einen `SRPDefaultUnlit` und einen `UniversalForward`,
in dieser Reihenfolge. Built-in zeichnet jeden Pass. Ein Shader mit einem Umriss-Pass neben seinem Beleuchtungs-Pass,
Fellhüllen, Schraffurschichten oder zwei Passes ohne LightMode-Tag behält nur einen davon, oft ohne Meldung.

Befunde: „Zusätzliche Passes neben einem Surface-Programm“ (`SHADER.SURFACE.EXTRA_PASS`), „Passes ohne LightMode: N“
(`SHADER.PASS.MULTIPLE_UNTAGGED`).

## Was Urpify tut (Pro)

- **Zwei gezeichnete Passes:** Der erste wird `SRPDefaultUnlit`, der zweite `UniversalForward`, sodass die
  Zeichenreihenfolge von Built-in erhalten bleibt. Geprüft mit URP 14 und 17.
- **Drei oder mehr gezeichnete Passes:** Passes ab dem dritten bekommen die Tags `UrpifyPass3`, `UrpifyPass4` und so
  weiter. Urpifys Renderer Feature Built-in Camera Hooks zeichnet sie nach den undurchsichtigen oder transparenten Objekten
  von URP, die Passes jedes Objekts in Reihenfolge. Bis zu 32 gezeichnete Passes pro SubShader.
- **Handgeschriebene Passes neben einem Surface-Programm** bleiben erhalten und werden nach den Vert/Frag-Regeln
  konvertiert.
- Bildeffekt-Shader, die `Graphics.Blit` zeichnet, bekommen keine Tags: Ihre Passes werden über den Index gewählt.

Das Renderer Feature wird im Schritt „Urpify-Konverter“ zu jedem URP-Renderer hinzugefügt, der es braucht.

## Wie nah

`Auto` für zwei Passes, `Auto, angenähert` für drei oder mehr. Wenn mehrere solche Objekte auf dem Bildschirm sind,
werden die ersten beiden Passes jedes Objekts vor den zusätzlichen Passes irgendeines Objekts gezeichnet, was die
Sortierung überlappender Objekte ändern kann.
