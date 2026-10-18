using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Urpify.Analysis;
using Urpify.Generation;
using Urpify.Parsing;
using Urpify.Verify;

namespace Urpify.Pipeline
{
    /// <summary>What a batch conversion did: per shader, the files it wrote and the URP settings they need.</summary>
    public sealed class ShaderBatchResult
    {
        public KitConverterReport Report = new KitConverterReport();
        public HashSet<string> Requirements = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Shaders rewritten and kept (they compiled).</summary>
        public List<string> Written = new List<string>();
        /// <summary>Include-file copies that stayed in the project (a shader that compiled uses them).</summary>
        public List<string> Copies = new List<string>();

        /// <summary>How to undo one written shader (QA-031: a conversion the self-check sees pink goes back).</summary>
        internal sealed class Undo
        {
            public string Original, OriginalText;
            /// <summary>The original's bytes: written back as they were, whatever the file's encoding (code review R-03).</summary>
            public byte[] OriginalBytes;
            public List<string> Created = new List<string>();
            public List<string> Files = new List<string>();
            public List<UnityEngine.Material> Moved = new List<UnityEngine.Material>();
        }
        /// <summary>Written shader path -> how to undo it.</summary>
        internal readonly Dictionary<string, Undo> Undos = new Dictionary<string, Undo>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts a list of project shaders with the Pro converters (TZ 6.1): each shader a converter handles is rewritten
    /// in place, imported and compiled with the keywords its materials use; a shader that does not compile is restored
    /// and reported for manual work with the compiler's message. Used by the Urpify converters step and by the
    /// .unitypackage export. The caller backs the files up.
    /// </summary>
    public static class ShaderBatchConverter
    {
        public static ShaderBatchResult Run(IList<string> shaderPaths, IList<Material> materials, ShaderOutput output,
            Action<string, float> progress = null)
        {
            var batch = new ShaderBatchResult();
            var converters = MigrationRunner.ShaderConverters();
            batch.Report.Output = output.ToString();
            var copies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // include-file copy -> shaders using it
            var previous = ConversionOptions.ScreenEffectShader;
            ConversionOptions.ScreenEffectShader = ConversionOptions.ScreenEffectsAmong(
                new HashSet<string>(materials.Where(m => m != null && m.shader != null).Select(m => m.shader.name), StringComparer.Ordinal));
            try
            {
                // Every shader's text as it was before this step wrote anything: UsePass reads the original of a shader converted
                // earlier in the step (Urpify.Generation.OriginalShaderTexts).
                foreach (var p in shaderPaths)
                    try { if (File.Exists(p)) Generation.OriginalShaderTexts.Set(p, SourceFiles.Read(p)); } catch { }
                var check = SelfCheck && GraphicsSettings.currentRenderPipeline != null ? RenderOriginals(shaderPaths, materials, converters) : null;
                ConvertAll(shaderPaths, materials, output, progress, converters, batch, copies);
                if (check != null) CheckConverted(batch, check, copies);
            }
            finally { ConversionOptions.ScreenEffectShader = previous; Generation.OriginalShaderTexts.Clear(); }
            batch.Copies.AddRange(copies.Keys.OrderBy(p => p, StringComparer.Ordinal));
            return batch;
        }

        // ------------------------------------------------------------------ self-check (QA-004)

        /// <summary>Render each converted shader in Built-in and URP and compare (on by default; tests turn it off).</summary>
        public static bool SelfCheck = true;
        /// <summary>Development aid: a folder where a converted shader that did not compile is saved before it is restored (null: not saved).</summary>
        public static string DumpRestoredTo;
        public const string ReviewOutcome = "LooksDifferent";
        public const string SelfCheckFolder = "Urpify/Reports/self-check";

        sealed class Original
        {
            public List<Material> Materials = new List<Material>();
            public List<Material> Temporary = new List<Material>();
            public ShaderSelfCheck.Shots BuiltIn;
            /// <summary>A surface shader with finalcolor: rendered with one light (ShaderSelfCheck.Render).</summary>
            public bool SingleLight;
            /// <summary>Why the self-check does not run for this shader; empty when it runs.</summary>
            public string Skip = "";
            /// <summary>Rendered with a camera of type Game (<see cref="SelfCheckGameCamera"/>).</summary>
            public bool GameCamera;
        }

