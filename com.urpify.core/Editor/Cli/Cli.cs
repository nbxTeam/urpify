using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Urpify.Reporting;
using Urpify.Scanner;

namespace Urpify.Cli
{
    /// <summary>
    /// Batch-mode entry points. Exit codes: 0 success, 1 error, 2 findings at or above -urpifyFailOn.
    ///
    ///   Unity -batchmode -projectPath &lt;project&gt; -executeMethod Urpify.Cli.Commands.Scan
    ///         [-urpifyOutput &lt;dir&gt;] [-urpifyScope &lt;path&gt;[;&lt;path&gt;...]] [-urpifyFormat json,html] [-urpifyFailOn pink|silent|degraded] [-urpifyNoCache]
    ///
    /// Do not pass -quit: the command exits the Editor itself with the exit code.
    /// </summary>
    public static partial class Commands
    {
        public static void Scan()
        {
            int code = 1;
            var args = Environment.GetCommandLineArgs();
            s_ScanError = "";
            s_ScanReports.Clear();
            try { code = RunScan(args); }
            catch (Exception e) { s_ScanError = e.Message; Log("Scan failed: " + e); }
            // Unity can crash while exiting some old projects after the work is done; CI reads this file instead.
            try
            {
                var output = Arg(args, "-urpifyOutput") ?? Path.Combine("Urpify", "Reports");
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "urpify-scan-result.txt"), code.ToString());
            }
            catch (Exception e) { Log("Cannot write urpify-scan-result.txt: " + e.Message); }
            var result = new Pipeline.CommandResult { command = "Scan", exitCode = code, error = s_ScanError };
            result.reports.AddRange(s_ScanReports);
            result.Write(Arg(args, "-urpifyOutput") ?? Path.Combine("Urpify", "Reports"), "urpify-scan-result.json");
            EditorApplication.Exit(code);
        }

        /// <summary>Testable core of <see cref="Scan"/>; returns the exit code.</summary>
        public static int RunScan(string[] args)
        {
            var output = Arg(args, "-urpifyOutput") ?? Path.Combine("Urpify", "Reports");
            var scope = (Arg(args, "-urpifyScope") ?? "Assets").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
            var formats = new HashSet<string>((Arg(args, "-urpifyFormat") ?? "json,html").Split(','), StringComparer.OrdinalIgnoreCase);
            var failOn = Arg(args, "-urpifyFailOn");
            bool noCache = args.Contains("-urpifyNoCache");

            foreach (var s in scope)
                if (!File.Exists(s) && !Directory.Exists(s)) { s_ScanError = $"Scope path not found: {s}"; Log(s_ScanError); return 1; }

            var scanner = new ProjectScanner();
            scanner.RunToEnd(new ScanOptions
            {
                Roots = scope,
                ScopeLabel = scope.Length == 1 && scope[0] == "Assets" ? "Project" : scope.Length == 1 && File.Exists(scope[0]) ? "File" : "Folder",
                UseCache = !noCache,
            });
            var r = scanner.Report;
            Directory.CreateDirectory(output);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var written = new List<string>();
            if (formats.Contains("json"))
            {
                var p = Path.Combine(output, $"urp-migration-report-{stamp}.json");
                File.WriteAllText(p, ReportJson.Write(r));
                File.WriteAllText(Path.Combine(output, "urp-migration-report-latest.json"), ReportJson.Write(r));
                written.Add(p);
            }
            if (formats.Contains("html"))
            {
                var p = Path.Combine(output, $"urp-migration-report-{stamp}.html");
                File.WriteAllText(p, ReportHtml.Write(r));
                written.Add(p);
            }
            var s2 = r.Summary;
            Log($"Scanned {r.FilesScanned} files ({r.FilesFromCache} from cache) in {r.ScanSeconds:0.00} s. " +
                $"Scenes: {s2.ScenesTotal} total, {s2.ScenesPink} pink, {s2.ScenesSilent} silent, {s2.ScenesDegraded} degraded, {s2.ScenesUnaffected} unaffected. " +
                $"Findings: {r.Findings.Count}. Parse failures: {s2.ParseFailures}. Kit converts {s2.KitConvertible} of {s2.KitNeeded}.");
            foreach (var w in written) { Log("Report: " + Path.GetFullPath(w)); s_ScanReports.Add(w); }

            if (!string.IsNullOrEmpty(failOn))
            {
                Severity threshold;
                switch (failOn.ToLowerInvariant())
                {
                    case "pink": threshold = Severity.Pink; break;
                    case "silent": threshold = Severity.Silent; break;
                    case "degraded": threshold = Severity.Degraded; break;
                    default: s_ScanError = "Unknown -urpifyFailOn value: " + failOn; Log(s_ScanError); return 1;
                }
                if (r.Findings.Any(f => f.Severity >= threshold)) return 2;
            }
            return 0;
        }

        static string s_ScanError = "";
        static readonly List<string> s_ScanReports = new List<string>();

        static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static void Log(string msg) => Debug.Log("[Urpify] " + msg);
    }
}
