# Redistributing converted shaders

> DRAFT for the owner's approval (architect's decision 2 after M5). Publisher: nbxTeam. Not legal advice; have it reviewed
> before release.

This section adds to the license of Urpify Pro (the Asset Store End User License Agreement for the Asset Store edition).
It says what you may ship in your own products after converting them with Urpify.

## 1. Your converted files are yours

Shaders, materials, scripts and other files that Urpify converts or generates from your files ("Converted Files") remain
your work, under the license your original files had. Urpify claims no rights in them and adds no conditions to them,
beyond section 3 for the library files Urpify adds.

If an original file was under someone else's license (for example a shader you downloaded), its converted copy stays
under that license. Urpify shows the license line it finds in a shader's header; a non-commercial license stays
non-commercial after conversion.

## 2. What Urpify adds to your files

A conversion can add these files ("Urpify Runtime Files"):
- Urpify's shader compatibility library: `BirpCompat*.hlsl`, in `Assets/Urpify/ShaderLibrary/` or, in an exported
  package, in a `UrpCompat` folder of the package;
- its runtime part for converted scripts: the Built-in Camera Hooks renderer feature and the helpers that run image
  effects, command buffers, camera callbacks and projectors in URP;
- the Editor check an exported package uses to ask its buyer for URP settings (`UrpSettingsCheck_<package>.cs`);
- URP versions of Unity's own Built-in tree shaders, converted from Unity's MIT-licensed sources.

## 3. You may redistribute Urpify Runtime Files

As a licensee of Urpify Pro you may copy, include and distribute the Urpify Runtime Files, unmodified or modified, as part
of your own products, including paid Asset Store packages and games, in source or compiled form, without royalties and
without any further license from us, on these conditions:

1. They are distributed only as part of a product of yours that contains Converted Files that need them, not as a
   standalone library and not as a tool that converts shaders.
2. The notice at the top of each library file stays in it.
3. The Built-in tree shaders keep Unity's MIT license text, as listed in `Third Party Notices.md`.

Your buyers may use, modify and redistribute the Urpify Runtime Files as part of your product under the same conditions
and need no Urpify license of their own.

## 4. What stays ours

Urpify itself, its converters and the Pro editor assembly are not Runtime Files. You may not redistribute them, in source
or compiled form, or offer them as a service. The free edition (`com.urpify.core`) is MIT-licensed and can be shared under
the MIT license.

## 5. Questions

Support contact: nbxteamsup@gmail.com.
