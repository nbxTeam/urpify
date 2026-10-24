using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Urpify.Pipeline
{
    /// <summary>
    /// The settings files right after a command-line rollback, and putting them back in the next Editor. Unity keeps the
    /// packages the migration installed loaded until it quits, and on the way out writes their settings
    /// (ProjectSettings/ShaderGraphSettings.asset) or re-saves settings it holds (InputManager.asset, QA-005). Nobody works
    /// in the project between a batch rollback and the next start, so what changed in between is undone. Only
    /// ProjectSettings and the files directly in Packages are kept: that is where those writes land.
    /// </summary>
    public static class AfterRollback
    {
        const string Dir = "Urpify/Backup/after-rollback";

        [Serializable]
        sealed class Manifest { public List<FileHash> Files = new List<FileHash>(); }

        static IEnumerable<string> Watched(string root)
        {
            var ps = Path.Combine(root, "ProjectSettings");
            if (Directory.Exists(ps))
                foreach (var f in Directory.EnumerateFiles(ps, "*", SearchOption.AllDirectories)) yield return Rel(root, f);
            var pk = Path.Combine(root, "Packages");
            if (Directory.Exists(pk))
                foreach (var f in Directory.EnumerateFiles(pk, "*", SearchOption.TopDirectoryOnly))
                    if (!BackupStore.LeftToPackageManager(Rel(root, f))) yield return Rel(root, f);
        }

        static string Rel(string root, string abs) => abs.Substring(Path.GetFullPath(root).Length).TrimStart('\\', '/').Replace('\\', '/');

        public static void Record(string root)
        {
            var dir = Path.Combine(root, Dir);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            var m = new Manifest();
            foreach (var rel in Watched(Path.GetFullPath(root)))
            {
                var src = Path.Combine(root, rel);
                var dst = Path.Combine(dir, "files", rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(src, dst, true);
                m.Files.Add(new FileHash { Path = rel, Hash = BackupStore.Hash(src) });
            }
            BackupStore.WriteAtomically(Path.Combine(dir, "manifest.json"), JsonUtility.ToJson(m, true));
        }

        /// <summary>Puts back what changed since <see cref="Record"/>; returns the paths it touched. No record: nothing.</summary>
        public static List<string> Settle(string root)
        {
            var done = new List<string>();
            var dir = Path.Combine(root, Dir);
            var mp = Path.Combine(dir, "manifest.json");
            if (!File.Exists(mp)) return done;
            var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(mp)) ?? new Manifest();
            var known = m.Files.ToDictionary(f => f.Path, f => f.Hash, StringComparer.Ordinal);
            foreach (var rel in Watched(Path.GetFullPath(root)).ToList())
            {
                var abs = Path.Combine(root, rel);
                if (!known.TryGetValue(rel, out var hash))
                {
                    // Written by a package the rollback removed; kept next to the record, not deleted outright.
                    var kept = Path.Combine(dir, "removed", rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(kept));
                    File.Copy(abs, kept, true);
                    File.Delete(abs);
                    done.Add(rel);
                }
                else if (BackupStore.Hash(abs) != hash)
                {
                    File.Copy(Path.Combine(dir, "files", rel), abs, true);
                    done.Add(rel);
                }
            }
            File.Delete(mp);
            if (done.Count > 0)
                foreach (var rel in done.Where(r => r.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(root, r))))
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(rel))
                        if (o != null) EditorUtility.ClearDirty(o);
            return done;
        }
    }
}
