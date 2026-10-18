# Built-in-Unterstützung behalten: ein Shader für beide Pipelines

Ausgabe = **Built-in-Unterstützung behalten** (Project Settings > Urpify > Konvertierung, Pro) schreibt jeden
konvertierten Shader als eine Datei, die in Built-in und in URP funktioniert. Das ist für Asset-Store-Autoren gedacht, die
an Käufer mit beiden Pipelines ausliefern, und für Projekte, die die Pipeline je nach Plattform wechseln.

## Wie die Datei aufgebaut ist

```
Shader "Author/Water" {
    Properties { ... }                      // unverändert
    SubShader {                             // von Urpify hinzugefügt, als erster
        Tags { "RenderPipeline" = "UniversalPipeline" }
        PackageRequirements { "com.unity.render-pipelines.universal" }
        ...                                 // die konvertierten Passes
    }
    SubShader { ... }                       // der ursprüngliche Built-in-SubShader, Byte für Byte
    Fallback "..."
}
```

Unity wählt den ersten SubShader, der zur aktiven Pipeline passt. Mit aktivem URP nimmt es den URP-SubShader; in Built-in
überspringt es ihn wegen des Tags und nimmt das Original. `PackageRequirements` hält ein Built-in-Projekt ohne URP davon
ab, den URP-Teil überhaupt zu kompilieren. Geprüft unter Unity 6000.5 und 2022.3.

Die ursprünglichen SubShader werden nicht angefasst, daher rendert ein Material in Built-in genau wie vorher. Das ist Byte
für Byte geprüft: Jeder Labor-Shader, der mit „Built-in-Unterstützung behalten“ konvertiert wurde, rendert in Built-in
dasselbe Bild wie das Original.

## Was sich mit dieser Ausgabe noch ändert

- Materialien bleiben auf ihren Shadern, daher funktionieren dieselben Materialien in beiden Pipelines.
- `Shader.Find` mit einem Built-in-Namen in einem Skript wählt den Namen zur Laufzeit nach der aktiven Pipeline.
- Konvertierte Skripte und Renderer Features arbeiten nur unter URP und tun unter Built-in nichts.

## Kosten

Ein Shader mit zwei SubShadern kompiliert beide, daher hat ein Build, der ihn enthält, mehr Varianten. Buildgröße und
-zeit mit Shadern für beide Pipelines wurden am Labor gemessen: siehe [FAQ](../faq.md).
