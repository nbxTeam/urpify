using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Urpify.Analysis;

namespace Urpify.Pipeline.AfterUnity
{
    /// <summary>
    /// Fixes for what Unity's converter leaves broken (TZ 6.11), measured on the lab and ShaderTutorials. The Scan step's
    /// report, taken before Unity's converter, says which materials and scenes are affected.
    /// - Standard in Transparent mode (lab STD04): Unity's converter sets URP Lit to Transparent with Premultiply blending
    ///   (_Blend 1) but not the keyword _ALPHAPREMULTIPLY_ON that makes the shader premultiply; the glass renders almost
    ///   opaque and white. The material is validated the way URP's material inspector does (its ShaderGUI.ValidateMaterial),
    ///   which sets the keywords from the properties.
    /// - Legacy Self-Illumin (lab LEG03): Built-in's emission is the albedo times the alpha of _Illum; Unity's converter
    ///   maps the material to Simple Lit, whose emission is its own texture, and the glow is gone. The material switches to
    ///   Urpify's URP Self-Illumin shader (ShaderSources/LegacySelfIllumin, stored as .txt, written on URP's ShaderLibrary only), with the
    ///   Built-in properties.
    /// - Scenes and prefabs that reference the Editor's built-in materials (ShaderTutorials 008-010, 044, 045, 052:
    ///   Default-Material on planes): the references point to the URP Asset's default materials instead.
    /// All of them wait for Unity's converter: while Built-in is still the active pipeline nothing is changed.
    /// </summary>
    public sealed class AfterUnityConverterFixes : IKitAssetConverter
    {
        public string Name => "Fixes after Unity's converter";
        public int Order => 100;

        public const string ShaderFolder = "Assets/Urpify/Shaders/";
        /// <summary>
        /// Urpify's own shader sources, stored as text so that neither Urpify nor the project compiles them where they
        /// sit; a visible folder, because a .unitypackage carries no '~' folder (QA-006).
        /// </summary>
        static string SourceFolder => KitPaths.In(KitPaths.Core, "ShaderSources/");

        /// <summary>True once Unity's converter has made URP the active pipeline; the tests replace it.</summary>
        public static Func<bool> UrpIsActive = () => GraphicsSettings.currentRenderPipeline != null;

        static readonly Dictionary<string, string> SelfIllumin = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Legacy Shaders/Self-Illumin/Diffuse"] = "Diffuse",
            ["Legacy Shaders/Self-Illumin/Bumped Diffuse"] = "BumpedDiffuse",
            ["Legacy Shaders/Self-Illumin/Specular"] = "Specular",
            ["Legacy Shaders/Self-Illumin/Bumped Specular"] = "BumpedSpecular",
        };

        public List<KitConverterItem> Run(MigrationReport scan)
        {
            var items = new List<KitConverterItem>();
            if (scan == null) return items;
            if (!UrpIsActive())
            {
                // Unity's converter has not switched the project to URP yet (Keep Built-in support runs this step with
                // Built-in active): the materials and scenes still render in Built-in, and these fixes would break that.
                var affected = scan.Findings.Where(f => f.RuleId == RuleIds.MaterialStandardTransparent || f.RuleId == RuleIds.MaterialSelfIllumin
                                                        || f.RuleId == RuleIds.BuiltInMaterialReference || f.RuleId == RuleIds.PackageMaterialReference
                                                        || f.RuleId == RuleIds.MaterialVariant && f.Tier == FixTier.Auto).Select(f => f.AssetPath).Distinct().Count();
                if (affected > 0)
                    items.Add(new KitConverterItem { Converter = Name, Outcome = "Info", Notes = { $"Info: Built-in is still the active render pipeline, so {affected} materials, scenes and prefabs stay as they are. These fixes follow Unity's converter: run Urpify converters again after it." } });
                return items;
            }
            foreach (var path in scan.Findings.Where(f => f.RuleId == RuleIds.MaterialStandardTransparent).Select(f => f.AssetPath).Distinct())
                items.Add(ValidateTransparent(path));
            foreach (var f in scan.Findings.Where(f => f.RuleId == RuleIds.MaterialSelfIllumin).GroupBy(f => f.AssetPath).Select(g => g.First()))
                items.Add(SwitchSelfIllumin(f.AssetPath, f.Evidence));
            var scenes = scan.Findings.Where(f => f.RuleId == RuleIds.BuiltInMaterialReference).Select(f => f.AssetPath).Distinct().ToList();
            if (scenes.Count > 0) items.AddRange(PointToUrpDefaults(scenes));
            var packageScenes = scan.Findings.Where(f => f.RuleId == RuleIds.PackageMaterialReference).Select(f => f.AssetPath).Distinct().ToList();
            if (packageScenes.Count > 0) items.AddRange(PointToPackageMaterialCopies(packageScenes));
            items.AddRange(PPv2BloomCalibration.Run());
            items.AddRange(MainLightFix.Run());
            items.AddRange(UrpAssetTexturesFix.Run());
            items.AddRange(BuiltInMaterialFix.Run(scan.Findings.Where(f => f.RuleId == RuleIds.MaterialBuiltInNoUpgrader && f.Tier == FixTier.Auto)));
            items.AddRange(MaterialVariantFix.Run(scan.Findings.Where(f => f.RuleId == RuleIds.MaterialVariant && f.Tier == FixTier.Auto)));
            return items;
        }

