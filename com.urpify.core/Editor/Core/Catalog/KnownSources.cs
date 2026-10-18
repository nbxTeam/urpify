using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Urpify.Catalog
{
    /// <summary>One known shader or script (TZ 11.1). Only fingerprints and recipes: the code itself is not in the package.</summary>
    [Serializable]
    public sealed class CatalogEntry
    {
        public string Id = "";
        /// <summary>"shader" or "script".</summary>
        public string Kind = "";
        public string Name = "";
        /// <summary>Where it comes from (repository URL), its license and the version it was taken at.</summary>
        public string Source = "", License = "", Version = "";
        /// <summary><see cref="ShaderFingerprint"/> of the normalized source.</summary>
        public string Fingerprint = "";
        /// <summary><see cref="SourceSignature.Structure"/>: the same for copies with renamed names.</summary>
        public string Structure = "";
        /// <summary><see cref="SourceSignature.MinHash"/>, base64.</summary>
        public string MinHash = "";
        /// <summary>How Urpify handles it: "constructions" (the converters), "camera-callbacks", "unity-urp-shader" (switch
        /// the materials to <see cref="UrpShader"/> with Unity's material upgrader), "none" (nothing to convert).</summary>
        public string Recipe = "";
        /// <summary>For "unity-urp-shader": the URP shader, and the Built-in shader whose upgrader carries the properties.</summary>
        public string UrpShader = "", BuiltInShader = "";
        /// <summary>What was checked, in words: "converts and compiles on URP 14 and 17 (Auto)", "4 scenes Same on 6000.5 and 2022.3".</summary>
        public string Status = "";
        /// <summary>The author's own URP version, recommended over a conversion (architect decision 6 after M4): name and link.</summary>
        public string OfficialUrp = "", OfficialUrpUrl = "";
        /// <summary>A script template whose recipe was checked on the real project (before/after Same): its findings are Auto.</summary>
        public bool Verified;

        [NonSerialized] uint[] m_Hashes;
        public uint[] Hashes => m_Hashes ??= SourceSignature.Decode(MinHash);
    }

    [Serializable]
    public sealed class CatalogFile
    {
        public int SchemaVersion = 1;
        /// <summary>Urpify version the catalog shipped with; it grows with every release (TZ 11.1).</summary>
        public string CatalogVersion = "";
        public List<CatalogEntry> Entries = new List<CatalogEntry>();
    }

    public enum MatchKind { None, Similar, Renamed, Exact }

    public sealed class CatalogMatch
    {
        public CatalogEntry Entry;
        public MatchKind Kind;
        /// <summary>Estimated share of code in common, 0..1 (1 for Exact and Renamed).</summary>
        public double Score;
        public override string ToString() => $"{Kind} {Entry?.Id} {Score:0.00}";
    }

    /// <summary>
    /// The catalog of known shaders and scripts: exact copies by fingerprint, copies with renamed names or other property
    /// defaults by structure, and similar code by the share of code in common ("looks like X, recipe X applied, check").
    /// </summary>
    public sealed class KnownSources
    {
        /// <summary>Minimum share of code in common for "looks like".</summary>
        public const double SimilarThreshold = 0.8; // 0.7 matched unrelated lab shaders to corpus entries (lab S10 at 0.72, M7)
        public static string CatalogPath => KitPaths.In(KitPaths.Core, "Catalog/catalog.json");

        readonly CatalogFile m_File;
        readonly Dictionary<string, CatalogEntry> m_ByFingerprint = new Dictionary<string, CatalogEntry>(StringComparer.Ordinal);
        readonly Dictionary<string, List<CatalogEntry>> m_ByStructure = new Dictionary<string, List<CatalogEntry>>(StringComparer.Ordinal);

        public KnownSources(CatalogFile file)
        {
            m_File = file ?? new CatalogFile();
            foreach (var e in m_File.Entries)
            {
                if (!string.IsNullOrEmpty(e.Fingerprint) && !m_ByFingerprint.ContainsKey(e.Fingerprint)) m_ByFingerprint[e.Fingerprint] = e;
                if (string.IsNullOrEmpty(e.Structure)) continue;
                if (!m_ByStructure.TryGetValue(e.Kind + e.Structure, out var same)) m_ByStructure[e.Kind + e.Structure] = same = new List<CatalogEntry>();
                same.Add(e);
            }
        }

        public IReadOnlyList<CatalogEntry> Entries => m_File.Entries;
        public string Version => m_File.CatalogVersion;

        static KnownSources s_Shipped;

        /// <summary>The catalog in the package, read once.</summary>
        public static KnownSources Shipped
        {
            get
            {
                if (s_Shipped != null) return s_Shipped;
                CatalogFile file = null;
                try
                {
                    var full = Path.GetFullPath(CatalogPath);
                    if (File.Exists(full)) file = JsonUtility.FromJson<CatalogFile>(File.ReadAllText(full));
                }
                catch (Exception e) { Debug.LogWarning("[Urpify] The catalog could not be read: " + e.Message); }
                return s_Shipped = new KnownSources(file);
            }
        }

        public CatalogEntry Find(string id) => m_File.Entries.FirstOrDefault(e => e.Id == id);

        public CatalogMatch RecognizeShader(string text, string path = "") =>
            Recognize("shader", ShaderFingerprint.Of(text, path), () => SourceSignature.OfShader(text, path), ShaderNameOf(text));

        static readonly System.Text.RegularExpressions.Regex s_ShaderName =
            new System.Text.RegularExpressions.Regex(@"^\s*Shader\s*""([^""]+)""", System.Text.RegularExpressions.RegexOptions.Multiline);

        static string ShaderNameOf(string text)
        {
            var m = s_ShaderName.Match(text);
            return m.Success ? m.Groups[1].Value : "";
        }

        public CatalogMatch RecognizeScript(string text) =>
            Recognize("script", ShaderFingerprint.OfScript(text), () => SourceSignature.OfScript(text));

        CatalogMatch Recognize(string kind, string fingerprint, Func<SourceSignature> signature, string name = "")
        {
            if (m_ByFingerprint.TryGetValue(fingerprint, out var exact) && exact.Kind == kind)
                return new CatalogMatch { Entry = exact, Kind = MatchKind.Exact, Score = 1 };
            var sig = signature();
            // Several entries can share a structure (variants that differ only in names and strings, lilToon's lts /
            // lts_cutout): the one with the same shader name wins, else the first.
            if (m_ByStructure.TryGetValue(kind + sig.Structure, out var renamed))
                return new CatalogMatch { Entry = renamed.FirstOrDefault(e => name.Length > 0 && e.Name == name) ?? renamed[0], Kind = MatchKind.Renamed, Score = 1 };
            CatalogEntry best = null;
            double bestScore = 0;
            foreach (var e in m_File.Entries)
            {
                if (e.Kind != kind) continue;
                var s = SourceSignature.Similarity(sig.MinHash, e.Hashes);
                if (s > bestScore) { bestScore = s; best = e; }
            }
            return bestScore >= SimilarThreshold ? new CatalogMatch { Entry = best, Kind = MatchKind.Similar, Score = bestScore } : null;
        }

        /// <summary>A catalog entry for a source (the build tool and the tests use it).</summary>
        public static CatalogEntry EntryFor(string kind, string text, string path = "")
        {
            var sig = kind == "shader" ? SourceSignature.OfShader(text, path) : SourceSignature.OfScript(text);
            return new CatalogEntry
            {
                Kind = kind,
                Fingerprint = kind == "shader" ? ShaderFingerprint.Of(text, path) : ShaderFingerprint.OfScript(text),
                Structure = sig.Structure,
                MinHash = SourceSignature.Encode(sig.MinHash),
            };
        }
    }
}
