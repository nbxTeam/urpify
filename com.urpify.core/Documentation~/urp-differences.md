# How URP looks different

Some differences after a migration are not breaks and not conversion errors: URP renders the same material a little
differently than Built-in. Compare scenes reports them as Changed when they are large enough, so it helps to know them.

## Standard to URP Lit

![Lab booth STD01: Standard and URP Lit side by side](images/STD01.jpg)

Unity's own conversion of Standard to URP Lit changes highlights and reflections slightly: URP uses a different
specular term and environment reflections. On our test scenes up to about 7 % of a frame differs. Urpify's thresholds
for Same and Changed are calibrated on exactly this conversion, so it alone does not make a scene Changed.

## Additional lights

Built-in draws each additional light in its own additive pass, URP adds them in one pass with its own attenuation curve
and a light limit per object (set in the URP Asset). Scenes with many point lights look slightly different, and lights
beyond the per-object limit are dropped by URP.

## Bloom

URP's bloom spreads small, bright spots wider and softer than Post-processing Stack v2. Urpify carries the bloom
parameters over with a calibration, which brings the glow close but not identical; the rest is the different bloom
pyramid, which no parameter reproduces.

## Ambient light in a Unity 6 player

A scene that takes ambient light from the skybox but has no baked lighting data renders darker in a Unity 6 player than
in the Editor, in Built-in and in URP alike. It is not caused by the migration, but it shows up when you compare a
migrated player with the Editor. The scan reports it ("Skybox ambient light not baked"), and the scene panel's **Bake
environment** bakes only the ambient light and reflections, with a backup.

## GrabPass with Opaque Texture

With the default GrabPass source, transparent objects are missing from refractions, because URP copies the screen before
it draws them. Set GrabPass source to After transparents to match Built-in. See [GrabPass](constructs/grabpass.md).

## Transparent objects and sorting

URP sorts transparent objects the same way, but draws extra passes of converted shaders (3 and more) after all objects'
first two passes, which can change the order of overlapping transparent objects. See
[Extra passes](constructs/extra-passes.md).
