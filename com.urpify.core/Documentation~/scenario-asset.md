# Convert an Asset Store package for both pipelines

You publish shaders or effects on the Asset Store and want one package that works for buyers on Built-in and on URP.
The main action is **Export for Built-in and URP** (Pro).

## What you need

- A project with URP installed. The converted shaders are compiled for URP before they are written, so the export
  refuses to run without it. Your package itself can stay a Built-in package; the project only hosts the export.
- The `.unitypackage` you ship today.

## Analyze the package

In the Urpify window: **Change** (scope) > **Unity package (.unitypackage)...** and choose your file. Urpify imports
it into a temporary folder, scans it, and removes the folder when you close the window or change the scope. Your project
stays as it was.

The overview shows what breaks for a buyer on URP, the same way as for a project. The free package stops here: the
report (Report > Export HTML) is what you can show or attach.

## Export

On the overview, **Export for Built-in and URP...**, or Report > **Export converted package...**. Choose where to save.
The new `.unitypackage` keeps every GUID and path of the original, and contains:

- the converted shaders. With Output **Keep Built-in support** (Project Settings > Urpify > Conversion > Output), each
  file has its original Built-in SubShaders unchanged plus a URP SubShader in front of them, so one file works in both
  pipelines; Unity picks the SubShader that matches the active pipeline;
- materials that used Unity's Built-in shaders, upgraded the way Unity's converter would do it in the buyer's project,
  with Urpify's fixes after it;
- scripts converted like in a project, each compiled against the project before it is written. A script whose rewrite
  does not compile stays as it was and is listed;
- Urpify's shader library and its small runtime part, in a `UrpCompat` folder of each root folder of your package, with
  new GUIDs on every export and the runtime in your package's namespace. Two packages from different authors never share
  or overwrite each other's copy;
- a small Editor check, named after your package. When the buyer's project uses URP, it checks the URP Assets for what
  your shaders need (Depth Texture, Opaque Texture, the Built-in Camera Hooks renderer feature) and asks once before it
  turns anything on. In Built-in it does nothing.

A compatibility report is saved next to the package: every shader with its result, and the URP Asset settings buyers
need.

## Check it like a buyer

Import the exported package into a clean Built-in project and into a clean URP project. In Built-in, the Keep Built-in
support output renders exactly as the original: the original SubShaders are not touched. In URP, open the package's demo
scene and compare it with your Built-in screenshots, or run the migration's Compare scenes on it.

## Redistribution

You may ship the converted shaders and the `UrpCompat` folder in your paid packages. See
[Redistributing converted shaders](license-redistribution.md).
