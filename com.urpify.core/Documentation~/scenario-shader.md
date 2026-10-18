# Convert a shader

You write or maintain shaders and want one of them on URP, with the formula untouched.

## Open the shader

Any of these opens the shader panel:
- select the shader in the Urpify window's list after a scan;
- right-click the shader in the Project window > Urpify > Analyze;
- drag a `.shader` file into the Urpify window, from the Project window or straight from disk. A file from disk stays
  where it is; nothing is imported.

## Read the code

The **Code** tab shows the original on the left and the converted shader on the right. Changed lines are marked;
hover one for why it changed: a pass retagged for URP, a Built-in include replaced, material properties moved into
`UnityPerMaterial` for the SRP Batcher.

What Urpify changes, and what it leaves alone:
- The shader's name and its property names stay, so materials keep working and keep their values.
- The author's functions (`surf`, `vert`, `frag`, lighting functions, helpers) are copied as they are. Only the wiring
  changes: how the shader gets lights, shadows, depth, the screen picture and matrices.
- Built-in names such as `UnityObjectToClipPos`, `_LightColor0` or `UNITY_LIGHT_ATTENUATION` keep working through
  Urpify's compatibility library, `BirpCompat.hlsl`, which maps them to URP.

**Edit result** lets you change the converted code and **Compile** it with the keywords its materials use; errors appear
on their lines. **Save as...** writes the result to a new file; **Convert** replaces the original after backing it up to
`Urpify/Backup/ShaderPanel`.

When the converter cannot finish, the right column shows a read-only draft with the line where it stopped and why.
You can save the draft to finish by hand; Convert and the migration never use it.

## Preview

The **Preview** tab renders the shader on a sphere, cube, plane or your own mesh, in Built-in and in URP, with the same
material values in both. **View** switches between Built-in, URP, a curtain with a slider, and a difference map.
**Light direction** turns the main light. Change a material property and both previews follow.

The Built-in preview switches the render pipeline off for the moment of rendering and back, inside one call. If that
fails, the panel switches to **From captures** and shows the scene captures instead. For a file that is already
converted, the Built-in half renders the original from the migration's backup.

## Check the result

Every shader the migration converts is also checked by a render: a sphere with the project's own materials of the
shader, in Built-in before the conversion and in URP after it. When more than 25 % of the sphere differs, the shader is
still converted but marked `Review`, and the pair of images is kept in `Urpify/Reports/self-check/`. A shader whose
converted code does not compile is put back as it was and reported.

## When the conversion stops

The panel names the construct the converter does not handle and what to do. [What is not converted](not-converted.md)
lists them all. If the shader should convert and does not, **Report > Send report...** packs the shader, its include
files and the findings into an archive: save it, or send it to us (only when you press Send).

## License of the shader

When the file states a license in its header, the panel shows it. A converted copy stays under the same license; with a
non-commercial license it may not be sold.
