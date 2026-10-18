using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Urpify.Pipeline;
using Urpify.Reporting;
using Urpify.Verify;

namespace Urpify.Cli
{
    /// <summary>
    /// Batch-mode commands for the before/after check and the migration steps. Each command exits the Editor with its
    /// own exit code (0 success, 1 error, 2 findings at or above -urpifyFailOn) and writes it to a result file first,
    /// because some projects make Unity crash while it exits after the work is done.
    ///
    ///   -executeMethod Urpify.Cli.Commands.Capture  -urpifyLabel before [-urpifyScenes build|all|a.unity;b.unity] [-urpifyWidth 960 -urpifyHeight 540 -urpifyFrames 30]
    ///   -executeMethod Urpify.Cli.Commands.Verify   [-urpifyBefore before] [-urpifyAfter after] [-urpifyFailOn pink|changed]
    ///   -executeMethod Urpify.Cli.Commands.Migrate  -urpifyStep scan|backup|capture-before|install-urp|unity-converter|kit-converters|capture-after|review
    ///                                                        [-urpifyShaderOutput replace|keep-builtin]  (kit-converters; default: the project setting)
    ///                                                        [-urpifyGrabPass opaque-texture|after-transparents]  (kit-converters; default: the project setting)
    ///                                                        [-urpifyStepTimeout 20]  (minutes without progress before the step is stopped; 0 = never)
    ///   -executeMethod Urpify.Cli.Commands.Rollback [-urpifyToStep backup]
    ///   -executeMethod Urpify.Cli.Commands.BakeEnvironment -urpifyScene Assets/a.unity [-urpifyUndo]
    /// </summary>
    public static partial class Commands
    {
        public static void Capture()
        {
            s_Command = "Capture";
            var args = Environment.GetCommandLineArgs();
            var label = Arg(args, "-urpifyLabel");
            if (string.IsNullOrEmpty(label)) { Fail("Capture needs -urpifyLabel, e.g. -urpifyLabel before."); return; }
            var s = CaptureSettingsFromArgs(args);
            var error = CaptureRunner.Start(label, s, exitEditorWhenDone: true);
            if (error != null) Fail(error);
            // CaptureRunner exits the Editor when Play Mode is over.
        }

        public static void Verify()
        {
            s_Command = "Verify";
            var args = Environment.GetCommandLineArgs();
            int code = 1;
            string error = "";
            var reports = new List<string>();
            try
            {
                var before = Arg(args, "-urpifyBefore") ?? MigrationRunner.BeforeLabel;
                var after = Arg(args, "-urpifyAfter") ?? MigrationRunner.AfterLabel;
                var report = MigrationRunner.LatestScanReport();
                var kit = KitOutcomes.Load();
                KitOutcomes.Apply(report, kit);
                var cmp = VerifyRunner.Compare(before, after, MigrationRunner.Thresholds, true, VerifyRunner.KnownBrokenShaders(report));
                KitOutcomes.MarkReview(kit, VerifyRunner.Apply(report, cmp, before, after, kit != null));
                report.UrpVersion = UrpInstaller.LoadedVersion() ?? report.UrpVersion;
                foreach (var p in MigrationRunner.WriteReport(report, "verify")) { Log("Report: " + Path.GetFullPath(p)); reports.Add(p); }
                var s = report.Summary;
                Log($"Verify {before} -> {after}: {cmp.Count} scenes, {s.ObservedPink} pink, {s.ObservedChanged} changed, {s.ObservedSame} same, {s.ObservedNotCaptured} not captured.");
                foreach (var c in cmp.Where(c => c.Verdict != ObservedVerdict.Same))
                    Log($"  {c.Verdict,-8} {c.Scene}  " + string.Join("  ", c.Views.Select(v => $"[{v.Name}: pink {v.PinkBeforePct:0.###}%->{v.PinkAfterPct:0.###}%, changed {v.ChangedPixelsPct:0.###}%, mean {v.MeanDiff:0.##}]")) + (c.Error.Length > 0 ? " " + c.Error : ""));
                code = 0;
                var failOn = Arg(args, "-urpifyFailOn");
                if (failOn == "pink" && s.ObservedPink > 0) code = 2;
                if (failOn == "changed" && s.ObservedPink + s.ObservedChanged > 0) code = 2;
            }
            catch (Exception e) { error = e.Message; Log("Verify failed: " + e.Message); }
            ExitWith(code, "urpify-verify-result.txt", error, reports);
        }