        /// <summary>Urpify's Self-Illumin shader file name for a Legacy Self-Illumin shader, or null.</summary>
        public static string SelfIlluminFile(string builtInShader) => builtInShader != null && SelfIllumin.TryGetValue(builtInShader, out var f) ? f : null;

        /// <summary>Folder of Urpify's Self-Illumin shaders in the project once a fix installed them.</summary>
        public const string SelfIlluminFolder = ShaderFolder + "LegacySelfIllumin/";

        /// <summary>Standard in Transparent mode after Unity's upgrader: Alpha blending with Preserve Specular, keywords set (also used by the package export).</summary>
        public static KitConverterItem FixStandardTransparent(string path) => ValidateTransparent(path);

        /// <summary>A Legacy Self-Illumin material after Unity's upgrader: Urpify's Self-Illumin shader (also used by the package export).</summary>
        public static KitConverterItem FixSelfIllumin(string path, string original) => SwitchSelfIllumin(path, original);

        static KitConverterItem ValidateTransparent(string path)
        {
            var item = new KitConverterItem { Path = path, Converter = "Standard Transparent" };
            if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                // Materials inside imported models are read-only; Unity's converter gives them their own setup (FBX, M6).
                item.Outcome = "Info";
                item.Notes.Add("Info: a material inside an imported model; left as Unity's converter set it.");
                return item;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) { item.Outcome = "NotSupportedYet"; item.Notes.Add("Not supported yet: the material could not be loaded."); return item; }
            var before = string.Join(" ", mat.shaderKeywords.OrderBy(k => k));
            // Built-in's Transparent mode multiplies the diffuse by alpha in the shader and keeps the specular: URP's Alpha
            // blending with Preserve Specular Lighting. URP's Premultiply, which Unity's converter picks, expects colors
            // already multiplied in the asset (BaseShaderGUI.SetupMaterialBlendModeInternal, URP 14.0.12 and 17.5.0).
            bool premultiply = mat.HasProperty("_Surface") && mat.GetFloat("_Surface") == 1 && mat.HasProperty("_Blend") && mat.GetFloat("_Blend") == 1;
            if (!premultiply)
            {
                item.Outcome = "Info";
                item.Notes.Add("Info: the blending is not Premultiply; left as Unity's converter set it.");
                return item;
            }
            {
                mat.SetFloat("_Blend", 0);
                if (mat.HasProperty("_BlendModePreserveSpecular")) mat.SetFloat("_BlendModePreserveSpecular", 1);
                item.Notes.Add("Info: blending Premultiply -> Alpha with Preserve Specular Lighting (Built-in's Transparent mode).");
            }
            var editor = (MaterialEditor)Editor.CreateEditor(mat);
            try
            {
                var gui = editor.customShaderGUI;
                if (gui == null) { item.Outcome = "NotSupportedYet"; item.Notes.Add($"Not supported yet: {mat.shader.name} has no material inspector to set its keywords."); return item; }
                gui.ValidateMaterial(mat);
            }
            finally { UnityEngine.Object.DestroyImmediate(editor); }
            EditorUtility.SetDirty(mat);
            var after = string.Join(" ", mat.shaderKeywords.OrderBy(k => k));
            item.Outcome = "Auto";
            item.Notes.Add($"Info: keywords set from the material's properties, as URP's inspector does: [{before}] -> [{after}].");
            return item;
        }

        static KitConverterItem SwitchSelfIllumin(string path, string original)
        {
            var item = new KitConverterItem { Path = path, Converter = "Legacy Self-Illumin" };
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || original == null || !SelfIllumin.TryGetValue(original, out var file))
            {
                item.Outcome = "NotSupportedYet";
                item.Notes.Add($"Not supported yet: {original ?? "this Self-Illumin shader"} has no replacement in Urpify; give the material an emission map that holds the albedo times the Illumin alpha.");
                return item;
            }
            var shader = InstallShader(file, item);
            if (shader == null) { item.Outcome = "NotSupportedYet"; return item; }
            // Unity's converter keeps the Built-in values next to the new ones (_MainTex, _Color, _Illum stay serialized).
            var keep = new Dictionary<string, object>();
            foreach (var p in new[] { "_MainTex", "_Illum", "_BumpMap" }) if (mat.HasProperty(p)) keep[p] = (mat.GetTexture(p), mat.GetTextureScale(p), mat.GetTextureOffset(p));
            mat.shader = shader;
            foreach (var kv in keep)
            {
                var (tex, scale, offset) = ((Texture, Vector2, Vector2))kv.Value;
                mat.SetTexture(kv.Key, tex); mat.SetTextureScale(kv.Key, scale); mat.SetTextureOffset(kv.Key, offset);
            }
            EditorUtility.SetDirty(mat);
            item.Outcome = "Auto";
            item.Notes.Add($"Info: switched from {original} (Simple Lit after Unity's converter) to {shader.name}: emission is the albedo times the Illumin alpha, as in Built-in.");
            return item;
        }

        /// <summary>The include every Self-Illumin shader uses, next to them in the project.</summary>
        const string SelfIlluminInclude = "UrpifyLegacySelfIllumin.hlsl";

        static Shader InstallShader(string file, KitConverterItem item)
        {
            var folder = ShaderFolder + "LegacySelfIllumin/";
            var dst = folder + file + ".shader";
            foreach (var name in new[] { SelfIlluminInclude, file + ".shader" })
            {
                var to = folder + name;
                if (File.Exists(to)) continue;
                Directory.CreateDirectory(folder);
                File.Copy(Path.GetFullPath(SourceFolder + "LegacySelfIllumin/" + name + ".txt"), to);
                File.WriteAllText(to + ".meta", Packaging.AssetGuids.DefaultMeta(Packaging.AssetGuids.Stable(to), Packaging.AssetGuids.ImporterFor(to)));
                AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceSynchronousImport);
            }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(dst);
            var errors = shader == null ? null : ShaderCompileCheck.Errors(shader, MigrationRunner.KeywordSets(shader));
            if (shader == null || errors.Count > 0) { item.Notes.Add("Not supported yet: Urpify's Self-Illumin shader did not compile: " + string.Join(" | ", (errors ?? new List<CompileMessage>()).Take(2))); return null; }
            return shader;
        }

        public const string MaterialFolder = "Assets/Urpify/Materials/";

        /// <summary>
        /// The Editor's built-in materials a scene or prefab can reference, with the URP shader Unity's material upgraders
        /// give their shaders. Default-Material is Standard, white, smoothness 0.5; URP's own default (Lit.mat) is 0.5 grey,
        /// so pointing to it darkened ShaderTutorials' floors (M6). Particle and line materials use the URP Asset's defaults.
        /// </summary>
        static readonly (string builtIn, string urpShader)[] BuiltInMaterials =
        {
            ("Default-Material.mat", "Universal Render Pipeline/Lit"),
            ("Default-Diffuse.mat", "Universal Render Pipeline/Simple Lit"),
            ("Default-Particle.mat", null),
            ("Default-Line.mat", null),
        };

        /// <summary>A URP material with the built-in material's color, texture and smoothness (created once per project).</summary>
        static Material UrpCopy(Material builtIn, string shaderName)
        {
            var path = MaterialFolder + builtIn.name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find(shaderName);
            if (shader == null) return null;
            var m = new Material(shader) { name = builtIn.name };
            if (builtIn.HasProperty("_Color")) m.SetColor("_BaseColor", builtIn.GetColor("_Color"));
            if (builtIn.HasProperty("_MainTex") && builtIn.GetTexture("_MainTex") != null) m.SetTexture("_BaseMap", builtIn.GetTexture("_MainTex"));
            if (builtIn.HasProperty("_Glossiness") && m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", builtIn.GetFloat("_Glossiness"));
            if (builtIn.HasProperty("_Metallic") && m.HasProperty("_Metallic")) m.SetFloat("_Metallic", builtIn.GetFloat("_Metallic"));
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Built-in material references in scene and prefab files, rewritten to URP materials with the same look.</summary>
        static List<KitConverterItem> PointToUrpDefaults(List<string> files)
        {
            var items = new List<KitConverterItem>();
            var rp = GraphicsSettings.currentRenderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (name, shader) in BuiltInMaterials)
            {
                var b = AssetDatabase.GetBuiltinExtraResource<Material>(name);
                if (b == null) continue;
                var urp = shader != null ? UrpCopy(b, shader)
                    : name == "Default-Particle.mat" ? rp?.defaultParticleMaterial : rp?.defaultLineMaterial;
                if (urp == null) continue;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(b, out string bg, out long bid)) continue;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(urp, out string ug, out long uid)) continue;
                map[$"{{fileID: {bid}, guid: {bg}, type: 0}}"] = $"{{fileID: {uid}, guid: {ug}, type: 2}}";
            }
            // Unity's converter, run with its search waited for (M6), points Default-Material to the URP Asset's default
            // material itself; in the files the Scan step found, that reference goes to the copy with Built-in's values too.
            var defaultCopy = BuiltInMaterials.Where(b => b.builtIn == "Default-Material.mat").Select(b => AssetDatabase.GetBuiltinExtraResource<Material>(b.builtIn))
                .Where(b => b != null).Select(b => AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + b.name + ".mat")).FirstOrDefault();
            if (rp?.defaultMaterial != null && defaultCopy != null
                && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(rp.defaultMaterial, out string dg, out long did)
                && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(defaultCopy, out string cg, out long cid))
                map[$"{{fileID: {did}, guid: {dg}, type: 2}}"] = $"{{fileID: {cid}, guid: {cg}, type: 2}}";
            AssetDatabase.SaveAssets();
            // A scene open in the Editor would be written back over the change: edit the files with none of them open.
            var setup = EditorSceneManager.GetSceneManagerSetup();
            bool reopen = setup.Any(s => files.Contains(s.path));
            var keptOpen = reopen ? CloseScenesForEdit(setup) : new HashSet<string>();
            try
            {
            foreach (var path in files)
            {
                var item = new KitConverterItem { Path = path, Converter = "Built-in material references" };
                items.Add(item);
                if (keptOpen.Contains(path)) { item.Outcome = "NotSupportedYet"; item.Notes.Add(UnsavedSceneNote); continue; }
                string text;
                try { text = File.ReadAllText(path); } catch (Exception e) { item.Outcome = "NotSupportedYet"; item.Notes.Add("Not supported yet: " + e.Message); continue; }
                int n = 0;
                foreach (var kv in map)
                {
                    int c = 0, at = 0;
                    while ((at = text.IndexOf(kv.Key, at, StringComparison.Ordinal)) >= 0) { c++; at += kv.Key.Length; }
                    if (c == 0) continue;
                    text = text.Replace(kv.Key, kv.Value);
                    n += c;
                }
                if (n == 0)
                {
                    item.Outcome = "NotSupportedYet";
                    item.Notes.Add($"Not supported yet: no reference Urpify can replace was found ({map.Count} built-in materials known); assign a URP material by hand.");
                    continue;
                }
                try { File.WriteAllText(path, text); }
                catch (Exception e) { item.Outcome = "NotSupportedYet"; item.Notes.Add("Not supported yet: could not write the file (" + e.Message + ")."); continue; }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                item.Outcome = "Auto";
                item.Notes.Add($"Info: {n} references to the Editor's built-in materials now point to URP materials with the same values ({MaterialFolder}).");
            }
            }
            finally
            {
                if (reopen && keptOpen.Count == 0)
                    try { EditorSceneManager.RestoreSceneManagerSetup(setup.Where(s => File.Exists(s.path)).ToArray()); } catch { }
            }
            return items;
        }

        const string UnsavedSceneNote = "Not supported yet: the scene is open with unsaved changes, which were not saved, so Urpify left the file as it is. Save or close the scene and run Urpify converters again.";

        /// <summary>
        /// Closes the open scenes so that their files can be edited (an open scene would be written back over the change).
        /// Unsaved changes are saved first if the user wants, never dropped (code review R-08, 18.10.2026): when the user
        /// declines, the scenes stay open as they are and their paths come back, so the caller leaves those files alone.
        /// </summary>
        public static HashSet<string> CloseScenesForEdit(SceneSetup[] setup)
        {
            var dirty = Enumerable.Range(0, EditorSceneManager.sceneCount).Select(EditorSceneManager.GetSceneAt).Where(s => s.isDirty).ToList();
            if (dirty.Count > 0)
            {
                bool saved = Application.isBatchMode ? EditorSceneManager.SaveScenes(dirty.ToArray()) : EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                if (!saved || Enumerable.Range(0, EditorSceneManager.sceneCount).Select(EditorSceneManager.GetSceneAt).Any(s => s.isDirty))
                    return new HashSet<string>(setup.Select(s => s.path), StringComparer.Ordinal);
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return new HashSet<string>(StringComparer.Ordinal);
        }

        static readonly System.Text.RegularExpressions.Regex MaterialRef =
            new System.Text.RegularExpressions.Regex(@"\{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: 2\}");

        /// <summary>
        /// References to materials inside packages whose shader URP cannot draw, rewritten to URP copies in the project
        /// (packages are read-only, so Unity's converter leaves them pink: ProBuilder 5's default material on smo-shaders).
        /// A built-in shader is upgraded by Unity's own upgrader; any other shader becomes URP Lit with the material's
        /// color, texture, smoothness and metallic.
        /// </summary>
        static List<KitConverterItem> PointToPackageMaterialCopies(List<string> files)
        {
            var items = new List<KitConverterItem>();
            var evaluator = new Scanner.ShaderUrpEvaluator();
            var copies = new Dictionary<string, (string reference, bool approx, string note)>(StringComparer.Ordinal);
            System.Collections.IList upgraders = null;
            (string reference, bool approx, string note)? CopyOf(string guid, string fileId)
            {
                var key = guid + ":" + fileId;
                if (copies.TryGetValue(key, out var known)) return known.reference == null ? ((string, bool, string)?)null : known;
                copies[key] = (null, false, null);
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Packages/", StringComparison.Ordinal) || !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) return null;
                var src = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (src == null || src.shader == null) return null;
                var o = evaluator.Evaluate(src.shader);
                if (o.Severity == Severity.Info) return null;
                var package = path.Split('/')[1];
                var dst = MaterialFolder + "Packages/" + package + "/" + src.name + ".mat";
                var copy = AssetDatabase.LoadAssetAtPath<Material>(dst);
                bool approx = !(o.BuiltIn && o.HasUnityUpgrader);
                if (copy == null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    if (!approx)
                    {
                        copy = new Material(src) { name = src.name };
                        AssetDatabase.CreateAsset(copy, dst);
                        upgraders = upgraders ?? UnityMaterialUpgraders.All();
                        if (upgraders != null) UnityMaterialUpgraders.Upgrade(upgraders, copy);
                        if (copy.shader == src.shader) { AssetDatabase.DeleteAsset(dst); return null; }
                    }
                    else
                    {
                        var lit = Shader.Find("Universal Render Pipeline/Lit");
                        if (lit == null) return null;
                        copy = new Material(lit) { name = src.name };
                        if (src.HasProperty("_Color")) copy.SetColor("_BaseColor", src.GetColor("_Color"));
                        if (src.HasProperty("_MainTex") && src.GetTexture("_MainTex") != null)
                        {
                            copy.SetTexture("_BaseMap", src.GetTexture("_MainTex"));
                            copy.SetTextureScale("_BaseMap", src.GetTextureScale("_MainTex"));
                            copy.SetTextureOffset("_BaseMap", src.GetTextureOffset("_MainTex"));
                        }
                        if (src.HasProperty("_Glossiness")) copy.SetFloat("_Smoothness", src.GetFloat("_Glossiness"));
                        if (src.HasProperty("_Metallic")) copy.SetFloat("_Metallic", src.GetFloat("_Metallic"));
                        AssetDatabase.CreateAsset(copy, dst);
                    }
                    EditorUtility.SetDirty(copy);
                }
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(copy, out string cg, out long cid)) return null;
                var note = approx
                    ? $"{path} ({o.ShaderName}) -> {dst}: URP Lit with its color, texture, smoothness and metallic; what the shader itself adds (vertex colors, its own effects) is not kept."
                    : $"{path} ({o.ShaderName}) -> {dst}: upgraded by Unity's own upgrader for {o.ShaderName}.";
                var r = ($"{{fileID: {cid}, guid: {cg}, type: 2}}", approx, note);
                copies[key] = r;
                return r;
            }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            bool reopen = setup.Any(s => files.Contains(s.path));
            var keptOpen = reopen ? CloseScenesForEdit(setup) : new HashSet<string>();
            try
            {
            foreach (var path in files)
            {
                var item = new KitConverterItem { Path = path, Converter = "Package material references" };
                items.Add(item);
                if (keptOpen.Contains(path)) { item.Outcome = "NotSupportedYet"; item.Notes.Add(UnsavedSceneNote); continue; }
                string text;
                try { text = File.ReadAllText(path); } catch (Exception e) { item.Outcome = "NotSupportedYet"; item.Notes.Add("Not supported yet: " + e.Message); continue; }
                int n = 0;
                bool approx = false;
                var notes = new HashSet<string>(StringComparer.Ordinal);
                text = MaterialRef.Replace(text, m =>
                {
                    var c = CopyOf(m.Groups[2].Value, m.Groups[1].Value);
                    if (c == null) return m.Value;
                    n++;
                    approx |= c.Value.approx;
                    notes.Add(c.Value.note);
                    return c.Value.reference;
                });
                AssetDatabase.SaveAssets();
                if (n == 0)
                {
                    item.Outcome = "NotSupportedYet";
                    item.Notes.Add("Not supported yet: no package material reference Urpify can replace was found; assign a URP material by hand.");
                    continue;
                }
                try { File.WriteAllText(path, text); }
                catch (Exception e) { item.Outcome = "NotSupportedYet"; item.Notes.Add("Not supported yet: could not write the file (" + e.Message + ")."); continue; }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                item.Outcome = approx ? "AutoApprox" : "Auto";
                item.Notes.Add($"{(approx ? "Approx" : "Info")}: {n} references to package materials URP cannot draw now point to URP copies in {MaterialFolder}Packages/ (packages are read-only, so Unity's converter leaves them as they are).");
                foreach (var note in notes) item.Notes.Add((approx ? "Approx: " : "Info: ") + note);
            }
            }
            finally
            {
                if (reopen && keptOpen.Count == 0)
                    try { EditorSceneManager.RestoreSceneManagerSetup(setup.Where(s => File.Exists(s.path)).ToArray()); } catch { }
            }
            return items;
        }
    }
}