        sealed class Originals
        {
            public readonly Dictionary<string, Original> ByPath = new Dictionary<string, Original>(StringComparer.OrdinalIgnoreCase);
            public string Failure = "";
        }

        /// <summary>
        /// Why a sphere with the shader proves nothing, or "" when the self-check renders it. A screen effect without a material
        /// runs on the whole picture. A shader under Hidden/ is an internal one that scripts draw with their own matrices and
        /// textures (Unity leaves Hidden/ shaders out of the material's shader menu): Amplify Motion's ClothVectors and
        /// SkinnedVectors multiply the vertices by a matrix only their script sets, so in a scene they draw nothing in either
        /// pipeline, while a preview drew a sphere in Built-in, and the pair was marked different.
        /// </summary>
        public static string SelfCheckSkip(string shaderName, bool screenEffect, int materialCount)
        {
            if (screenEffect && materialCount == 0) return "a screen effect: scripts run it on the whole picture, not on an object";
            if ((shaderName ?? "").StartsWith("Hidden/", StringComparison.Ordinal)) return "an internal shader (Hidden/): scripts draw it, not a material on an object";
            return "";
        }

        /// <summary>
        /// The self-check renders this shader with a camera of type Game. URP 14 never copies the Opaque Texture for a preview
        /// camera (UniversalRenderer.cs, URP 14.0.12: "copyColorPass &amp;= !isPreviewCamera"), so a converted GrabPass read
        /// nothing there and lab G01 was marked different on 2022.3; a game camera gets it (architect's decision 3,
        /// 2026-10-12; SelfCheck_GameCameraGetsTheOpaqueTexture: 48 % of the sphere right with the preview camera, 97.5 %
        /// with a game camera). URP 17 gives the preview camera the texture, so Unity 6 keeps the preview camera.
        /// </summary>
        public static bool SelfCheckGameCamera(bool grabPass)
        {
#if UNITY_6000_0_OR_NEWER
            return false;
#else
            return grabPass;
#endif
        }

        /// <summary>
        /// Before anything is converted: every shader a converter will take, rendered in Built-in with its own materials
        /// (the first <see cref="ShaderSelfCheck.MaxMaterials"/> by path, or one with its defaults), all in one pipeline switch.
        /// </summary>
        static Originals RenderOriginals(IList<string> shaderPaths, IList<Material> materials, List<IShaderFileConverter> converters)
        {
            var o = new Originals();
            foreach (var path in shaderPaths)
            {
                ShaderDocument doc;
                string text;
                try { text = SourceFiles.Read(path); doc = ShaderLabParser.Parse(text, path); } catch { continue; }
                if (!converters.Any(c => { try { return c.Handles(doc); } catch { return false; } })) continue;
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                var entry = new Original { SingleLight = ShaderSelfCheck.HasFinalColor(text) };
                o.ByPath[path] = entry;
                if (shader == null) { entry.Skip = "the original shader did not load"; continue; }
                bool screenEffect = doc.SubShaders.Where(s => s.RenderPipeline.Length == 0).All(s => s.IsBlitShader) || ConversionOptions.ScreenEffectShader(doc.ShaderName);
                entry.Materials = materials.Where(m => m != null && m.shader == shader).OrderBy(m => AssetDatabase.GetAssetPath(m), StringComparer.Ordinal)
                    .Take(ShaderSelfCheck.MaxMaterials).ToList();
                bool grabPass = doc.SubShaders.Where(s => s.RenderPipeline.Length == 0).Any(s => s.Passes.Any(p => p.Kind == PassKind.GrabPass));
                entry.Skip = SelfCheckSkip(doc.ShaderName, screenEffect, entry.Materials.Count);
                entry.GameCamera = SelfCheckGameCamera(grabPass);
                if (entry.Skip.Length > 0) continue;
                if (entry.Materials.Count == 0)
                {
                    var temp = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    entry.Materials.Add(temp);
                    entry.Temporary.Add(temp);
                }
            }
            var todo = o.ByPath.Values.Where(e => e.Skip.Length == 0).ToList();
            if (todo.Count == 0) return o;
            StepProgress.Report($"self-check: {todo.Count} shaders in Built-in");
            BuiltInRender.Run(() => { foreach (var e in todo) e.BuiltIn = ShaderSelfCheck.Render(e.Materials, e.SingleLight, e.GameCamera); }, out o.Failure);
            return o;
        }

