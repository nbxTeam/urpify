# Urpify

Urpify moves a Unity project, a shader or an Asset Store package from the Built-in Render Pipeline to URP. It shows
everything the switch breaks before anything changes, including what breaks without an error. Then it converts what it
can, backs up every file it touches, and compares each scene before and after with captured frames. Every step can be
rolled back.

Urpify comes in two packages:

| | Urpify (free, MIT) | Urpify Pro |
|---|---|---|
| Scan a project, folder, file or `.unitypackage` | Yes | Yes |
| Compare scenes before and after with captured frames | Yes | Yes |
| Run Unity's Render Pipeline Converter reliably, from the window or the command line | Yes | Yes |
| Fixes after Unity's converter (glass, Self-Illumin, built-in materials, Post-processing v2 bloom, URP Asset settings, package materials) | Yes | Yes |
| Backup and rollback | Yes | Yes |
| Shader panel: code, preview in Built-in and URP as they are | Yes | Yes |
| Convert custom shaders, image effects, camera scripts, command buffers and projectors | No | Yes |
| Output: one shader file for both pipelines (Keep Built-in support) | No | Yes |
| Export a converted `.unitypackage` | Report only | Yes |
| Command line | Scan, capture, compare, Unity's converter and the free fixes | Everything |

## Contents

Start here:
- [Quick start](quick-start.md): the whole cycle in 5 minutes, on the demo scene or your project.
- [Migrate a game](scenario-game.md)
- [Convert a shader](scenario-shader.md)
- [Convert an Asset Store package for both pipelines](scenario-asset.md)

How Urpify handles each construct of the Built-in Render Pipeline:
- [Constructs: overview](constructs/index.md), one page per construct with before and after pictures.
- [What is not converted, and why](not-converted.md)
- [How URP looks different](urp-differences.md)

Reference:
- [The Urpify window](window.md): icons, counts, verdicts.
- [Settings](settings.md)
- [Command line](cli.md)
- [Report schema](report-schema.md)
- [Unity and URP versions](urp-version-matrix.md)
- [FAQ](faq.md)
- [Redistributing converted shaders](license-redistribution.md)

## Requirements

- Unity 2022.3 LTS (URP 14), or Unity 6000.0 LTS, 6000.3 LTS, 6000.5 or 6000.6 (URP 17), the versions tested. See [Unity and URP versions](urp-version-matrix.md).
- A project on the Built-in Render Pipeline, or a `.unitypackage` made for it. Urpify installs URP itself when the
  migration needs it.
- No other packages. Urpify sends nothing over the network, except the shader archive you choose to send with
  `Send report` (see [FAQ](faq.md#what-does-send-report-send)).

## Languages

The window speaks English, 简体中文, 日本語, 한국어, Русский, Español, Português (Brasil) and Deutsch. By default it follows
the Editor's language (Preferences > Languages) and uses English when Urpify does not have that language. Change it in
Project Settings > Urpify > Personal > Language. Converter notes in reports, the command line and the HTML report's
technical details stay in English. These pages are also available in the same languages, in the folders next to this one.
