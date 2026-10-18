using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Urpify.Pipeline.AfterUnity
{
    /// <summary>
    /// Material Variants after Unity's converter (decision before M8 No. 2). A variant's file keeps only its own values
    /// (m_SavedProperties holds the overrides; the rest comes from m_Parent), under the Built-in property names. Measured
    /// on 6000.5.9f1 (URP 17.5.0) and 2022.3.62f3 (URP 14.0.12), M8, Tools/Editor/UrpifyVariantProbe.cs: after Unity's
    /// converter the variant follows its parent to URP Lit, but its own values are gone, so it silently looks like its
    /// parent. The values are read back from the variant's file in the backup taken before Unity's converter, put through
    /// Unity's own upgrader for the Built-in shader of the root material (a temporary material with and without them), and
    /// every URP property the variant's values change becomes an override of the variant again.
    /// </summary>
    public static class MaterialVariantFix
    {
        /// <summary>A material file as it was before Unity's converter (that step's backup), or null. The tests replace it.</summary>
        public static Func<string, string> OriginalText = path =>
        {
            // The first run of the step holds the file as it was; a run after an interruption may hold it half converted.
            var store = new BackupStore(".");
            foreach (var id in MigrationState.Snapshots(MigrationState.Load().Get(StepId.UnityConverter)))
            {
                var copy = store.CopyIn(id, path);
                if (copy != null) return File.ReadAllText(copy);
            }
            return null;
        };

        public static List<KitConverterItem> Run(IEnumerable<Finding> variants)
        {
            var items = new List<KitConverterItem>();
            IList upgraders = null;
            foreach (var f in variants.GroupBy(f => f.AssetPath).Select(g => g.First()))
            {
                var item = new KitConverterItem { Path = f.AssetPath, Converter = "Material Variants" };
                items.Add(item);
                var variant = AssetDatabase.LoadAssetAtPath<Material>(f.AssetPath);
                var original = f.Evidence;
                var builtIn = Shader.Find(original);
                if (variant == null || variant.parent == null)
                {
                    item.Outcome = "Info";
                    item.Notes.Add("Info: the material is no longer a variant; nothing to carry over.");
                    continue;
                }
                if (builtIn == null || variant.shader == null || variant.shader == builtIn)
                {
                    item.Outcome = "Info";
                    item.Notes.Add($"Info: the variant's shader is still {variant.shader?.name}; nothing to carry over.");
                    continue;
                }
                var chain = Chain(f.AssetPath);
                if (chain == null)
                {
                    item.Outcome = "NotSupportedYet";
                    item.Notes.Add("Not supported yet: the variant's file from before Unity's converter is not in the backup, so its own values cannot be read back.");
                    continue;
                }
                upgraders = upgraders ?? UnityMaterialUpgraders.All();
                if (upgraders == null) { item.Outcome = "NotSupportedYet"; item.Notes.Add("Not supported yet: Unity's material upgraders were not found in this URP version."); continue; }
                // Without: the root and the ancestors' values; with: the variant's own values on top.
                var without = new Material(builtIn) { hideFlags = HideFlags.HideAndDontSave };
                var with = new Material(builtIn) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    for (int i = 0; i < chain.Count; i++)
                    {
                        if (i < chain.Count - 1) chain[i].Apply(without);
                        chain[i].Apply(with);
                    }
                    UnityMaterialUpgraders.Upgrade(upgraders, without);
                    UnityMaterialUpgraders.Upgrade(upgraders, with);
                    if (with.shader == builtIn) { item.Outcome = "NotSupportedYet"; item.Notes.Add($"Not supported yet: Unity has no material upgrader for {original}."); continue; }
                    int n = Overrides(with, without, variant);
                    item.Outcome = "Auto";
                    item.Notes.Add(n == 0
                        ? "Info: the variant already has its own values on the URP properties."
                        : $"Info: {n} values of the variant set for {original} override the URP shader's properties again ({with.shader.name}), as Unity's upgrader maps them.");
                    if (n > 0) EditorUtility.SetDirty(variant);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(without);
                    UnityEngine.Object.DestroyImmediate(with);
                }
            }
            if (items.Count > 0) AssetDatabase.SaveAssets();
            return items;
        }

        /// <summary>
        /// The saved values from the root material down to the variant, each as it was before Unity's converter; null when
        /// the variant's own file is not in the backup. An ancestor missing from the backup leaves the shader's defaults.
        /// </summary>
        static List<SavedProperties> Chain(string variantPath)
        {
            var list = new List<SavedProperties>();
            var path = variantPath;
            for (int depth = 0; depth < 16 && !string.IsNullOrEmpty(path); depth++)
            {
                var text = OriginalText(path);
                if (text == null)
                {
                    if (depth == 0) return null;
                    break;
                }
                var saved = SavedProperties.Parse(text);
                list.Insert(0, saved);
                path = string.IsNullOrEmpty(saved.ParentGuid) ? null : AssetDatabase.GUIDToAssetPath(saved.ParentGuid);
            }
            return list;
        }

        /// <summary>Sets on the variant every URP property the variant's own values change; returns how many.</summary>
        static int Overrides(Material with, Material without, Material variant)
        {
            int n = 0;
            var shader = with.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                var name = shader.GetPropertyName(i);
                if (!variant.HasProperty(name)) continue;
                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color:
                        if (with.GetColor(name) != without.GetColor(name) && with.GetColor(name) != variant.GetColor(name)) { variant.SetColor(name, with.GetColor(name)); n++; }
                        break;
                    case ShaderPropertyType.Vector:
                        if (with.GetVector(name) != without.GetVector(name) && with.GetVector(name) != variant.GetVector(name)) { variant.SetVector(name, with.GetVector(name)); n++; }
                        break;
                    case ShaderPropertyType.Float: case ShaderPropertyType.Range:
                        if (!Mathf.Approximately(with.GetFloat(name), without.GetFloat(name)) && !Mathf.Approximately(with.GetFloat(name), variant.GetFloat(name))) { variant.SetFloat(name, with.GetFloat(name)); n++; }
                        break;
                    case ShaderPropertyType.Int:
                        if (with.GetInteger(name) != without.GetInteger(name) && with.GetInteger(name) != variant.GetInteger(name)) { variant.SetInteger(name, with.GetInteger(name)); n++; }
                        break;
                    case ShaderPropertyType.Texture:
                        if (with.GetTexture(name) != without.GetTexture(name) && with.GetTexture(name) != variant.GetTexture(name)) { variant.SetTexture(name, with.GetTexture(name)); n++; }
                        var st = new Vector4(with.GetTextureScale(name).x, with.GetTextureScale(name).y, with.GetTextureOffset(name).x, with.GetTextureOffset(name).y);
                        var stWithout = new Vector4(without.GetTextureScale(name).x, without.GetTextureScale(name).y, without.GetTextureOffset(name).x, without.GetTextureOffset(name).y);
                        var stVariant = new Vector4(variant.GetTextureScale(name).x, variant.GetTextureScale(name).y, variant.GetTextureOffset(name).x, variant.GetTextureOffset(name).y);
                        if (st != stWithout && st != stVariant)
                        {
                            variant.SetTextureScale(name, with.GetTextureScale(name));
                            variant.SetTextureOffset(name, with.GetTextureOffset(name));
                            n++;
                        }
                        break;
                }
            }
            return n;
        }

        /// <summary>A material file's m_SavedProperties (serializedVersion 3, Unity 2019.3 and later), its keywords and its parent.</summary>
        public sealed class SavedProperties
        {
            public string ParentGuid = "";
            public readonly List<string> Keywords = new List<string>();
            public readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>(StringComparer.Ordinal);
            public readonly Dictionary<string, float> Floats = new Dictionary<string, float>(StringComparer.Ordinal);
            public readonly Dictionary<string, int> Ints = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly Dictionary<string, TexEnv> Textures = new Dictionary<string, TexEnv>(StringComparer.Ordinal);

            public sealed class TexEnv
            {
                public long FileId;
                public string Guid = "";
                public Vector2 Scale = Vector2.one;
                public Vector2 Offset = Vector2.zero;
            }

            static readonly Regex Map = new Regex(@"(\w+):\s*([^,}]+)");
            static readonly Regex Entry = new Regex(@"^    - (\w+):\s*(.*)$");
            static readonly Regex TexField = new Regex(@"^        m_(Texture|Scale|Offset):\s*\{(.*)\}");
            static readonly Regex Parent = new Regex(@"^  m_Parent:\s*\{(.*)\}");
            static readonly Regex KeywordList = new Regex(@"^  m_(ValidKeywords|ShaderKeywords):\s*(.*)$");

            static Dictionary<string, string> Fields(string inline) =>
                Map.Matches(inline).Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim(), StringComparer.Ordinal);

            static float F(Dictionary<string, string> d, string key, float fallback) =>
                d.TryGetValue(key, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : fallback;

            public static SavedProperties Parse(string yaml)
            {
                var p = new SavedProperties();
                string section = null, keywordList = null;
                TexEnv tex = null;
                foreach (var raw in yaml.Split('\n'))
                {
                    var line = raw.TrimEnd('\r');
                    var pm = Parent.Match(line);
                    if (pm.Success) { var d = Fields(pm.Groups[1].Value); p.ParentGuid = d.TryGetValue("guid", out var g) ? g : ""; continue; }
                    var km = KeywordList.Match(line);
                    if (km.Success)
                    {
                        keywordList = km.Groups[2].Value.Trim();
                        // m_ShaderKeywords (before 2021.2) is one space-separated string; m_ValidKeywords is a list.
                        if (km.Groups[1].Value == "ShaderKeywords") { p.Keywords.AddRange(keywordList.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)); keywordList = null; }
                        else if (keywordList.Length > 0) keywordList = null; // "[]"
                        continue;
                    }
                    if (keywordList != null)
                    {
                        if (line.StartsWith("  - ", StringComparison.Ordinal)) { p.Keywords.Add(line.Substring(4).Trim()); continue; }
                        keywordList = null;
                    }
                    if (line.StartsWith("    m_", StringComparison.Ordinal))
                    {
                        var name = line.Trim().TrimEnd(':');
                        int colon = name.IndexOf(':');
                        section = colon < 0 ? name : name.Substring(0, colon);
                        tex = null;
                        continue;
                    }
                    if (section == null) continue;
                    if (!line.StartsWith("    ", StringComparison.Ordinal)) { section = null; tex = null; continue; }
                    var em = Entry.Match(line);
                    if (em.Success)
                    {
                        var key = em.Groups[1].Value;
                        var value = em.Groups[2].Value.Trim();
                        tex = null;
                        switch (section)
                        {
                            case "m_TexEnvs": tex = new TexEnv(); p.Textures[key] = tex; break;
                            case "m_Floats":
                                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fl)) p.Floats[key] = fl;
                                break;
                            case "m_Ints":
                                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv)) p.Ints[key] = iv;
                                break;
                            case "m_Colors":
                                var c = Fields(value.Trim('{', '}'));
                                p.Colors[key] = new Color(F(c, "r", 0), F(c, "g", 0), F(c, "b", 0), F(c, "a", 1));
                                break;
                        }
                        continue;
                    }
                    if (tex == null) continue;
                    var tm = TexField.Match(line);
                    if (!tm.Success) continue;
                    var fields = Fields(tm.Groups[2].Value);
                    switch (tm.Groups[1].Value)
                    {
                        case "Texture":
                            tex.FileId = fields.TryGetValue("fileID", out var fid) && long.TryParse(fid, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : 0;
                            tex.Guid = fields.TryGetValue("guid", out var tg) ? tg : "";
                            break;
                        case "Scale": tex.Scale = new Vector2(F(fields, "x", 1), F(fields, "y", 1)); break;
                        case "Offset": tex.Offset = new Vector2(F(fields, "x", 0), F(fields, "y", 0)); break;
                    }
                }
                return p;
            }

            /// <summary>Sets these values on a material of the Built-in shader (properties it does not have are skipped).</summary>
            public void Apply(Material m)
            {
                foreach (var k in Keywords) m.EnableKeyword(k);
                foreach (var kv in Colors) if (m.HasProperty(kv.Key)) m.SetColor(kv.Key, kv.Value);
                foreach (var kv in Floats) if (m.HasProperty(kv.Key)) m.SetFloat(kv.Key, kv.Value);
                foreach (var kv in Ints) if (m.HasProperty(kv.Key)) m.SetInteger(kv.Key, kv.Value);
                foreach (var kv in Textures)
                {
                    if (!m.HasProperty(kv.Key)) continue;
                    if (TryLoad(kv.Value, out var t)) m.SetTexture(kv.Key, t);
                    m.SetTextureScale(kv.Key, kv.Value.Scale);
                    m.SetTextureOffset(kv.Key, kv.Value.Offset);
                }
            }

            /// <summary>The texture a reference points to; false when it cannot be found (left as it is).</summary>
            static bool TryLoad(TexEnv e, out Texture texture)
            {
                texture = null;
                if (e.FileId == 0) return true; // None
                var path = AssetDatabase.GUIDToAssetPath(e.Guid);
                if (string.IsNullOrEmpty(path)) return false;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (o is Texture t && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(t, out _, out long id) && id == e.FileId) { texture = t; return true; }
                }
                texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                return texture != null;
            }
        }
    }
}