        /// <summary>
        /// After the conversion: each written shader with the same materials in URP, against its Built-in render. A shader
        /// that looks different is still written (the converter did what it could), but its outcome is LooksDifferent, not
        /// Auto, and the pair of images is kept for the report. A shader the check could not render is at most AutoApprox.
        /// </summary>
        static void CheckConverted(ShaderBatchResult batch, Originals o, Dictionary<string, int> copies)
        {
            foreach (var item in batch.Report.Items.Where(i => i.Outcome == "Auto" || i.Outcome == "AutoApprox"))
            {
                var key = o.ByPath.ContainsKey(item.Path) ? item.Path : o.ByPath.Keys.FirstOrDefault(k => CopyPath(k) == item.Path);
                if (key == null) continue;
                var e = o.ByPath[key];
                string skip = e.Skip.Length > 0 ? e.Skip : o.Failure.Length > 0 ? "Built-in could not render: " + o.Failure : e.BuiltIn == null ? "no Built-in render" : "";
                if (skip.Length > 0)
                {
                    if (item.Outcome == "Auto") item.Outcome = "AutoApprox";
                    item.Notes.Add("Info: not checked by a render (" + skip + "). Check it in Compare scenes.");
                    continue;
                }
                StepProgress.Report("self-check: " + item.Path);
                ShaderSelfCheck.Shots urp;
                try { urp = ShaderSelfCheck.Render(e.Materials, e.SingleLight, e.GameCamera); }
                catch (Exception ex) { if (item.Outcome == "Auto") item.Outcome = "AutoApprox"; item.Notes.Add("Info: not checked by a render (" + ex.Message + ")."); continue; }
                var results = e.BuiltIn.Images.Zip(urp.Images, (a, b) => ShaderSelfCheck.Compare(e.BuiltIn.Reference, a, urp.Reference, b)).ToList();
                var worst = ShaderSelfCheck.Worst(results);
                int at = results.IndexOf(worst);
                var image = SelfCheckFolder + "/" + System.Text.RegularExpressions.Regex.Replace(item.Path.Replace("Assets/", ""), "[^A-Za-z0-9_.-]", "_") + ".png";
                try { ShaderSelfCheck.SavePair(e.BuiltIn.Images[at], urp.Images[at], image); } catch { image = ""; }
                var pct = worst.ChangedPct.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                // A shader that reads the time shows another moment in each render (Built-in before the conversion, URP
                // after it): VoxelWave and the palm leaves differed by 16 to 82 % of the sphere while their scenes were the
                // same. Only what no moment explains counts for it: pink, or one render going dark. A small difference does
                // not prove it right either, so it is not Auto.
                if (worst.PinkPct <= 0.5 && !worst.WentDark && worst.HolePct <= 50 && ShaderSelfCheck.UsesTime(item.Path))
                {
                    if (item.Outcome == "Auto") item.Outcome = "AutoApprox";
                    item.Notes.Add($"Info: animated (reads the time), so the test sphere shows another moment in Built-in and URP ({pct} % changed, Built-in | URP: {image}) and the render does not decide. Check it in Compare scenes, which captures with a fixed time.");
                    continue;
                }
                // A conversion that renders pink is a broken result, not one to review: the original goes back (QA-031; architect's
                // decision 4 of 2026-10-12: a pink result of a conversion is a defect of the product even when the self-check caught it).
                if (worst.PinkPct > 0.5 && batch.Undos.TryGetValue(item.Path, out var undo))
                {
                    UndoShader(batch, undo, item.Path, copies);
                    item.Outcome = "Restored";
                    item.Notes.Add($"Not supported yet: the converted shader renders pink in URP on a test sphere (Built-in | URP: {image}), so the original is back.");
                    continue;
                }
                if (worst.LooksDifferent)
                {
                    item.Outcome = ReviewOutcome;
                    item.Notes.Add(worst.PinkPct > 0.5
                        ? $"Review: converted, but it renders pink in URP on a test sphere (Built-in | URP: {image}). Check the material in its scene."
                        : worst.HolePct > 50
                        ? $"Review: converted, but it writes depth and no colour, and what shows through it differs in URP: the camera's Background colour instead of black, and other objects in a different draw order (Built-in | URP: {image}). Check the material in its scene."
                        : $"Review: converted, but it looks different from the original on a test sphere: {pct} % of the sphere changed (Built-in | URP: {image}). Check the material in its scene.");
                }
                else item.Notes.Add($"Info: checked by a render, {pct} % of a test sphere changed (Built-in | URP: {image}).");
            }
            foreach (var e in o.ByPath.Values) foreach (var t in e.Temporary) if (t != null) UnityEngine.Object.DestroyImmediate(t);
        }

