using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Urpify.UI
{
    /// <summary>
    /// Opens Urpify's documentation on the window's language (acceptance of the fixes, 18.10.2026). Installed with the Package
    /// Manager the pages are in the package's Documentation~ folder; imported from the .unitypackage they are in
    /// Assets/Urpify/Documentation next to Urpify's folder (a folder ending in "~" never goes into a .unitypackage, so the
    /// release build copies the pages there). A page opens with the app the system uses for Markdown files.
    /// </summary>
    public static class DocumentationLink
    {
        const string Index = "index.md";

        /// <summary>The documentation folder (full path) of this install, or null when it has none.</summary>
        public static string Folder()
        {
            foreach (var candidate in Candidates())
            {
                try
                {
                    if (!string.IsNullOrEmpty(candidate) && File.Exists(Path.Combine(candidate, Index))) return Path.GetFullPath(candidate);
                }
                catch (Exception) { }
            }
            return null;
        }

        static System.Collections.Generic.IEnumerable<string> Candidates()
        {
            // The Package Manager: a registry package sits in Library/PackageCache, so its real folder comes from PackageInfo.
            string resolved = null;
            try { resolved = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(DocumentationLink).Assembly)?.resolvedPath; }
            catch (Exception) { }
            if (!string.IsNullOrEmpty(resolved)) yield return Path.Combine(resolved, "Documentation~");
            var root = KitPaths.Root(KitPaths.Core);
            yield return Path.Combine(root, "Documentation~");
            // The .unitypackage: Assets/Urpify/Core holds the package, Assets/Urpify/Documentation the pages.
            var parent = Path.GetDirectoryName(root);
            if (!string.IsNullOrEmpty(parent)) yield return Path.Combine(parent, "Documentation");
            yield return Path.Combine(root, "Documentation");
        }

        /// <summary>The index page for a window language: &lt;folder&gt;/&lt;lang&gt;/index.md, English at the root.</summary>
        public static string IndexPage(string folder, string language)
        {
            if (folder == null) return null;
            if (!string.IsNullOrEmpty(language) && language != "en")
            {
                var translated = Path.Combine(folder, language, Index);
                if (File.Exists(translated)) return translated;
            }
            return Path.Combine(folder, Index);
        }

        [MenuItem("Help/Urpify/Documentation", false, 999)]
        public static void Open()
        {
            var page = IndexPage(Folder(), Loc.Language);
            if (Application.isBatchMode)
            {
                // The buyer check (Tools/m9b_buyer.sh) runs this item in batch mode: no app to open, the page is logged.
                if (page != null) Debug.Log("[Urpify] Documentation: " + page.Replace('\\', '/'));
                else Debug.LogError("[Urpify] No documentation in this install.");
                EditorApplication.Exit(page != null ? 0 : 1);
                return;
            }
            if (page == null)
            {
                EditorUtility.DisplayDialog(Loc.Get("ui.docs.title"), Loc.Get("ui.docs.missing"), Loc.Get("ui.ok"));
                return;
            }
            try { EditorUtility.OpenWithDefaultApp(page); }
            catch (Exception e)
            {
                // No app for Markdown files: show the page in the file browser instead.
                Debug.LogWarning("[Urpify] Could not open " + page + ": " + e.Message);
                EditorUtility.RevealInFinder(page);
            }
        }
    }
}
