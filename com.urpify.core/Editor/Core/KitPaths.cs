using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Urpify
{
    /// <summary>
    /// Where Urpify's packages sit in the project, project-relative with forward slashes and no trailing slash.
    /// Installed as a package (registry, embedded in Packages/, or file:) a package is "Packages/&lt;name&gt;". Imported from
    /// a .unitypackage it is the folder under Assets/ that holds its package.json (QA-006: Urpify read its catalog,
    /// localization, shader sources and runtime files from "Packages/..." only, and found nothing there).
    /// </summary>
    public static class KitPaths
    {
        public const string Core = "com.urpify.core";
        public const string Pro = "com.urpify.pro";

        static readonly Dictionary<string, string> s_Roots = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The package's root folder; "Packages/&lt;name&gt;" when it is not found anywhere (as before).</summary>
        public static string Root(string packageName)
        {
            lock (s_Roots)
            {
                if (s_Roots.TryGetValue(packageName, out var root)) return root;
                root = Find(packageName) ?? "Packages/" + packageName;
                s_Roots[packageName] = root;
                return root;
            }
        }

        /// <summary>A path inside the package: <c>In(Core, "Catalog/catalog.json")</c>.</summary>
        public static string In(string packageName, string relative) => Root(packageName) + "/" + relative.TrimStart('/');

        /// <summary>
        /// True for a file of Urpify itself imported into Assets: the scan and the converters leave it alone, as they
        /// never see Urpify installed as a package.
        /// </summary>
        public static bool IsKitFile(string projectPath)
        {
            var p = (projectPath ?? "").Replace('\\', '/');
            if (!p.StartsWith("Assets/", StringComparison.Ordinal)) return false;
            foreach (var name in new[] { Core, Pro })
            {
                var root = Root(name);
                if (root.StartsWith("Assets/", StringComparison.Ordinal) && (p == root || p.StartsWith(root + "/", StringComparison.Ordinal))) return true;
            }
            return false;
        }

        /// <summary>The folder the migration writes its own files into (compatibility scripts and shaders, library, fixed shaders).</summary>
        public const string ProjectFolder = "Assets/Urpify/";

        /// <summary>
        /// True for a file the migration or a package export of Urpify wrote: everything under Assets/Urpify/, the UrpCompat
        /// folder an exported package carries, and its UrpSettingsCheck_&lt;package&gt; script. They are written for URP, so a
        /// scan after the migration must not list them as what breaks (owner's check 19.10.2026, item 13: BuiltInCameraHooks.cs
        /// and BuiltInDepthNormals.shader were listed with fix tiers).
        /// </summary>
        public static bool IsWrittenByUrpify(string projectPath)
        {
            var p = (projectPath ?? "").Replace('\\', '/');
            if (p.StartsWith(ProjectFolder, StringComparison.Ordinal)) return true;
            if (p.IndexOf("/UrpCompat/", StringComparison.Ordinal) >= 0) return true;
            var name = Path.GetFileName(p);
            return name.StartsWith("UrpSettingsCheck_", StringComparison.Ordinal) && (name.EndsWith(".cs", StringComparison.Ordinal) || name.EndsWith(".cs.meta", StringComparison.Ordinal));
        }

        /// <summary>Forgets the roots found (tests; a package moved while the Editor runs).</summary>
        public static void Reset() { lock (s_Roots) s_Roots.Clear(); }

        static string Find(string packageName)
        {
            try
            {
                if (File.Exists(Path.Combine("Packages", packageName, "package.json"))) return "Packages/" + packageName;
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + packageName);
                if (info != null) return "Packages/" + packageName;
                if (!Directory.Exists("Assets")) return null;
                var name = new Regex("\"name\"\\s*:\\s*\"" + Regex.Escape(packageName) + "\"");
                foreach (var file in Directory.EnumerateFiles("Assets", "package.json", SearchOption.AllDirectories))
                {
                    string text;
                    try { text = File.ReadAllText(file); } catch { continue; }
                    if (name.IsMatch(text)) return Path.GetDirectoryName(file).Replace('\\', '/');
                }
            }
            catch (Exception) { }
            return null;
        }
    }
}
