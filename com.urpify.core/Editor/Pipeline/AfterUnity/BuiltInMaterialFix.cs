using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Urpify.Pipeline.AfterUnity
{
    /// <summary>
    /// Materials of Built-in shaders Unity's converter has no upgrader for (decision before M8 No. 2): each one moves to the
    /// nearest URP shader of <see cref="BuiltInShaderTable.ForMaterials"/> with its values, the Built-in property names
    /// carried to URP's (_MainTex -> _BaseMap, _Color -> _BaseColor ...), and URP's surface settings set as the Built-in
    /// shader rendered (transparent); URP's own material inspector then sets the keywords.
    /// </summary>
    static class BuiltInMaterialFix
    {
        public static List<KitConverterItem> Run(IEnumerable<Finding> findings)
        {
            var items = new List<KitConverterItem>();
            foreach (var f in findings.GroupBy(f => f.AssetPath).Select(g => g.First()))
            {
                if (!BuiltInShaderTable.ForMaterials.TryGetValue(f.Evidence ?? "", out var entry)) continue;
                var item = new KitConverterItem { Path = f.AssetPath, Converter = "Built-in shaders without an upgrader" };
                items.Add(item);
                if (!f.AssetPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                {
                    item.Outcome = "Info";
                    item.Notes.Add("Info: a material inside an imported model; left as it is.");
                    continue;
                }
                var mat = AssetDatabase.LoadAssetAtPath<Material>(f.AssetPath);
                var urp = Shader.Find(entry.Urp);
                if (mat == null || urp == null) { item.Outcome = "NotSupportedYet"; item.Notes.Add($"Not supported yet: {(mat == null ? "the material could not be loaded" : entry.Urp + " was not found")}."); continue; }
                if (mat.shader != null && mat.shader.name != f.Evidence) { item.Outcome = "Info"; item.Notes.Add($"Info: the material now uses {mat.shader.name}; left as it is."); continue; }
                var renames = new Dictionary<string, string>(BuiltInShaderTable.BaseRenames);
                foreach (var r in entry.Renames) renames[r.Key] = r.Value;
                var saved = new Dictionary<string, (Texture tex, Vector2 scale, Vector2 offset)>();
                var colors = new Dictionary<string, Color>();
                foreach (var r in renames)
                {
                    if (!mat.HasProperty(r.Key)) continue;
                    var idx = mat.shader.FindPropertyIndex(r.Key);
                    if (idx < 0) continue;
                    var type = mat.shader.GetPropertyType(idx);
                    if (type == ShaderPropertyType.Texture) saved[r.Value] = (mat.GetTexture(r.Key), mat.GetTextureScale(r.Key), mat.GetTextureOffset(r.Key));
                    else if (type == ShaderPropertyType.Color) colors[r.Value] = mat.GetColor(r.Key);
                }
                var from = mat.shader.name;
                mat.shader = urp;
                foreach (var t in saved) { if (!mat.HasProperty(t.Key)) continue; mat.SetTexture(t.Key, t.Value.tex); mat.SetTextureScale(t.Key, t.Value.scale); mat.SetTextureOffset(t.Key, t.Value.offset); }
                foreach (var c in colors) if (mat.HasProperty(c.Key)) mat.SetColor(c.Key, c.Value);
                if (entry.Transparent && mat.HasProperty("_Surface")) { mat.SetFloat("_Surface", 1); if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0); }
                // URP's inspector derives keywords, render queue and blend state from those properties.
                var editor = (MaterialEditor)Editor.CreateEditor(mat);
                try { editor.customShaderGUI?.ValidateMaterial(mat); }
                finally { UnityEngine.Object.DestroyImmediate(editor); }
                EditorUtility.SetDirty(mat);
                item.Outcome = entry.Note != null ? "AutoApprox" : "Auto";
                item.Notes.Add($"Info: {from} has no upgrader in Unity's converter; the material now uses {entry.Urp} with its values ({string.Join(", ", renames.Select(r => r.Key + " -> " + r.Value))}).");
                if (entry.Note != null) item.Notes.Add("Approx: " + entry.Note);
            }
            if (items.Count > 0) AssetDatabase.SaveAssets();
            return items;
        }
    }
}