        public static void Migrate()
        {
            s_Command = "Migrate";
            var args = Environment.GetCommandLineArgs();
            var name = Arg(args, "-urpifyStep");
            if (!TryStep(name, out var step)) { Fail("Unknown -urpifyStep '" + name + "'. Use scan, backup, capture-before, install-urp, unity-converter, kit-converters, capture-after or review."); return; }
            if (step == StepId.CaptureBefore || step == StepId.CaptureAfter) MigrationRunner.Capture = CaptureSettingsFromArgs(args);
            var output = Arg(args, "-urpifyShaderOutput");
            if (!string.IsNullOrEmpty(output))
            {
                if (!KitProjectSettings.TryParseOutput(output, out var o)) { Fail("Unknown -urpifyShaderOutput '" + output + "'. Use replace or keep-builtin."); return; }
                MigrationRunner.ShaderOutputOverride = o;
            }
            var grab = Arg(args, "-urpifyGrabPass");
            if (!string.IsNullOrEmpty(grab))
            {
                if (!KitProjectSettings.TryParseGrabPass(grab, out var g)) { Fail("Unknown -urpifyGrabPass '" + grab + "'. Use opaque-texture or after-transparents."); return; }
                MigrationRunner.GrabPassOverride = g;
            }
            if (MigrationState.IsModifying(step))
            {
                var (repo, changes, msg) = GitCheck.Status();
                if (repo && changes != 0) Log($"Warning: the git working tree has {(changes < 0 ? "unknown" : changes.ToString())} uncommitted changes. Commit them first to keep a clean restore point.\n{msg}");
            }
            var error = MigrationRunner.Run(step, exitEditorWhenDone: true);
            if (error != null) Fail(error);
        }

