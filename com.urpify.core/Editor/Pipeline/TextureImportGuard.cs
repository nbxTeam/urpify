using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Urpify.Pipeline
{
    /// <summary>
    /// Keeps the user's texture import settings as they were (QA-035). Unity 2022.3 switches a colour texture to Normal map
    /// when it imports a material whose shader declares the texture's slot as a normal map ([Normal]): checked on 2022.3.62f3,
    /// saving or reimporting such a material is enough, assigning its shader or texture is not, and the internal
    /// BumpMapSettings.silentMode does not stop it; 6000.5.9f1 has no such check. A step that re-saves one such material (its
    /// shader converted or restored) then turned a shared texture red and white in every material that uses it as a colour.
    /// Urpify never means to change import settings, so after a modifying step every texture .meta the step changed is put
    /// back from the step's backup, byte for byte, and the texture is reimported. The step's report lists each one.
    /// </summary>
    public static class TextureImportGuard
    {
        public sealed class Kept
        {
            public string Texture = "";
            /// <summary>What Unity had changed, in words ("switched to Normal map").</summary>
            public string Change = "";
            /// <summary>A material whose normal-map slot uses the texture (the likely cause), or "".</summary>
            public string Material = "";

            public string Note => "Info: Unity " + Change + " while the step saved materials"
                + (Material.Length > 0 ? " (" + Material + " uses it in a normal-map slot)" : "")
                + ". Urpify put its import settings back, so the materials that use it as a colour texture look as before.";
        }

        /// <summary>
        /// Puts back the texture import settings a completed step changed. <paramref name="import"/> false: files only
        /// (tests without the AssetDatabase). The snapshot no longer lists the restored files as changed.
        /// </summary>
        public static List<Kept> Restore(BackupStore store, Snapshot snap, bool import = true)
        {
            var kept = new List<Kept>();
            if (snap == null || !snap.Complete) return kept;
            foreach (var c in snap.Changed.Where(c => c.Path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                try
                {
                    var copy = store.CopyIn(snap.Id, c.Path);
                    if (copy == null) continue;
                    var before = File.ReadAllText(copy);
                    if (!IsTextureMeta(before)) continue;
                    var abs = store.AbsolutePath(c.Path);
                    var now = File.Exists(abs) ? File.ReadAllText(abs) : "";
                    BackupStore.Retry(() => { File.Copy(copy, abs, true); return true; });
                    snap.Changed.Remove(c);
                    kept.Add(new Kept { Texture = c.Path.Substring(0, c.Path.Length - ".meta".Length), Change = Describe(before, now) });
                }
                catch (Exception e) { Debug.LogWarning("[Urpify] Could not put back the import settings of " + c.Path + ": " + e.Message); }
            }
            if (kept.Count == 0) return kept;
            store.Save(snap);
            if (!import) return kept;
            foreach (var k in kept)
            {
                try { AssetDatabase.ImportAsset(k.Texture, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate); }
                catch (Exception e) { Debug.LogWarning("[Urpify] Could not reimport " + k.Texture + ": " + e.Message); }
            }
            NameTheMaterials(kept);
            foreach (var k in kept) Debug.Log("[Urpify] " + k.Texture + ": " + k.Note);
            return kept;
        }

        /// <summary>A .meta of a texture: Unity writes the importer's name at the start of a line.</summary>
        public static bool IsTextureMeta(string meta) =>
            meta.IndexOf("\nTextureImporter:", StringComparison.Ordinal) >= 0 || meta.IndexOf("\nIHVImageFormatImporter:", StringComparison.Ordinal) >= 0;

        /// <summary>The change in words: the texture type and colour space are what break materials.</summary>
        public static string Describe(string before, string now)
        {
            var typeBefore = Field(before, "textureType");
            var typeNow = Field(now, "textureType");
            // TextureImporterType.NormalMap is 1 in the .meta file.
            if (typeNow == "1" && typeBefore != "1") return "switched this texture to Normal map";
            if (typeNow != typeBefore) return "changed this texture's type";
            if (Field(now, "sRGBTexture") != Field(before, "sRGBTexture")) return "changed this texture's colour space (sRGB)";
            return "changed this texture's import settings";
        }

        static string Field(string meta, string name)
        {
            foreach (var line in meta.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith(name + ":", StringComparison.Ordinal)) return t.Substring(name.Length + 1).Trim();
            }
            return null;
        }

        /// <summary>Finds, for each kept texture, a project material whose shader declares the slot that holds it [Normal].</summary>
        static void NameTheMaterials(List<Kept> kept)
        {
            try
            {
                var wanted = kept.ToDictionary(k => AssetDatabase.AssetPathToGUID(k.Texture), k => k);
                foreach (var path in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (m == null || m.shader == null) continue;
                    var s = m.shader;
                    for (int i = 0; i < s.GetPropertyCount(); i++)
                    {
                        if (s.GetPropertyType(i) != ShaderPropertyType.Texture || (s.GetPropertyFlags(i) & ShaderPropertyFlags.Normal) == 0) continue;
                        var tex = m.GetTexture(s.GetPropertyName(i));
                        if (tex == null) continue;
                        if (wanted.TryGetValue(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tex)), out var k) && k.Material.Length == 0)
                            k.Material = path + ", " + s.GetPropertyName(i);
                    }
                    if (wanted.Values.All(k => k.Material.Length > 0)) return;
                }
            }
            catch (Exception e) { Debug.LogWarning("[Urpify] Could not find the material behind a texture import change: " + e.Message); }
        }
    }
}
