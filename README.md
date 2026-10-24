# Urpify (free)

Find everything that breaks when a Unity project moves from the Built-in Render Pipeline to URP, including what breaks
without an error, before anything changes. Then migrate step by step, compare every scene before and after, and roll
back with one click.

This is the free edition of Urpify, under the MIT license. **Urpify Pro** ($25, Asset Store and itch.io) adds the
converters for custom shaders, image effects, camera scripts, command buffers and projectors, one shader file for both
pipelines, and the export of converted `.unitypackage` files.

332 open-source shaders checked with Urpify Pro: [the full list](https://nbxteam.github.io/urpify/).

## What the free edition does

- **Scan** a project, a folder, a shader or a `.unitypackage`: every shader, script, material, scene and setting that
  breaks in URP, sorted into renders pink, breaks without error and looks different, with the reason and who fixes it.
- **Migrate** step by step, from a window or the command line: back up, install URP, run Unity's Render Pipeline
  Converter with the workarounds it needs, run the fixes after it, capture and compare, review.
- **Fix what Unity's converter gets wrong**: glass in Standard Transparent mode, Legacy Self-Illumin, the Editor's
  built-in materials, Built-in shaders without an upgrader, Post-processing v2 bloom, URP Asset settings and textures,
  material variants, `Shader.Find` with Built-in names.
- **Compare scenes** before and after with captured frames, a slider and a difference map.
- **Roll back** any step, or the whole migration.

## Install

Unity 2022.3 LTS, 6000.0 LTS, 6000.3 LTS, 6000.5 or 6000.6 (the versions tested), in a project that uses the Built-in Render Pipeline.

**The simplest way:** download `Urpify-1.0.1.unitypackage` from
[Releases](https://github.com/nbxTeam/urpify/releases/latest) and open it in your project (Assets > Import Package >
Custom Package). The demo scene is inside: Help > Urpify > Import Demo.

**Or with Package Manager:** `+` > **Add package from git URL**:

```
https://github.com/nbxTeam/urpify.git?path=com.urpify.core#v1.0.1
```

or download this repository and **Add package from disk** > `com.urpify.core/package.json`.

Then Window > Rendering > **Urpify** > Scan project. The documentation is in `com.urpify.core/Documentation~`, starting
with [the quick start](com.urpify.core/Documentation~/quick-start.md). Package Manager > Urpify > Samples > **Demo**
imports a scene with one booth per construct that breaks in URP.

## Privacy

No telemetry. The only network call is **Send report**, which you start: it lists the files that would be sent and sends
nothing until you press Send.

## License

MIT, see [LICENSE.md](com.urpify.core/LICENSE.md). The demo's textures are from ambientCG, CC0.

## Support

nbxteamsup@gmail.com, or an issue in this repository.

## Contributing

Issues and pull requests are welcome. A shader the scan misreads, or a construct it misses, helps most: please attach a
minimal shader that shows it, under a license that allows us to use it in tests.