        /// <summary>
        /// Scope = a .unitypackage (TZ 4.2): imported into a temporary folder, scanned, and with -urpifyExport (Pro) converted
        /// and written as a new package. The report (JSON + HTML) names the package's own paths.
        ///   -executeMethod Urpify.Cli.Commands.Package -urpifyPackage &lt;file.unitypackage&gt; [-urpifyOutput &lt;report dir&gt;]
        ///     [-urpifyExport &lt;out.unitypackage&gt;] [-urpifyShaderOutput replace|keep-builtin] [-urpifyKeepScope]
        /// </summary>
        public static void Package()
        {
            s_Command = "Package";
            var args = Environment.GetCommandLineArgs();
            int code = 1;
            string error = "";
            var reports = new List<string>();
            var keep = args.Contains("-urpifyKeepScope");
            try
            {
                var file = Arg(args, "-urpifyPackage");
                if (string.IsNullOrEmpty(file) || !File.Exists(file)) { Fail("Package needs -urpifyPackage <existing .unitypackage file>."); return; }
                ShaderOutput output = KitProjectSettings.instance.Output;
                var outputArg = Arg(args, "-urpifyShaderOutput");
                if (!string.IsNullOrEmpty(outputArg) && !KitProjectSettings.TryParseOutput(outputArg, out output)) { Fail("Unknown -urpifyShaderOutput '" + outputArg + "'. Use replace or keep-builtin."); return; }
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var state = PackageScope.Open(file);
                Log($"Package {Path.GetFileName(file)}: {state.Entries.Count(e => !e.Folder)} assets, {state.Entries.Count(e => e.ScopePath.Length > 0 && !e.Folder)} placed in {PackageScope.Root} ({clock.Elapsed.TotalSeconds:0.0} s).");
                var scanner = new Scanner.ProjectScanner();
                scanner.RunToEnd(new Scanner.ScanOptions { Roots = PackageScope.ScanRoots(), ScopeLabel = "Package", UseCache = false });
                PackageExportResult export = null;
                var exportPath = Arg(args, "-urpifyExport");
                // The analysis report is written whatever the export does (QA-016: the Free package refused the export and
                // wrote no report at all, though the analysis is what Free promises).
                string exportError = null;
                if (!string.IsNullOrEmpty(exportPath))
                {
                    try { export = PackageScope.Export(exportPath, output); }
                    catch (Exception e) { exportError = e.Message; export = null; Log("Export failed: " + e.Message); }
                }
                if (export != null)
                {
                    var items = export.Conversion.Items;
                    Log($"Exported {export.File}: output {KitProjectSettings.CliName(output)}, {items.Count(i => i.Outcome == "Auto" || i.Outcome == "AutoApprox" || i.Outcome == ShaderBatchConverter.ReviewOutcome)} of {items.Count} shaders converted "
                        + $"({items.Count(i => i.Outcome == "Auto")} Auto, {items.Count(i => i.Outcome == "AutoApprox")} AutoApprox, {items.Count(i => i.Outcome == ShaderBatchConverter.ReviewOutcome)} look different, {items.Count(i => i.Outcome == "Manual")} Manual, {items.Count(i => i.Outcome == "Restored")} Restored), "
                        + $"{export.Replaced} files replaced, {export.Added} added; URP settings needed: {(export.Requirements.Count == 0 ? "none" : string.Join(", ", export.Requirements))}.");
                }
                var report = PackageScope.ForPackage(scanner.Report, export);
                var dir = Arg(args, "-urpifyOutput") ?? MigrationRunner.ReportsDir;
                Directory.CreateDirectory(dir);
                var name = "urp-migration-package-" + Path.GetFileNameWithoutExtension(file);
                File.WriteAllText(Path.Combine(dir, name + ".json"), ReportJson.Write(report));
                File.WriteAllText(Path.Combine(dir, name + ".html"), ReportHtml.Write(report));
                reports.Add(Path.Combine(dir, name + ".json").Replace('\\', '/'));
                reports.Add(Path.Combine(dir, name + ".html").Replace('\\', '/'));
                var s = report.Summary;
                Log($"Report: {Path.GetFullPath(Path.Combine(dir, name + ".html"))}");
                Log($"Package scan: {report.FilesScanned} files, {s.AssetsPink} assets pink, {s.AssetsSilent} silent, {s.AssetsDegraded} degraded; scenes {s.ScenesPink} of {s.ScenesTotal} with pink objects; {s.KitConvertible} of {s.KitNeeded} breaking assets Pro converts; {clock.Elapsed.TotalSeconds:0.0} s in all.");
                code = exportError == null ? 0 : 1;
                if (exportError != null) error = "Export failed: " + exportError + " The analysis report was written.";
            }
            catch (Exception e) { error = e.Message; Log("Package failed: " + e); }
            finally
            {
                if (!keep) { try { PackageScope.Close(); } catch (Exception e) { Log("Could not remove the package scope folder: " + e.Message); } }
            }
            ExitWith(code, "urpify-package-result.txt", error, reports);
        }

        public static void Rollback()
        {
            s_Command = "Rollback";
            var args = Environment.GetCommandLineArgs();
            var name = Arg(args, "-urpifyToStep");
            StepId? target = null;
            if (!string.IsNullOrEmpty(name))
            {
                if (!TryStep(name, out var t)) { Fail("Unknown -urpifyToStep '" + name + "'."); return; }
                target = t;
            }
            int code = 1;
            string error = "";
            try
            {
                var r = MigrationRunner.Rollback(target);
                Log($"Rollback: {r.Restored.Count} files restored, {r.Removed.Count} removed, {r.Skipped.Count} left as changed after the migration, {r.Between.Count} appeared between steps, {r.Errors.Count} errors.");
                foreach (var f in r.Skipped) Log("  changed after the migration, not touched: " + f);
                foreach (var f in r.Between) Log("  appeared or changed between steps, not touched: " + f);
                foreach (var e in r.Errors) Log("  error: " + e);
                Directory.CreateDirectory("Urpify");
                File.WriteAllText("Urpify/rollback-latest.json", JsonUtility.ToJson(r, true));
                code = r.Errors.Count == 0 ? 0 : 1;
                if (code != 0) error = string.Join(" | ", r.Errors.Take(5));
            }
            catch (Exception e) { error = e.Message; Log("Rollback failed: " + e.Message); }
            // The Editor quits now with the migration's assemblies still loaded. Left to refresh on the way out, Unity's
            // compilation pipeline reads assembly definitions the rollback removed (Assets/Urpify/Compat) and batch mode
            // aborts with "Could not read file ...asmdef" (architect decision 4 of 13.10.2026; every 6000.5 project with the
            // runtime folder). The next session imports the restored project as it is on disk.
            AssetDatabase.DisallowAutoRefresh();
            ExitWith(code, "urpify-rollback-result.txt", error, new[] { "Urpify/rollback-latest.json" });
        }

