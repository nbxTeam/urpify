using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Urpify.Scanner
{
    /// <summary>
    /// Per-file cache of rule results in Library/Urpify/cache. An entry is valid while the file's hash,
    /// the hashes of its local includes, the rule set, Urpify version and the Unity version are unchanged.
    /// </summary>
    public sealed class ScanCache
    {
        [Serializable]
        sealed class IncludeHash { public string Path; public string Hash; }

        [Serializable]
        sealed class Entry
        {
            public string Key;
            public List<IncludeHash> Includes = new List<IncludeHash>();
            public List<Finding> Findings = new List<Finding>();
            public List<string> Extra = new List<string>();
        }

        readonly string dir;
        readonly string salt;
        readonly string projectRoot;
        readonly Dictionary<string, string> fileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public int Hits, Misses;

        public ScanCache(string projectRoot, string salt)
        {
            this.projectRoot = projectRoot;
            this.salt = salt;
            dir = Path.Combine(projectRoot, "Library", "Urpify", "cache");
        }

        public static string Sha1(byte[] data)
        {
            using (var sha = SHA1.Create())
            {
                var h = sha.ComputeHash(data);
                var sb = new StringBuilder(h.Length * 2);
                foreach (var b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static string Sha1(string s) => Sha1(Encoding.UTF8.GetBytes(s));

        /// <summary>Hash of a project file's bytes, memoized for the lifetime of this cache object (one scan).</summary>
        public string FileHash(string assetPath, Func<string, string> absolute)
        {
            if (fileHashes.TryGetValue(assetPath, out var h)) return h;
            try { h = Sha1(File.ReadAllBytes(absolute(assetPath))); }
            catch { h = "missing"; }
            fileHashes[assetPath] = h;
            return h;
        }

        string EntryPath(string kind, string assetPath) => Path.Combine(dir, kind, Sha1(assetPath) + ".json");

        public bool TryGet(string kind, string assetPath, string contentHash, Func<string, string> includeHash, out List<Finding> findings, out List<string> extra)
        {
            findings = null;
            extra = null;
            var p = EntryPath(kind, assetPath);
            if (!File.Exists(p)) { Misses++; return false; }
            try
            {
                var e = JsonUtility.FromJson<Entry>(File.ReadAllText(p));
                if (e == null || e.Key != Key(assetPath, contentHash)) { Misses++; return false; }
                foreach (var inc in e.Includes)
                    if (includeHash(inc.Path) != inc.Hash) { Misses++; return false; }
                findings = e.Findings;
                extra = e.Extra;
                Hits++;
                return true;
            }
            catch
            {
                Misses++;
                return false;
            }
        }

        public void Put(string kind, string assetPath, string contentHash, IEnumerable<(string path, string hash)> includes, List<Finding> findings, List<string> extra = null)
        {
            try
            {
                var e = new Entry { Key = Key(assetPath, contentHash), Findings = findings, Extra = extra ?? new List<string>() };
                foreach (var (path, hash) in includes) e.Includes.Add(new IncludeHash { Path = path, Hash = hash });
                var p = EntryPath(kind, assetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.WriteAllText(p, JsonUtility.ToJson(e));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Urpify] Cannot write scan cache: " + ex.Message);
            }
        }

        string Key(string assetPath, string contentHash) => assetPath + "|" + contentHash + "|" + salt;

        public void Clear()
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (Exception ex) { Debug.LogWarning("[Urpify] Cannot clear scan cache: " + ex.Message); }
        }
    }
}
