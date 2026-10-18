using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Urpify.Packaging;

namespace Urpify.Pipeline
{
    /// <summary>One asset of the package as the scope placed it.</summary>
    [Serializable]
    public sealed class PackageScopeEntry
    {
        public string Guid = "";
        /// <summary>Path in the package ("Assets/Shaders/Toon.shader").</summary>
        public string Path = "";
        /// <summary>Where the scope put it; "" when it was not extracted (textures, audio, binaries).</summary>
        public string ScopePath = "";
        public string ScopeGuid = "";
        public bool Folder;
    }

    [Serializable]
    public sealed class PackageScopeState
    {
        public string PackageFile = "";
        public string OpenedUtc = "";
        public List<PackageScopeEntry> Entries = new List<PackageScopeEntry>();
    }

    /// <summary>What an export wrote.</summary>
    public sealed class PackageExportResult
    {
        public string File = "";
        public ShaderOutput Output;
        public KitConverterReport Conversion = new KitConverterReport();
        /// <summary>URP settings the converted shaders need in the project that imports the package (UrpRequirement names).</summary>
        public List<string> Requirements = new List<string>();
        public int Replaced, Added;
        /// <summary>The package's own copy of Urpify's library ("Assets/Vendor/UrpCompat").</summary>
        public string CompatFolder = "";
    }

    /// <summary>
    /// Scope = a .unitypackage (TZ 4.2: "import into a temporary folder"). The package is read by Urpify itself and its
    /// assets are placed under <see cref="PackageScopePaths.Root"/> so that Unity imports them and the scanner and the
    /// converters see real materials, scenes and shaders:
    /// <list type="bullet">
    /// <item>every GUID gets a new one (and every reference to it in text assets follows), so assets the project already has
    /// (a package exported from this very project) are never touched;</item>
    /// <item>scripts and plug-ins are not imported (they would compile into the project): scripts go to a folder Unity
    /// ignores ("Scripts~") and are scanned as text; textures, audio and video are left out (the scan does not need them);</item>
    /// <item>shader includes written with a project path into the package ("Assets/...") point into the scope.</item>
    /// </list>
    /// The export (Pro) converts the scope's shaders, then writes a new .unitypackage from the original package: the same
    /// GUIDs and paths, the converted shaders' text, and the files the converted shaders need. Nothing outside the scope
    /// folder changes, except Urpify's shader library, which the compile check needs (added and removed again).
    /// </summary>
    public static class PackageScope
    {
        public const string StateFile = "Library/Urpify/package-scope.json";
        public static string Root => PackageScopePaths.Root;
        public static string ScriptsRoot => Root + "/Scripts~";

