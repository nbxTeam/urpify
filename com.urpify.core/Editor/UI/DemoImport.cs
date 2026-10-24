using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Urpify.UI
{
    /// <summary>
    /// Imports the demo scene (architect decision 3 of 13.10.2026). Urpify installed with the Package Manager carries it as the
    /// package sample (Samples~/Demo); Urpify imported from a .unitypackage cannot, since Unity leaves folders ending in "~" out of
    /// a .unitypackage, so that build carries the same demo as a .unitypackage of its own (Demo/UrpifyDemo.unitypackage, built
    /// from Samples~/Demo by Tools/build_packages.sh). Both land in Assets/Samples/Urpify/&lt;version&gt;/Demo.
    /// </summary>
    public static class DemoImport
    {
        public const string NestedPackage = "Demo/UrpifyDemo.unitypackage";
        const string SceneName = "Demo.unity";

        /// <summary>The demo .unitypackage inside Urpify, when Urpify was imported from a .unitypackage; else null.</summary>
        public static string NestedPackagePath()
        {
            var p = KitPaths.In(KitPaths.Core, NestedPackage);
            return File.Exists(p) ? p : null;
        }

        /// <summary>The package sample, when Urpify is installed with the Package Manager; else null.</summary>
        static Sample? UpmSample()
        {
            try
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(DemoImport).Assembly);
                if (info == null) return null;
                foreach (var s in Sample.FindByPackage(info.name, info.version))
                    if (s.displayName == "Demo") return s;
            }
            catch (Exception e) { Debug.LogWarning("[Urpify] Could not look up the package sample: " + e.Message); }
            return null;
        }

        public static bool Available => NestedPackagePath() != null || UpmSample() != null;

        [MenuItem("Help/Urpify/Import Demo", false, 1000)]
        public static void Import()
        {
            var nested = NestedPackagePath();
            if (nested != null)
            {
                // The import dialog lists the files first; the scene opens when the import has finished. The demo's scripts
                // make Unity compile and reload the domain after the import, which drops any handler registered here, so what
                // is left to do is kept in a file under Temp and picked up again after the reload (Resume). In batch mode (the
                // buyer check, Tools/m9b_buyer.sh, run without -quit) there is no dialog and no scene to open: the Editor quits.
                Directory.CreateDirectory("Temp");
                File.WriteAllText(PendingFile, Application.isBatchMode ? "quit" : "open");
                AssetDatabase.importPackageCancelled -= OnCancelled;
                AssetDatabase.importPackageCancelled += OnCancelled;
#if UNITY_6000_6_OR_NEWER
                UnityEditor.AssetPackage.Package.Import(nested, !Application.isBatchMode);
#else
                AssetDatabase.ImportPackage(nested, !Application.isBatchMode);
#endif
                Watch();
                return;
            }
            var sample = UpmSample();
            if (sample == null)
            {
                if (Application.isBatchMode) { Debug.LogError("[Urpify] No demo: neither " + NestedPackage + " nor the package sample."); EditorApplication.Exit(1); return; }
                EditorUtility.DisplayDialog(Loc.Get("ui.demo.title"), Loc.Get("ui.demo.missing"), Loc.Get("ui.ok"));
                return;
            }
            var s = sample.Value;
            if (!s.isImported || Application.isBatchMode || EditorUtility.DisplayDialog(Loc.Get("ui.demo.title"), Loc.Get("ui.demo.again"), Loc.Get("ui.demo.import"), Loc.Get("ui.cancel")))
            {
                if (!s.Import(Sample.ImportOptions.OverridePreviousImports))
                {
                    Debug.LogError("[Urpify] The demo sample did not import.");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }
                AssetDatabase.Refresh();
            }
            if (Application.isBatchMode) { Debug.Log("[Urpify] Demo imported."); EditorApplication.Exit(0); return; }
            OpenScene(s.importPath);
        }

        [MenuItem("Help/Urpify/Import Demo", true)]
        static bool CanImport() => Available;

        /// <summary>What is left after the nested package's import: "open" (the scene) or "quit" (batch mode); none = nothing.</summary>
        const string PendingFile = "Temp/urpify-demo-import.txt";

        static void OnCancelled(string packageName)
        {
            AssetDatabase.importPackageCancelled -= OnCancelled;
            if (!string.Equals(packageName, Path.GetFileNameWithoutExtension(NestedPackage), StringComparison.Ordinal)) return;
            try { File.Delete(PendingFile); } catch { }
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        /// <summary>After the domain reload the demo's scripts cause: carries on with what <see cref="Import"/> left to do.</summary>
        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (File.Exists(PendingFile)) Watch();
        }

        /// <summary>
        /// Waits until the demo scene is in the project and Unity is neither importing nor compiling, then opens the scene or
        /// quits. Gives up after ten minutes (the import window left open, or an import that failed).
        /// </summary>
        static void Watch()
        {
            var until = EditorApplication.timeSinceStartup + 600;
            double next = 0;
            int quiet = 0;
            void Tick()
            {
                // Twice a second is enough; FindAssets on a folder that does not exist yet warns on every call (75 000
                // warnings in the window check of 16.10.2026), so the folder is checked first.
                if (EditorApplication.timeSinceStartup < next) return;
                next = EditorApplication.timeSinceStartup + 0.5;
                if (!File.Exists(PendingFile)) { EditorApplication.update -= Tick; return; }
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) { quiet = 0; return; }
                bool imported = AssetDatabase.IsValidFolder("Assets/Samples") && AssetDatabase.FindAssets("Demo t:Scene", new[] { "Assets/Samples" }).Length > 0;
                bool timedOut = EditorApplication.timeSinceStartup > until;
                if ((!imported || ++quiet < 3) && !timedOut) return;
                EditorApplication.update -= Tick;
                string mode;
                try { mode = File.ReadAllText(PendingFile).Trim(); File.Delete(PendingFile); } catch { return; }
                if (mode == "quit")
                {
                    if (imported) Debug.Log("[Urpify] Demo imported.");
                    else Debug.LogError("[Urpify] The demo did not import.");
                    EditorApplication.Exit(imported ? 0 : 1);
                }
                else if (imported) OpenScene(null);
            }
            EditorApplication.update += Tick;
        }

        /// <summary>Opens the demo scene (asking first to save the open ones) and shows it in the Project window.</summary>
        static void OpenScene(string folder)
        {
            string scene = null;
            if (!string.IsNullOrEmpty(folder))
            {
                var full = Path.Combine(folder, SceneName);
                if (File.Exists(full)) scene = ProjectRelative(full);
            }
            if (scene == null && AssetDatabase.IsValidFolder("Assets/Samples"))
                scene = AssetDatabase.FindAssets("Demo t:Scene", new[] { "Assets/Samples" }).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.EndsWith("/Demo/" + SceneName, StringComparison.Ordinal))
                    .OrderByDescending(p => p.Contains("/Urpify/")).FirstOrDefault();
            if (scene == null) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(scene);
            UiKit.Ping(scene);
        }

        static string ProjectRelative(string path)
        {
            var root = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
            var p = Path.GetFullPath(path).Replace('\\', '/');
            return p.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? p.Substring(root.Length) : path.Replace('\\', '/');
        }
    }
}
