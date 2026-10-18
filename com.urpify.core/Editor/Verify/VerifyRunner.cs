using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Urpify.Verify
{
    [Serializable]
    public sealed class SceneComparison
    {
        public string Scene = "";
        public ObservedVerdict Verdict;
        public List<ViewpointComparison> Views = new List<ViewpointComparison>();
        public string Error = "";
        /// <summary>Console errors while the scene played after the migration that the capture before did not have.</summary>
        public List<string> RenderErrors = new List<string>();
    }

    /// <summary>Compares two captures of the same project and writes difference maps next to them.</summary>
    public static class VerifyRunner
    {
        public static string CompareFolder(string before, string after) => CaptureManifest.Root + "/compare_" + before + "_" + after;

        /// <summary>Compares every scene and view present in both captures. Missing images are reported per scene.</summary>
        /// <param name="knownBefore">Console errors the project had before the migration too (a shader that does not compile in
        /// Built-in logs its error when URP first renders it): not counted against the scene.</param>
        public static List<SceneComparison> Compare(string beforeLabel, string afterLabel, CompareThresholds thresholds, bool writeDiffMaps = true, Func<string, bool> knownBefore = null)
        {
            var before = CaptureManifest.Load(beforeLabel) ?? throw new InvalidOperationException($"No capture '{beforeLabel}'. Run Capture first.");
            var after = CaptureManifest.Load(afterLabel) ?? throw new InvalidOperationException($"No capture '{afterLabel}'. Run Capture first.");
            var outDir = CompareFolder(beforeLabel, afterLabel);
            var list = new List<SceneComparison>();
            // Both captures from one pipeline: the frames can only match, and "Same" would read as "the migration is fine"
            // (owner's check 19.10.2026, item 2: "after" taken on Built-in, Same 0 % next to "will render pink").
            if (SamePipeline(before, after))
            {
                foreach (var b in before.Scenes)
                    list.Add(new SceneComparison { Scene = b.Scene, Verdict = ObservedVerdict.NotCaptured, Error = Loc.Get("observed.samePipeline", PipelineName(before.Pipeline)) });
                return list;
            }
            if (writeDiffMaps) Directory.CreateDirectory(outDir);
            var afterByScene = after.Scenes.GroupBy(s => s.Scene, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            foreach (var b in before.Scenes)
            {
                var sc = new SceneComparison { Scene = b.Scene, Verdict = ObservedVerdict.NotCaptured };
                list.Add(sc);
                if (!afterByScene.TryGetValue(b.Scene, out var a)) { sc.Error = "Not captured after."; continue; }
                if (b.Error.Length > 0 || a.Error.Length > 0) sc.Error = (b.Error + " " + a.Error).Trim();
                foreach (var bv in b.Views)
                {
                    var av = a.Views.FirstOrDefault(v => v.Name == bv.Name);
                    if (av == null) continue;
                    var vc = CompareImages(CaptureManifest.Folder(beforeLabel) + "/" + bv.Image, CaptureManifest.Folder(afterLabel) + "/" + av.Image,
                        thresholds, writeDiffMaps ? outDir + "/" + bv.Image : null);
                    vc.Name = bv.Name;
                    sc.Views.Add(vc);
                    sc.Verdict = sc.Verdict == ObservedVerdict.NotCaptured ? vc.Verdict : ImageCompare.Worst(sc.Verdict, vc.Verdict);
                }
                ApplyRenderErrors(sc, b, a, knownBefore);
                if (sc.RenderErrors.Count > 0)
                {
                    var cause = GrabPassSheetCause(sc.RenderErrors, SceneShaders(sc.Scene), ReadText);
                    if (cause.Length > 0) sc.Error = (sc.Error + " " + cause).Trim();
                }
            }
            return list;
        }

        /// <summary>The two captures were made with the same render pipeline: there is nothing to compare.</summary>
        public static bool SamePipeline(CaptureManifest before, CaptureManifest after) =>
            before != null && after != null && before.Pipeline.Length > 0 && string.Equals(before.Pipeline, after.Pipeline, StringComparison.Ordinal);

        /// <summary>"Built-in" or "URP" for a capture's pipeline (the manifest holds the pipeline asset's class name).</summary>
        public static string PipelineName(string pipeline) =>
            pipeline == "Built-in" ? "Built-in" : (pipeline ?? "").IndexOf("Universal", StringComparison.Ordinal) >= 0 ? "URP" : pipeline ?? "";

        static readonly System.Text.RegularExpressions.Regex SheetError =
            new System.Text.RegularExpressions.Regex(@"global property sheet\. Trying to add (\w+) \(");
        static readonly System.Text.RegularExpressions.Regex GrabPassBlock =
            new System.Text.RegularExpressions.Regex(@"(?m)^[ \t]*GrabPass\s*\{\s*(?:""(\w+)"")?");
        static readonly System.Text.RegularExpressions.Regex UrpSubShader =
            new System.Text.RegularExpressions.Regex(@"""RenderPipeline""\s*=\s*""UniversalPipeline""");

        /// <summary>
        /// "Shader properties can't be added to this global property sheet. Trying to add _X_ST": URP drew a shader that is
        /// still in its Built-in form and has a GrabPass into _X (checked directly, 2026-10-12: any such shader logs it for
        /// _X_ST, _X_TexelSize, _X_HDR and _X every frame, a converted one never). Names the files of the given shaders that
        /// have such a GrabPass and no URP SubShader, or "" when the errors are not of this kind or no file has it.
        /// </summary>
        public static string GrabPassSheetCause(IEnumerable<string> errors, IEnumerable<string> shaderPaths, Func<string, string> read)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in errors)
            {
                var m = SheetError.Match(e ?? "");
                if (!m.Success) continue;
                var n = m.Groups[1].Value;
                names.Add(n);
                foreach (var suffix in new[] { "_ST", "_TexelSize", "_HDR" })
                    if (n.EndsWith(suffix, StringComparison.Ordinal)) names.Add(n.Substring(0, n.Length - suffix.Length));
            }
            if (names.Count == 0) return "";
            var files = new List<string>();
            var grabNames = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var path in shaderPaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.Ordinal))
            {
                string text;
                try { text = read(path); } catch { continue; }
                if (string.IsNullOrEmpty(text) || UrpSubShader.IsMatch(text)) continue;
                var hit = GrabPassBlock.Matches(text).Cast<System.Text.RegularExpressions.Match>()
                    .Select(g => g.Groups[1].Success ? g.Groups[1].Value : "_GrabTexture").Where(names.Contains).ToList();
                if (hit.Count == 0) continue;
                files.Add(path);
                foreach (var h in hit) grabNames.Add(h);
            }
            if (files.Count == 0) return "";
            var shown = string.Join(", ", files.Take(5)) + (files.Count > 5 ? Loc.Get("observed.grabSheet.more", files.Count - 5) : "");
            return Loc.Get("observed.grabSheet", string.Join(", ", grabNames), shown);
        }

        /// <summary>The shader files a scene uses through its materials, or every shader file under Assets when none is found.</summary>
        static IEnumerable<string> SceneShaders(string scene)
        {
            string[] deps;
            try { deps = UnityEditor.AssetDatabase.GetDependencies(scene, true); } catch { deps = Array.Empty<string>(); }
            var shaders = deps.Where(d => d.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)).ToList();
            if (shaders.Count > 0) return shaders;
            return UnityEditor.AssetDatabase.FindAssets("t:Shader", new[] { "Assets" }).Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".shader", StringComparison.OrdinalIgnoreCase));
        }

        static string ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : "";

        /// <summary>
        /// Errors in the console while the scene played after the migration, new against the capture before, belong to the
        /// verdict: such a scene is at least Changed, never Same, and says why (QA-002: a renderer feature threw every frame
        /// and the report said only "Changed 100 %").
        /// </summary>
        public static void ApplyRenderErrors(SceneComparison sc, CapturedScene before, CapturedScene after, Func<string, bool> knownBefore = null)
        {
            var known = new HashSet<string>((before?.RenderErrors ?? new List<string>()).Select(CaptureRunner.ErrorText), StringComparer.Ordinal);
            sc.RenderErrors = (after?.RenderErrors ?? new List<string>())
                .Where(e => !known.Contains(CaptureRunner.ErrorText(e)) && (knownBefore == null || !knownBefore(CaptureRunner.ErrorText(e)))).ToList();
            if (sc.RenderErrors.Count == 0) return;
            if (sc.Verdict == ObservedVerdict.Same || sc.Verdict == ObservedVerdict.NotCaptured) sc.Verdict = ObservedVerdict.Changed;
            var first = sc.RenderErrors[0];
            sc.Error = (sc.Error + " Errors during rendering: " + first + (sc.RenderErrors.Count > 1 ? $" (and {sc.RenderErrors.Count - 1} more)" : "")).Trim();
        }

        public static ViewpointComparison CompareImages(string beforePath, string afterPath, CompareThresholds t, string diffPath)
        {
            var b = Load(beforePath);
            var a = Load(afterPath);
            try
            {
                var vc = new ViewpointComparison { Before = beforePath, After = afterPath };
                if (b.width != a.width || b.height != a.height)
                {
                    vc.Verdict = ObservedVerdict.NotCaptured;
                    return vc;
                }
                var diff = diffPath != null ? new Color32[b.width * b.height] : null;
                var r = ImageCompare.Compare(b.GetPixels32(), a.GetPixels32(), b.width, b.height, t, diff);
                vc.Verdict = r.Verdict;
                vc.PinkBeforePct = Math.Round(r.PinkBeforePct, 4);
                vc.PinkAfterPct = Math.Round(r.PinkAfterPct, 4);
                vc.MeanDiff = Math.Round(r.MeanDiff, 3);
                vc.ChangedPixelsPct = Math.Round(r.ChangedPixelsPct, 3);
                vc.LargestAreaPct = Math.Round(r.LargestAreaPct, 3);
                vc.WentFlat = ImageCompare.WentFlat(ImageCompare.FlatShare(b.GetPixels32()), ImageCompare.FlatShare(a.GetPixels32()));
                if (diffPath != null)
                {
                    var tex = new Texture2D(b.width, b.height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
                    tex.SetPixels32(diff);
                    tex.Apply();
                    File.WriteAllBytes(diffPath, tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex);
                    vc.Diff = diffPath;
                }
                return vc;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(b);
                UnityEngine.Object.DestroyImmediate(a);
            }
        }

        static Texture2D Load(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            if (!tex.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot read image " + path);
            return tex;
        }

        /// <summary>
        /// Errors of shaders the scan found broken before the migration (they do not compile in Built-in either): a message
        /// "Shader error in '&lt;name&gt;'" naming one of them is the project's, not the migration's.
        /// </summary>
        public static Func<string, bool> KnownBrokenShaders(MigrationReport scan)
        {
            if (scan == null) return null;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in scan.Findings.Where(f => f.RuleId == "SHADER.COMPILE_ERRORS").Select(f => f.AssetPath).Distinct())
            {
                var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (s != null) names.Add(s.name);
            }
            if (names.Count == 0) return null;
            return message => names.Any(n => message.IndexOf("'" + n + "'", StringComparison.Ordinal) >= 0);
        }

        /// <summary>Copies observed verdicts into a scan report (scenes matched by path) and recomputes its summary.</summary>
        /// <param name="afterUrpifyConverters">
        /// The Urpify converters step is done and the report carries its outcomes (KitOutcomes.Apply). A Same scene then gets the
        /// "silent break expected" note only when one of its silent breaks is left: not fixed by Unity's converter, by the free
        /// fixes, or by Pro when it is installed. Without the step, every Same scene with a silent break gets the note.
        /// </param>
        /// <returns>The scripts marked Review because a scene that uses them went flat (<see cref="ScreenEffectRules"/>).</returns>
        public static HashSet<string> Apply(MigrationReport report, IEnumerable<SceneComparison> comparisons, string beforeLabel, string afterLabel,
            bool afterUrpifyConverters = false)
        {
            var marked = new HashSet<string>(StringComparer.Ordinal);
            var byPath = report.Scenes.ToDictionary(s => s.Path, StringComparer.OrdinalIgnoreCase);
            foreach (var c in comparisons)
            {
                if (!byPath.TryGetValue(c.Scene, out var sv))
                {
                    sv = new SceneVerdict { Path = c.Scene };
                    report.Scenes.Add(sv);
                    byPath[c.Scene] = sv;
                }
                sv.Observed = c.Verdict;
                sv.Viewpoints = c.Views;
                sv.RenderErrors = c.RenderErrors.ToArray();
                sv.ObservedNote = c.Error;
                // The scanner expected a silent break here and the frames look the same: say so, never just "Same" (QA-009, QA-027),
                // also when the migration converted the break: the note then says so.
                if (sv.Has(Severity.Silent) && c.Verdict == ObservedVerdict.Same)
                    sv.ObservedNote = (sv.ObservedNote + " " + Loc.Get(!afterUrpifyConverters || SilentBreakLeft(report, sv.Path)
                        ? "observed.silentExpected" : "observed.silentFixedCheck")).Trim();
                // A frame the migration turned almost one flat colour (QA-030 black, QA-025 white): the screen effects Urpify runs
                // in this scene are not trusted as converted any more, whatever their conversion said.
                if (c.Views.Any(v => v.WentFlat))
                {
                    var effects = ScreenEffectsOf(report, sv.Path);
                    if (effects.Count > 0)
                    {
                        foreach (var f in report.Findings.Where(f => effects.Contains(f.AssetPath) && f.Severity > Severity.Info
                                                                     && (f.Tier == FixTier.Auto || f.Tier == FixTier.AutoApprox)))
                            f.Tier = FixTier.Review;
                        marked.UnionWith(effects);
                        sv.ObservedNote = (sv.ObservedNote + " " + Loc.Get("observed.flatFrame", string.Join(", ", effects.Select(Path.GetFileName)))).Trim();
                    }
                }
            }
            report.CompareBefore = beforeLabel;
            report.CompareAfter = afterLabel;
            report.Summarize();
            return marked;
        }

        /// <summary>Script rules whose fix runs inside URP's frame through Urpify's renderer feature: what can blank a frame out.</summary>
        public static readonly HashSet<string> ScreenEffectRules = new HashSet<string>(StringComparer.Ordinal)
        {
            "SCRIPT.ONRENDERIMAGE", "SCRIPT.COMMANDBUFFER.CAMERA", "SCRIPT.CAMERA_CALLBACKS", "SCRIPT.REPLACEMENT_SHADER", "SCRIPT.WILLRENDER_CAMERA",
        };

        /// <summary>The scripts of the scene whose screen-effect findings the migration fixed (Auto or AutoApprox, edition installed).</summary>
        public static List<string> ScreenEffectsOf(MigrationReport report, string scenePath) =>
            report.Findings.Where(f => ScreenEffectRules.Contains(f.RuleId) && Fixed(f)
                                       && (f.UsedBy ?? Array.Empty<string>()).Contains(scenePath, StringComparer.OrdinalIgnoreCase))
                .Select(f => f.AssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();

        /// <summary>
        /// A silent break of the scene (its own findings and those of the assets it uses) that the migration did not fix: one the
        /// Urpify converters step reported as Manual, Not supported yet or Review, or one only Pro fixes while Pro is not
        /// installed. The demo, fixed in full, then reads Same without a warning (2026-10-12).
        /// </summary>
        public static bool SilentBreakLeft(MigrationReport report, string scenePath) =>
            report.Findings.Any(f => f.Severity == Severity.Silent
                && (string.Equals(f.AssetPath, scenePath, StringComparison.OrdinalIgnoreCase) || (f.UsedBy ?? Array.Empty<string>()).Contains(scenePath, StringComparer.OrdinalIgnoreCase))
                && !Fixed(f));

        /// <summary>
        /// Fixed by the migration as far as the report knows: a tier the migration's steps do, by an edition that is installed.
        /// Meaningful once the Urpify converters step is done (before it the tiers are the scan's estimate).
        /// </summary>
        public static bool Fixed(Finding f) =>
            (f.Tier == FixTier.Auto || f.Tier == FixTier.AutoApprox || f.Tier == FixTier.UnityConverter)
            && (Edition.Owner(f) != FixOwner.Pro || Edition.ProInstalled);
    }
}