        static readonly HashSet<string> Code = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".cs", ".asmdef", ".asmref", ".rsp", ".dll", ".so", ".dylib", ".bundle", ".jslib", ".jspre", ".aar", ".jar", ".a", ".framework", ".mm", ".m", ".cpp", ".c", ".h" };
        static readonly HashSet<string> NotNeeded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".exr", ".hdr", ".gif", ".bmp", ".iff", ".pict", ".dds", ".ktx", ".astc",
              ".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac", ".mod", ".it", ".s3m", ".xm", ".mp4", ".mov", ".webm", ".avi", ".m4v", ".mpg", ".mpeg", ".ogv", ".wmv", ".asf", ".dv", ".vp8",
              ".ttf", ".otf", ".zip", ".7z", ".rar", ".unitypackage", ".pdf" };
        /// <summary>Text files in which GUID references are rewritten.</summary>
        static readonly HashSet<string> Yaml = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".mat", ".unity", ".prefab", ".asset", ".controller", ".anim", ".overrideController", ".mask", ".physicMaterial", ".physicsMaterial2D",
              ".flare", ".guiskin", ".fontsettings", ".terrainlayer", ".lighting", ".renderTexture", ".cubemap", ".spriteatlas", ".spriteatlasv2",
              ".playable", ".signal", ".mixer", ".brush", ".shadervariants", ".preset", ".giparams", ".mesh", ".compute", ".meta" };
        static readonly HashSet<string> ShaderText = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".shader", ".cginc", ".hlsl", ".cg", ".glslinc", ".compute" };

        static readonly Regex GuidToken = new Regex(@"\b[0-9a-fA-F]{32}\b");

        public static bool IsOpen => File.Exists(StateFile) && AssetDatabase.IsValidFolder(Root);

        public static PackageScopeState Load()
        {
            try { return File.Exists(StateFile) ? JsonUtility.FromJson<PackageScopeState>(File.ReadAllText(StateFile)) : null; }
            catch { return null; }
        }

        /// <summary>The folders a scan of the open package covers.</summary>
        public static string[] ScanRoots() => Directory.Exists(ScriptsRoot) ? new[] { Root, ScriptsRoot } : new[] { Root };

        /// <summary>Reads the package and imports it into the scope folder (an open scope is closed first).</summary>
        public static PackageScopeState Open(string packageFile, Action<string, float> progress = null)
        {
            var full = System.IO.Path.GetFullPath(packageFile);
            progress?.Invoke("Reading " + System.IO.Path.GetFileName(full), 0);
            var entries = UnityPackageArchive.Read(full);
            // A path that leaves the scope (a ".." segment, a drive or an absolute path) is never written: Unity's importer
            // refuses such entries, and so does the scope (code review R-12, 18.10.2026).
            foreach (var u in entries.Where(e => !IsSafePackagePath(e.Path)))
                Debug.LogWarning("[Urpify] The package lists a path outside its own folders; left out: " + u.Path);
            entries = entries.Where(e => IsSafePackagePath(e.Path)).ToList();
            Close();
            var state = new PackageScopeState { PackageFile = full.Replace('\\', '/'), OpenedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") };
            var map = entries.ToDictionary(e => e.Guid.ToLowerInvariant(), e => AssetGuids.Stable("package-scope/" + e.Guid.ToLowerInvariant()));
            var scopePaths = entries.ToDictionary(e => e.Path, ScopePathOf, StringComparer.Ordinal);
            int k = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var e in entries)
                {
                    progress?.Invoke("Extracting " + e.Path, 0.1f + 0.5f * k++ / Math.Max(1, entries.Count));
                    var item = new PackageScopeEntry { Guid = e.Guid, Path = e.Path, Folder = e.IsFolder, ScopeGuid = map[e.Guid.ToLowerInvariant()] };
                    state.Entries.Add(item);
                    var ext = System.IO.Path.GetExtension(e.Path);
                    if (e.IsFolder)
                    {
                        item.ScopePath = scopePaths[e.Path];
                        Directory.CreateDirectory(item.ScopePath);
                        if (e.Meta != null && item.ScopePath != Root) File.WriteAllBytes(item.ScopePath + ".meta", Rewrite(e.Meta, ".meta", map, scopePaths));
                        continue;
                    }
                    if (NotNeeded.Contains(ext)) continue;
                    var code = Code.Contains(ext);
                    // Scripts are scanned as text but never compiled: they go to a folder Unity does not import.
                    var target = code ? ScriptsRoot + "/" + Relative(e.Path) : scopePaths[e.Path];
                    if (code && !(ext.Equals(".cs", StringComparison.OrdinalIgnoreCase))) continue;
                    item.ScopePath = target;
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
                    File.WriteAllBytes(target, Rewrite(e.Asset, ext, map, scopePaths));
                    if (!code && e.Meta != null) File.WriteAllBytes(target + ".meta", Rewrite(e.Meta, ".meta", map, scopePaths));
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(StateFile));
            File.WriteAllText(StateFile, JsonUtility.ToJson(state, true));
            progress?.Invoke("Importing into " + Root, 0.65f);
            AssetDatabase.ImportAsset(Root, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return state;
        }

        /// <summary>Removes the scope folder and its state. Safe to call when nothing is open.</summary>
        public static void Close()
        {
            if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
            if (Directory.Exists(Root)) { try { Directory.Delete(Root, true); } catch { } }
            if (File.Exists(Root + ".meta")) File.Delete(Root + ".meta");
            if (File.Exists(StateFile)) File.Delete(StateFile);
        }

        /// <summary>False for a path from an archive that could leave the folder it is written to.</summary>
        public static bool IsSafePackagePath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("\\", StringComparison.Ordinal) || path.IndexOf(':') >= 0) return false;
            return !path.Replace('\\', '/').Split('/').Any(s => s == ".." || s == ".");
        }

        static string Relative(string packagePath) =>
            packagePath.StartsWith("Assets/", StringComparison.Ordinal) ? packagePath.Substring("Assets/".Length) : "_Other/" + packagePath.Replace(':', '_');

        static string ScopePathOf(UnityPackageEntry e) => e.Path == "Assets" ? Root : Root + "/" + Relative(e.Path);

        /// <summary>Package GUIDs to the scope's, and project-path includes into the scope.</summary>
        static byte[] Rewrite(byte[] data, string ext, Dictionary<string, string> map, Dictionary<string, string> scopePaths)
        {
            if (data == null) return new byte[0];
            if (Yaml.Contains(ext))
            {
                var text = Encoding.UTF8.GetString(data);
                if (text.IndexOf('\0') >= 0) return data; // binary-serialized asset: left as it is
                var rewritten = GuidToken.Replace(text, m => map.TryGetValue(m.Value.ToLowerInvariant(), out var g) ? g : m.Value);
                return rewritten == text ? data : Encoding.UTF8.GetBytes(rewritten);
            }
            if (ShaderText.Contains(ext))
            {
                var text = Encoding.UTF8.GetString(data);
                var rewritten = Regex.Replace(text, "(#\\s*include\\s*\")(Assets/[^\"]+)(\")",
                    m => scopePaths.TryGetValue(m.Groups[2].Value, out var p) ? m.Groups[1].Value + p + m.Groups[3].Value : m.Value);
                return rewritten == text ? data : Encoding.UTF8.GetBytes(rewritten);
            }
            return data;
        }

        /// <summary>The scope's GUIDs back to the package's (an exported material's references), from the open scope's state.</summary>
        public static string ToPackageGuids(string text)
        {
            var state = Load();
            if (state == null || string.IsNullOrEmpty(text)) return text ?? "";
            var back = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in state.Entries) if (e.ScopeGuid.Length > 0) back[e.ScopeGuid] = e.Guid;
            return GuidToken.Replace(text, m => back.TryGetValue(m.Value, out var g) ? g : m.Value);
        }

        /// <summary>Scope paths back to package paths (report paths, exported shader text).</summary>
        public static string ToPackagePaths(string text) =>
            (text ?? "").Replace(ScriptsRoot + "/", "Assets/").Replace(Root + "/_Other/", "").Replace(Root + "/", "Assets/");

        /// <summary>A copy of the report with the package's own paths instead of the scope folder's.</summary>
        public static MigrationReport ForPackage(MigrationReport report, PackageExportResult export = null)
        {
            var state = Load();
            var copy = JsonUtility.FromJson<MigrationReport>(ToPackagePaths(JsonUtility.ToJson(report)));
            copy.Scope = "Package";
            copy.ScopePaths = state != null ? new[] { state.PackageFile } : copy.ScopePaths;
            if (state != null) copy.PackageFile = state.PackageFile;
            if (export != null)
                copy.PackageExport = new PackageExportSummary
                {
                    File = export.File,
                    Output = export.Output.ToString(),
                    Replaced = export.Replaced,
                    Added = export.Added,
                    UrpSettings = export.Requirements.ToList(),
                    Shaders = export.Conversion.Items.Select(i => new ConvertedShader { Path = i.Path, Converter = i.Converter, Outcome = i.Outcome, Notes = i.Notes.ToList() }).ToList(),
                };
            return copy;
        }

        // ------------------------------------------------------------------------------------------ export (Pro)

        /// <summary>
        /// Converts the scope's shaders and writes the converted package: Urpify Pro's
        /// <see cref="IPackageExporter"/>. The scope's files are back to the package's afterwards.
        /// </summary>
        public static PackageExportResult Export(string outFile, ShaderOutput output, Action<string, float> progress = null)
        {
            var exporter = TypeCache.GetTypesDerivedFrom<IPackageExporter>().Where(t => !t.IsAbstract && !t.IsInterface && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => (IPackageExporter)Activator.CreateInstance(t)).FirstOrDefault();
            if (exporter == null) throw new InvalidOperationException("Exporting a converted package is part of Urpify Pro.");
            return exporter.Export(outFile, output, progress);
        }
    }

    /// <summary>Writes the converted copy of the open package (Pro), found through the TypeCache like the converters.</summary>
    public interface IPackageExporter
    {
        PackageExportResult Export(string outFile, ShaderOutput output, Action<string, float> progress);
    }
}