        /// <summary>Bake environment for one scene, as the scene panel's button does; with -urpifyUndo, rolls the bake back.</summary>
        public static void BakeEnvironment()
        {
            s_Command = "BakeEnvironment";
            var args = Environment.GetCommandLineArgs();
            var scene = Arg(args, "-urpifyScene");
            int code = 1;
            try
            {
                if (string.IsNullOrEmpty(scene)) Log("Bake environment: -urpifyScene <scene path> is required.");
                else if (args.Contains("-urpifyUndo"))
                {
                    var r = EnvironmentBake.Undo(scene);
                    Log($"Bake environment rolled back: {r.Restored.Count} files restored, {r.Removed.Count} removed, {r.Skipped.Count} left as changed later, {r.Errors.Count} errors.");
                    foreach (var e in r.Errors) Log("  error: " + e);
                    code = r.Errors.Count == 0 ? 0 : 1;
                }
                else
                {
                    var error = EnvironmentBake.Bake(scene);
                    Log(error == null ? "Bake environment: " + EnvironmentBake.Done(scene)?.Message : "Bake environment failed: " + error);
                    code = error == null ? 0 : 1;
                }
            }
            catch (Exception e) { Log("Bake environment failed: " + e.Message); }
            ExitWith(code, "urpify-bake-result.txt");
        }

        // ------------------------------------------------------------------ helpers

        static bool TryStep(string name, out StepId id)
        {
            id = StepId.Scan;
            if (string.IsNullOrEmpty(name)) return false;
            var key = name.Replace("-", "").Replace("_", "");
            foreach (StepId s in Enum.GetValues(typeof(StepId)))
                if (string.Equals(s.ToString(), key, StringComparison.OrdinalIgnoreCase)) { id = s; return true; }
            return false;
        }

        static CaptureSettings CaptureSettingsFromArgs(string[] args)
        {
            var s = new CaptureSettings();
            var scenes = Arg(args, "-urpifyScenes");
            if (scenes == "all") s.Scenes = SceneSelection.All;
            else if (!string.IsNullOrEmpty(scenes) && scenes != "build")
            {
                s.Scenes = SceneSelection.Selected;
                s.Selected = scenes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
            }
            if (int.TryParse(Arg(args, "-urpifyWidth"), out var w)) s.Width = w;
            if (int.TryParse(Arg(args, "-urpifyHeight"), out var h)) s.Height = h;
            if (int.TryParse(Arg(args, "-urpifyFrames"), out var f)) s.Frames = f;
            return s;
        }

        static void Fail(string message)
        {
            Log(message);
            ExitWith(1, "urpify-command-result.txt", message);
        }

        /// <summary>The command running, for the JSON result (set by each entry point).</summary>
        static string s_Command = "";

        static void ExitWith(int code, string resultFile, string error = "", IEnumerable<string> reports = null)
        {
            try
            {
                Directory.CreateDirectory("Urpify");
                File.WriteAllText(Path.Combine("Urpify", resultFile), code.ToString());
            }
            catch { }
            var r = new CommandResult { command = s_Command, exitCode = code, error = error ?? "" };
            if (reports != null) r.reports.AddRange(reports);
            r.Write(MigrationRunner.ReportsDir, Path.ChangeExtension(resultFile, ".json"));
            EditorApplication.Exit(code);
        }
    }
}
