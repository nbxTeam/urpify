using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Urpify.Pipeline
{
    /// <summary>
    /// URP presence is checked by fact, not by the package list (architect decision after M1, item 7): the runtime
    /// assembly is loaded and the UniversalRenderPipelineAsset type exists. In 2022.3 copies the package list said URP
    /// was installed while Library/PackageCache was gone and URP never compiled (STATUS.md, M1).
    /// </summary>
    public static class UrpInstaller
    {
        public const string PackageName = "com.unity.render-pipelines.universal";
        const string RuntimeAssembly = "Unity.RenderPipelines.Universal.Runtime";
        const string AssetType = "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset";

        public static bool IsLoaded() =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == RuntimeAssembly && a.GetType(AssetType, false) != null);

        /// <summary>The version of the URP package loaded in this Editor, or null without URP (QA-018: the Review report said "").</summary>
        public static string LoadedVersion()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == RuntimeAssembly);
            if (asm == null) return null;
            try { return UnityEditor.PackageManager.PackageInfo.FindForAssembly(asm)?.version; }
            catch { return null; }
        }

        public static bool InManifest(string projectRoot = ".") =>
            File.Exists(Path.Combine(projectRoot, "Packages", "manifest.json")) &&
            File.ReadAllText(Path.Combine(projectRoot, "Packages", "manifest.json")).Contains("\"" + PackageName + "\"");

        /// <summary>The URP version shipped with this Editor (its BuiltInPackages folder), e.g. 17.5.0 in 6000.5.9f1, 14.0.12 in 2022.3.62f3.</summary>
        public static string BundledVersion()
        {
            var p = Path.Combine(EditorApplication.applicationContentsPath, "Resources", "PackageManager", "BuiltInPackages", PackageName, "package.json");
            if (!File.Exists(p)) return null;
            var m = Regex.Match(File.ReadAllText(p), "\"version\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

#if !URPIFY_ASSET_STORE
        /// <summary>
        /// Recipe from STATUS.md M1 for a package list that claims packages the Library does not have: delete the cached
        /// package state so the Package Manager resolves again. Not in the Asset Store build, which leaves packages to the
        /// Package Manager.
        /// </summary>
        public static void RepairPackageCache(string projectRoot = ".")
        {
            foreach (var f in new[] { "ProjectCache", "ProjectCache.md5" })
            {
                var p = Path.Combine(projectRoot, "Library", "PackageManager", f);
                if (File.Exists(p)) File.Delete(p);
            }
            UnityEditor.PackageManager.Client.Resolve();
        }
#endif
    }

    public static class GitCheck
    {
        /// <summary>
        /// Uncommitted changes in the project's git repository, ignoring Urpify's own Urpify folder.
        /// Returns (false, 0, "") outside a repository and (true, -1, message) when git cannot run.
        /// </summary>
        public static (bool isRepo, int changes, string message) Status(string projectRoot = ".")
        {
            var root = Path.GetFullPath(projectRoot);
            var dir = new DirectoryInfo(root);
            bool repo = false;
            for (var d = dir; d != null; d = d.Parent) if (Directory.Exists(Path.Combine(d.FullName, ".git"))) { repo = true; break; }
            if (!repo) return (false, 0, "");
            try
            {
                // Only the Unity project's own folder (a project in a subfolder of the repository was warned about the
                // rest of it), paths relative to it, and a time limit (code review R-15.9).
                var psi = new ProcessStartInfo("git", "-c core.quotepath=off status --porcelain -- .")
                {
                    WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
                };
                using (var p = Process.Start(psi))
                {
                    var output = p.StandardOutput.ReadToEndAsync();
                    var errors = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(20000))
                    {
                        try { p.Kill(); } catch { }
                        return (true, -1, "git did not answer in 20 seconds.");
                    }
                    var lines = output.Result.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 3)
                        .Where(l => !l.Substring(3).Trim('"').StartsWith("Urpify/", StringComparison.Ordinal)).ToList();
                    return (true, lines.Count, string.Join("\n", lines.Take(20)));
                }
            }
            catch (Exception e)
            {
                return (true, -1, "git could not run: " + e.Message);
            }
        }
    }
}
