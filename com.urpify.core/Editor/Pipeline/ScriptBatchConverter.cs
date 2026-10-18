using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Urpify.Generation;

namespace Urpify.Pipeline
{
    /// <summary>What the script part of the Urpify converters step wrote: rewritten scripts with their original text.</summary>
    [Serializable]
    public sealed class ScriptBatchResult
    {
        public List<string> Paths = new List<string>();
        /// <summary>Each rewritten script's bytes before the step: "the original is back" writes them as they were (code review R-03).</summary>
        [NonSerialized] public Dictionary<string, byte[]> Originals = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        /// <summary>Runtime files copied into the project for the converted scripts.</summary>
        public List<string> RuntimeFiles = new List<string>();
        /// <summary>
        /// Scripts that work only together: an effect and the scripts whose drawing its OnRenderImage reaches. When one of them
        /// is restored, the others are too (an effect run under URP whose Blit calls in another file were not rewritten
        /// blacks out the frame, QA-030).
        /// </summary>
        [NonSerialized] public Dictionary<string, HashSet<string>> Coupled = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        /// <summary>The rewritten scripts to restore with <paramref name="paths"/>: they and their coupled scripts, at any depth.</summary>
        public List<string> WithCoupled(IEnumerable<string> paths)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>(paths);
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                if (!set.Add(p)) continue;
                if (Coupled.TryGetValue(p, out var more)) foreach (var m in more) stack.Push(m);
            }
            return set.Where(Paths.Contains).ToList();
        }
    }

    /// <summary>
    /// Rewrites the project's run-time scripts with the script converters (TZ 6.7, 6.8, 6.11: the Free fix for Shader.Find,
    /// then the Pro converters) and copies the runtime
    /// files they need. Unity then compiles; a script whose rewrite does not compile is restored (see
    /// <see cref="MigrationRunner"/>), so the step never leaves the project with compiler errors it did not have.
    /// </summary>
    public static class ScriptBatchConverter
    {
        public static List<IScriptFileConverter> Converters() =>
            TypeCache.GetTypesDerivedFrom<IScriptFileConverter>().Where(t => !t.IsAbstract && !t.IsInterface && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => (IScriptFileConverter)Activator.CreateInstance(t)).OrderBy(c => c.Order).ToList();

        public static bool IsScript(string path) => (path ?? "").EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

        public static ScriptBatchResult Run(KitConverterReport report)
        {
            var batch = new ScriptBatchResult();
            var converters = Converters();
            if (converters.Count == 0) return batch;
            var scripts = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !PackageScopePaths.IsInside(p) && !KitPaths.IsKitFile(p)).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
            var used = new HashSet<IScriptFileConverter>();
            // Read as SourceFiles does, so a script that is not UTF-8 (a Windows-1251 string, a Shift-JIS comment) is written back
            // with only the code a converter changed (code review R-03).
            var kinds = new Dictionary<string, SourceFiles.Kind>(StringComparer.Ordinal);
            var texts = scripts.Select(p => { try { var t = SourceFiles.Read(p, out var k); kinds[p] = k; return (p, t); } catch { return (p, (string)null); } }).ToList();
            var fed = Analysis.LightBufferFeeds.Find(texts);
            var reach = Analysis.ImageEffectReach.Find(texts, ProjectShaderTexts());
            foreach (var (path, text) in texts)
            {
                if (text == null) continue;
                var (item, result, applied) = ConvertText(path, text, converters, fed, reach);
                if (item == null) continue;
                report.Items.Add(item);
                if (result == null || !result.Converted) continue;
                // One file the step cannot write is reported; the others go on (QA-012).
                try
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0) throw new UnauthorizedAccessException(path + " is read-only");
                    var original = File.ReadAllBytes(path);
                    SourceFiles.Write(path, result.Output, kinds[path]);
                    batch.Originals[path] = original;
                }
                catch (Exception e)
                {
                    item.Outcome = "Manual";
                    item.Notes.Add($"Manual: the file could not be changed ({e.GetType().Name}: {e.Message}). It may be read-only or open in another program; make it writable and run the step again.");
                    continue;
                }
                item.Outcome = result.Tier.ToString();
                batch.Paths.Add(path);
                foreach (var conv in applied) used.Add(conv);
            }
            // An effect and the scripts its drawing is in stand or fall together.
            foreach (var kv in reach.Where(r => r.Value.Blocker.Length == 0 && r.Value.Helpers.Count > 0 && batch.Paths.Contains(r.Key)))
            {
                var missing = kv.Value.Helpers.Where(h => !batch.Paths.Contains(h)).ToList();
                if (missing.Count > 0)
                {
                    Restore(batch, report, kv.Key, $"Not supported yet: OnRenderImage draws in {string.Join(", ", missing.Select(Path.GetFileName))}, which could not be changed, "
                        + "so the effect is left as it is: under URP it is missing and the rest of the frame is drawn.");
                    continue;
                }
                foreach (var h in kv.Value.Helpers)
                {
                    Couple(batch, kv.Key, h);
                    Couple(batch, h, kv.Key);
                }
            }
            foreach (var conv in converters.Where(c => c.NeedsRuntime(report.Requirements))) used.Add(conv);
            foreach (var conv in used)
                foreach (var file in conv.RuntimeFiles())
                {
                    MigrationRunner.InstallRuntimeFile(file.Key, file.Value, import: false);
                    batch.RuntimeFiles.Add(file.Value);
                }
            return batch;
        }

        static void Couple(ScriptBatchResult batch, string a, string b)
        {
            if (!batch.Coupled.TryGetValue(a, out var set)) batch.Coupled[a] = set = new HashSet<string>(StringComparer.Ordinal);
            set.Add(b);
        }

        static void Restore(ScriptBatchResult batch, KitConverterReport report, string path, string note)
        {
            if (!batch.Originals.TryGetValue(path, out var bytes)) return;
            try { File.WriteAllBytes(path, bytes); } catch (Exception e) { note += " (" + e.Message + ")"; }
            batch.Paths.Remove(path);
            var item = report.Items.LastOrDefault(i => i.Path == path);
            if (item == null) return;
            item.Outcome = "NotSupportedYet";
            item.Notes.Add(note);
        }

        /// <summary>The project's shaders (path, text), for what effects read (<see cref="Analysis.ImageEffectReach"/>).</summary>
        public static List<(string path, string text)> ProjectShaderTexts()
        {
            var list = new List<(string, string)>();
            foreach (var p in AssetDatabase.FindAssets("t:Shader", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                         .Where(p => p.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)).Distinct())
            {
                try { list.Add((p, File.ReadAllText(p))); } catch { }
            }
            return list;
        }

        /// <summary>The note of an effect left as it is because of what its OnRenderImage reaches (QA-025, QA-029).</summary>
        public static string EffectLeftAsIs(Analysis.EffectReach r) => r.GBuffer
            ? $"Not supported yet: this effect's shader {r.Blocker} from Built-in's deferred path, which URP does not provide. Run under URP it would draw "
              + "from empty textures and distort the frame instead of adding the effect, so it is left as it is: under URP the effect is missing and the rest of the frame is drawn."
            : $"Not supported yet: OnRenderImage reaches {r.Blocker}, which runs at once and cannot be recorded into URP's frame. Converted, the effect would "
              + "black out or blank the frame, so it is left as it is: under URP the effect is missing and the rest of the frame is drawn. To convert it, move that call "
              + "out of the effect (into OnPreRender, or into a command buffer run with ImageEffectGraphics.ExecuteCommandBuffer).";

        /// <summary>
        /// One script through every converter that handles it, in order, each on the previous one's result (the Free fix
        /// for Shader.Find, then the Pro converters). Nothing is written. Item null: no converter had anything to say.
        /// An effect fed by command buffers on lights (<paramref name="fed"/>, QA-003) is reported and left as it is.
        /// </summary>
        public static (KitConverterItem item, ShaderConversion result, List<IScriptFileConverter> applied) ConvertText(string path, string text,
            List<IScriptFileConverter> converters, IDictionary<string, string> fed, Dictionary<string, Analysis.EffectReach> reach = null)
        {
            if (reach != null && reach.TryGetValue(path, out var er) && er.Blocker.Length > 0)
                return (new KitConverterItem { Path = path, Converter = "Image effects", Outcome = "NotSupportedYet", Notes = { EffectLeftAsIs(er) } }, null, null);
            if (fed != null && fed.TryGetValue(path, out var feeder))
                return (new KitConverterItem { Path = path, Converter = "Image effects", Outcome = "NotSupportedYet", Notes = {
                    $"Not supported yet: this effect draws what {feeder} renders with Light.AddCommandBuffer, which URP does not run and Urpify does not convert yet. "
                    + "Converted alone it would darken or black out every frame, so it is left as it is: under URP the effect is missing and the rest of the frame is drawn." } }, null, null);
            var result = new ShaderConversion { Output = text };
            var names = new List<string>();
            var applied = new List<IScriptFileConverter>();
            // The converters read the reach of this run (Analysis.ImageEffectReach.Of / HelperOf).
            var previous = Analysis.ImageEffectReach.Current;
            if (reach != null) Analysis.ImageEffectReach.Use(reach);
            try
            {
                foreach (var conv in converters)
                {
                    if (!conv.Handles(result.Output, path)) continue;
                    ShaderConversion r;
                    try { r = conv.Convert(result.Output, path); }
                    catch (Exception e) { r = new ShaderConversion(); r.Add(NoteKind.NotSupported, 0, "Converter error: " + e.Message); }
                    if (!r.Converted && r.Notes.Count == 0) continue;
                    names.Add(conv.Name);
                    foreach (var n in r.Notes) result.Add(n.Kind, n.Line, n.Text);
                    if (!r.Converted) continue;
                    result.Output = r.Output;
                    result.Converted = true;
                    applied.Add(conv);
                }
            }
            finally { if (reach != null) Analysis.ImageEffectReach.Use(previous); }
            if (names.Count == 0) return (null, null, null);
            var item = new KitConverterItem { Path = path, Converter = string.Join(" + ", names), Notes = result.Notes.Select(n => n.ToString()).ToList() };
            item.Outcome = result.Tier.ToString();
            return (item, result, applied);
        }
    }
}
