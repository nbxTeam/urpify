using System;
using System.Collections.Generic;

namespace Urpify
{
    /// <summary>
    /// Built-in shaders by name, with the URP shader that takes their place and the property names that change. Used for
    /// Shader.Find in scripts (TZ 6.11) and for materials of Built-in shaders Unity's converter has no rule for.
    /// </summary>
    public static class BuiltInShaderTable
    {
        public sealed class Entry
        {
            public string Urp;
            /// <summary>Built-in property name -> URP property name (on top of _MainTex -> _BaseMap, _Color -> _BaseColor).</summary>
            public Dictionary<string, string> Renames = new Dictionary<string, string>(StringComparer.Ordinal);
            /// <summary>What differs from the Built-in look, for the report; null when nothing does.</summary>
            public string Note;
            /// <summary>Materials: URP's Surface Type Transparent (alpha blending).</summary>
            public bool Transparent;
        }

        /// <summary>Renames every entry has: URP's shaders keep the Built-in names only as hidden properties.</summary>
        public static readonly Dictionary<string, string> BaseRenames = new Dictionary<string, string>(StringComparer.Ordinal)
            { ["_MainTex"] = "_BaseMap", ["_Color"] = "_BaseColor" };

        static Entry E(string urp, string note = null, params string[] renames)
        {
            var e = new Entry { Urp = urp, Note = note };
            for (int i = 0; i + 1 < renames.Length; i += 2) e.Renames[renames[i]] = renames[i + 1];
            return e;
        }

        /// <summary>Shaders a script asks for with Shader.Find, with the URP shader Unity's material upgraders use for them.</summary>
        public static readonly Dictionary<string, Entry> ForScripts = new Dictionary<string, Entry>(StringComparer.Ordinal)
        {
            ["Standard"] = E("Universal Render Pipeline/Lit", null, "_Glossiness", "_Smoothness"),
            ["Standard (Specular setup)"] = E("Universal Render Pipeline/Lit",
                "Standard (Specular setup) becomes URP Lit in its metallic workflow; for the specular workflow set _WorkflowMode to 0 and enable _SPECULAR_SETUP in code.", "_Glossiness", "_Smoothness"),
            ["Legacy Shaders/Diffuse"] = E("Universal Render Pipeline/Simple Lit"),
            ["Legacy Shaders/Bumped Diffuse"] = E("Universal Render Pipeline/Simple Lit"),
            ["Legacy Shaders/Specular"] = E("Universal Render Pipeline/Simple Lit"),
            ["Legacy Shaders/Bumped Specular"] = E("Universal Render Pipeline/Simple Lit"),
            ["Legacy Shaders/VertexLit"] = E("Universal Render Pipeline/Simple Lit"),
            ["Mobile/Diffuse"] = E("Universal Render Pipeline/Simple Lit"),
            ["Mobile/Bumped Diffuse"] = E("Universal Render Pipeline/Simple Lit"),
            ["Unlit/Texture"] = E("Universal Render Pipeline/Unlit"),
            ["Unlit/Color"] = E("Universal Render Pipeline/Unlit"),
            ["Unlit/Transparent"] = E("Universal Render Pipeline/Unlit",
                "Unlit/Transparent becomes URP Unlit, which is opaque until _Surface is set to 1 (Transparent) with its blend settings."),
            ["Unlit/Transparent Cutout"] = E("Universal Render Pipeline/Unlit",
                "Unlit/Transparent Cutout becomes URP Unlit; enable _ALPHATEST_ON and set _AlphaClip to 1 for the cutout."),
            ["Particles/Standard Unlit"] = E("Universal Render Pipeline/Particles/Unlit", null, "_TintColor", "_BaseColor"),
            ["Particles/Standard Surface"] = E("Universal Render Pipeline/Particles/Lit", null, "_Glossiness", "_Smoothness", "_TintColor", "_BaseColor"),
        };

        /// <summary>
        /// Materials of Built-in shaders that Unity's converter has no upgrader for and that URP does not draw as they are
        /// (decision before M8 No. 2): the nearest URP shader, the property names that change, and the surface setting
        /// (Transparent) the material gets. Which shaders need it was measured on 6000.5.9f1 / URP 17.5.0
        /// and 2022.3.62f3 / URP 14.0.12 with Tools/Editor/UrpifyBuiltInShaders.cs (Spikes/M8/builtin-shaders-*.tsv).
        /// </summary>
        public static readonly Dictionary<string, Entry> ForMaterials = new Dictionary<string, Entry>(StringComparer.Ordinal)
        {
            ["Legacy Shaders/Decal"] = E("Universal Render Pipeline/Simple Lit",
                "Legacy Decal becomes Simple Lit with its base texture; the decal texture (_DecalTex) is not drawn."),
            ["Legacy Shaders/Transparent/VertexLit"] = Transparent(E("Universal Render Pipeline/Simple Lit")),
            ["Legacy Shaders/Transparent/Parallax Diffuse"] = Transparent(E("Universal Render Pipeline/Simple Lit",
                "Parallax (_ParallaxMap) is not drawn: Simple Lit has no height map.")),
            ["Legacy Shaders/Transparent/Parallax Specular"] = Transparent(E("Universal Render Pipeline/Simple Lit",
                "Parallax (_ParallaxMap) is not drawn: Simple Lit has no height map.")),
            // URP 14.0.12 has upgraders for these two under their old names ("Particles/VertexLit Blended", "Mobile/Bumped
            // Specular (1 Directional Light)"), so on 2022.3 Unity's converter leaves them pink; URP 17.5 upgrades them itself.
            ["Legacy Shaders/Particles/VertexLit Blended"] = Transparent(E("Universal Render Pipeline/Particles/Simple Lit",
                "The emissive colour (_EmisColor) is not carried over.")),
            ["Mobile/Bumped Specular (1 Directional Realtime Light)"] = E("Universal Render Pipeline/Simple Lit",
                "Shininess (_Shininess) is not carried over; Simple Lit's own smoothness applies."),
        };

        static Entry Transparent(Entry e) { e.Transparent = true; return e; }

        /// <summary>Names that are Built-in's own shaders (as opposed to a project shader with a similar name).</summary>
        public static bool IsBuiltInName(string name) =>
            name.StartsWith("Legacy Shaders/", StringComparison.Ordinal) || name.StartsWith("Mobile/", StringComparison.Ordinal)
            || name.StartsWith("Unlit/", StringComparison.Ordinal) || name.StartsWith("Particles/", StringComparison.Ordinal)
            || name.StartsWith("Nature/", StringComparison.Ordinal) || name.StartsWith("Standard", StringComparison.Ordinal);
    }
}