        /// <summary>Puts the original shader back after it was written: its text, its materials, and the files only it created.</summary>
        static void UndoShader(ShaderBatchResult batch, ShaderBatchResult.Undo undo, string target, Dictionary<string, int> copies)
        {
            if (target != undo.Original)
            {
                var original = AssetDatabase.LoadAssetAtPath<Shader>(undo.Original);
                foreach (var m in undo.Moved.Where(m => m != null)) { m.shader = original; EditorUtility.SetDirty(m); }
                AssetDatabase.DeleteAsset(target);
            }
            else
            {
                File.WriteAllBytes(undo.Original, undo.OriginalBytes);
                AssetDatabase.ImportAsset(undo.Original, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            foreach (var f in undo.Files)
                if (copies.TryGetValue(f, out var n)) { if (n <= 1) copies.Remove(f); else copies[f] = n - 1; }
            foreach (var c in undo.Created.Where(c => !copies.ContainsKey(c))) AssetDatabase.DeleteAsset(c);
            batch.Written.Remove(target);
            batch.Undos.Remove(target);
        }

        static void ConvertAll(IList<string> shaderPaths, IList<Material> materials, ShaderOutput output, Action<string, float> progress,
            List<IShaderFileConverter> converters, ShaderBatchResult batch, Dictionary<string, int> copies)
        {
            for (int k = 0; k < shaderPaths.Count; k++)
            {
                var path = shaderPaths[k];
                progress?.Invoke(path, (float)k / Math.Max(1, shaderPaths.Count));
                StepProgress.Report(path, k, shaderPaths.Count);
                string text;
                byte[] bytes;
                SourceFiles.Kind kind;
                // Read as SourceFiles does: a shader that is not UTF-8 keeps its bytes outside the code a converter changes (R-03).
                try { bytes = File.ReadAllBytes(path); text = SourceFiles.Decode(bytes, out kind); } catch { continue; }
                int before = batch.Report.Items.Count;
                try { ConvertOne(path, text, bytes, kind, converters, materials, output, batch, copies); }
                catch (Exception e)
                {
                    // One file must not end the step (QA-012): the original goes back, the file is reported, the rest go on.
                    var item = batch.Report.Items.Count > before ? batch.Report.Items[batch.Report.Items.Count - 1] : null;
                    if (item == null) { item = new KitConverterItem { Path = path, Converter = "Shaders" }; batch.Report.Items.Add(item); }
                    string back = "";
                    try
                    {
                        if (File.Exists(path) && !File.ReadAllBytes(path).SequenceEqual(bytes)) { File.WriteAllBytes(path, bytes); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
                    }
                    catch (Exception e2) { back = " The original could not be written back either: " + e2.Message; }
                    batch.Written.Remove(path);
                    item.Outcome = "Manual";
                    item.Notes.Add($"Manual: the file could not be changed ({e.GetType().Name}: {e.Message}). It may be read-only or open in another program; make it writable and run the step again.{back}");
                }
            }
        }

        /// <summary>Converts one shader file; throws only for a file the step cannot change (read-only, locked).</summary>
        static void ConvertOne(string path, string text, byte[] bytes, SourceFiles.Kind kind, List<IShaderFileConverter> converters, IList<Material> materials,
            ShaderOutput output, ShaderBatchResult batch, Dictionary<string, int> copies)
        {
            ShaderDocument doc;
            try { doc = ShaderLabParser.Parse(text, path); } catch { return; }
            var conv = converters.FirstOrDefault(c => c.Handles(doc));
            if (conv == null)
            {
                // A shader URP draws as it is may still read the depth or opaque texture (lab D01).
                try { foreach (var r in UrpAssetSettings.NeedsOf(doc)) batch.Requirements.Add(r); } catch { }
                return;
            }
            var item = new KitConverterItem { Path = path, Converter = conv.Name };
            batch.Report.Items.Add(item);
            // A shader Unity cannot compile before the migration is the project's to fix first (group A of Manual), as the
            // scan says; converting it only adds Urpify's name to the author's errors (QA-011).
            var before = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (before != null && ShaderUtil.ShaderHasError(before))
            {
                var first = ShaderUtil.GetShaderMessages(before).FirstOrDefault(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error);
                item.Outcome = "Manual";
                item.Notes.Add("Manual: the shader does not compile before the migration either" + (first.message != null ? $" ({first.message.Trim()}, line {first.line})" : "") + ". Fix it in Built-in first, then run the step again.");
                return;
            }
            ShaderConversion result;
            try { result = conv.Convert(text, path, output); }
            catch (Exception e) { result = new ShaderConversion(); result.Add(NoteKind.NotSupported, 0, "Converter error: " + e.Message); }
            item.Notes = result.Notes.Select(n => n.ToString()).ToList();
            if (!result.Converted) { item.Outcome = result.Tier.ToString(); return; }
            // A file the step cannot write is found before anything of this shader changes.
            var target = output == ShaderOutput.WriteCopy ? CopyPath(path) : path;
            foreach (var p in result.Files.Select(f => f.Path).Append(target))
                if (File.Exists(p) && (File.GetAttributes(p) & FileAttributes.ReadOnly) != 0)
                    throw new UnauthorizedAccessException($"{p} is read-only");
            // Keep Built-in support leaves the Built-in SubShaders as they are, errors included: only new errors count.
            List<CompileMessage> originalErrors = null;
            if (output == ShaderOutput.KeepBuiltInSupport)
            {
                var original = AssetDatabase.LoadAssetAtPath<Shader>(path);
                originalErrors = original != null ? ShaderCompileCheck.Errors(original, KeywordSets(original, materials)) : new List<CompileMessage>();
            }
            var created = new List<string>();
            foreach (var f in result.Files)
            {
                if (!File.Exists(f.Path)) created.Add(f.Path);
                // A copy is written the way its original include file is (R-03).
                if (!File.Exists(f.Path) || SourceFiles.Read(f.Path) != f.Content) SourceFiles.WriteLike(f.Path, f.Content, f.Source.Length > 0 ? f.Source : path);
                AssetDatabase.ImportAsset(f.Path, ImportAssetOptions.ForceSynchronousImport);
            }
            // Output = Write copy (TZ 4.3, architect decision 4 after M5): the converted shader goes next to the original as
            // <Name>_URP.shader, named "<Name> (URP)", and the materials move to it; the original stays as it is.
            var written = output == ShaderOutput.WriteCopy ? CopyNames.CopyHeader + Path.GetFileName(path) + ".\n" + CopyName(result.Output) : result.Output;
            if (output == ShaderOutput.WriteCopy && !File.Exists(target)) created.Add(target);
            SourceFiles.Write(target, written, kind);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(target);
            // Every SubShader is compiled, so with Keep Built-in support the Built-in part is checked too.
            var errors = shader == null ? new List<CompileMessage> { new CompileMessage { Message = "The converted shader did not import." } }
                : ShaderCompileCheck.Errors(shader, KeywordSets(shader, materials));
            if (originalErrors != null && shader != null)
            {
                var kept = errors.Count - (errors = ShaderCompileCheck.NewErrors(errors, originalErrors, ShaderCompileCheck.LeadingUrpSubShaders(result.Output, path))).Count;
                if (kept > 0) item.Notes.Add($"Info: {kept} compiler errors of the original Built-in SubShader are left as they are (Keep Built-in support does not change it).");
            }
            if (errors.Count > 0)
            {
                if (DumpRestoredTo != null)
                    try { Directory.CreateDirectory(DumpRestoredTo); File.WriteAllText(Path.Combine(DumpRestoredTo, Path.GetFileName(target) + ".txt"), written + "\n// " + string.Join("\n// ", errors)); } catch { }
                if (target != path) AssetDatabase.DeleteAsset(target);
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                foreach (var c in created.Where(c => !copies.ContainsKey(c))) AssetDatabase.DeleteAsset(c);
                item.Outcome = "Restored";
                item.Notes.Add("Not supported yet: the converted shader did not compile, the original is back. " + string.Join(" | ", errors.Take(3)));
                return;
            }
            item.Outcome = result.Tier.ToString();
            batch.Written.Add(target);
            var undo = new ShaderBatchResult.Undo { Original = path, OriginalText = text, OriginalBytes = bytes, Created = created, Files = result.Files.Select(f => f.Path).ToList() };
            batch.Undos[target] = undo;
            if (target != path)
            {
                var original = AssetDatabase.LoadAssetAtPath<Shader>(path);
                int moved = 0;
                foreach (var m in materials.Where(m => m != null && m.shader == original)) { m.shader = shader; EditorUtility.SetDirty(m); moved++; undo.Moved.Add(m); }
                item.Path = target;
                item.Notes.Add($"Info: written as a copy of {path} ({shader.name}); {moved} materials moved to it, the original is unchanged (Output: Write copy).");
            }
            foreach (var f in result.Files) copies[f.Path] = copies.TryGetValue(f.Path, out var n) ? n + 1 : 1;
            foreach (var r in result.Requires) batch.Requirements.Add(r);
        }

        /// <summary>
        /// Where Output = Write copy puts the converted shader: Folder/Name.shader -> Folder/Name_URP.shader, or Name_URP2.shader
        /// and on when the project already has a Name_URP.shader that Urpify did not write (code review R-14: a vendor's own
        /// URP version was overwritten, and deleted when the conversion was undone).
        /// </summary>
        public static string CopyPath(string path) => CopyNames.With(path.Replace('\\', '/'), CopyNames.FreeSuffix(path));

        static readonly System.Text.RegularExpressions.Regex ShaderName = new System.Text.RegularExpressions.Regex("(?m)^(\\s*Shader\\s*\")([^\"]+)(\")");

        /// <summary>The copy's own name, "Name (URP)", so both shaders exist side by side.</summary>
        public static string CopyName(string text) => ShaderName.Replace(text, m => m.Groups[1].Value + m.Groups[2].Value + " (URP)" + m.Groups[3].Value, 1);

        /// <summary>Keyword sets to compile a shader with: none, the common lighting keywords, and each project material's.</summary>
        public static List<string[]> KeywordSets(Shader shader) =>
            KeywordSets(shader, AssetDatabase.FindAssets("t:Material", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m => m != null && m.shader == shader).ToList());

        /// <summary>Keyword sets to compile a converted shader with: its materials' keywords, alone and with URP's lighting keywords.</summary>
        public static List<string[]> KeywordSets(Shader shader, IList<Material> materials)
        {
            var lighting = new[] { "_MAIN_LIGHT_SHADOWS", "_ADDITIONAL_LIGHTS", "FOG_LINEAR" };
            var sets = new List<string[]> { new string[0], lighting };
            foreach (var m in materials.Where(m => m != null && m.shader == shader))
            {
                var kw = m.shaderKeywords ?? new string[0];
                if (kw.Length == 0) continue;
                sets.Add(kw);
                sets.Add(kw.Concat(lighting).ToArray());
            }
            return sets.Select(x => x.OrderBy(k => k, StringComparer.Ordinal).ToArray())
                .GroupBy(x => string.Join(" ", x)).Select(g => g.First()).Take(10).ToList();
        }
    }
}
