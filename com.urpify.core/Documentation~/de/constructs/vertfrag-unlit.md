# Unbeleuchtete Vert/Frag-Shader

![Stand V01: ein unbeleuchteter CG-Shader mit Nebel und scrollenden UVs, in allen dreien gleich](../../images/V01.jpg)

*Stand V01: ein unbeleuchteter CG-Shader mit Nebel und scrollenden UVs. URP zeichnet ihn so, wie er ist.*

## Was passiert

Ein Vert/Frag-Shader ohne LightMode-Tag oder mit `Always` rendert in URP oft unverändert: Einen Pass ohne LightMode-Tag
zeichnet URP als `SRPDefaultUnlit`, und CG-Code mit `UnityCG.cginc` kompiliert weiterhin. Urpify prüft, dass der Shader in
URP kompiliert, und lässt ihn in Ruhe, wenn er es tut.

Befund: keiner, oder ein Hinweis, dass URP den Shader so zeichnet, wie er ist.

Was trotzdem kaputtgehen kann und seine eigene Seite hat:
- zwei oder mehr Passes ohne LightMode: [Zusätzliche Passes](extra-passes.md);
- Lesen von `_CameraDepthTexture`: [Tiefentextur](depth-texture.md);
- `GrabPass`: [GrabPass](grabpass.md);
- Lichtvariablen von Built-in wie `_LightColor0` in einem unbeleuchteten Pass: In URP sind sie null; Pro bildet sie auf das
  Hauptlicht von URP ab.
