using System;
using System.Collections.Generic;
using System.Linq;

namespace Urpify
{
    /// <summary>What the before/after captures of one viewpoint show.</summary>
    public enum ObservedVerdict
    {
        /// <summary>No capture pair exists for the scene.</summary>
        NotCaptured = 0,
        Same = 1,
        /// <summary>Visible difference without new pink.</summary>
        Changed = 2,
        /// <summary>The pink share of the frame grew by more than the threshold.</summary>
        Pink = 3,
    }

    /// <summary>Comparison of one viewpoint (a camera or a MigrationViewpoint) before and after.</summary>
    [Serializable]
    public sealed class ViewpointComparison
    {
        public string Name = "";
        public ObservedVerdict Verdict;
        /// <summary>Share of the frame that is pure magenta, percent.</summary>
        public double PinkBeforePct, PinkAfterPct;
        /// <summary>Mean absolute difference per channel, 0..255.</summary>
        public double MeanDiff;
        /// <summary>Share of pixels whose largest channel difference is above the pixel threshold, percent.</summary>
        public double ChangedPixelsPct;
        /// <summary>Largest connected changed area, percent of the frame (QA-009).</summary>
        public double LargestAreaPct;
        /// <summary>The frame after is almost one flat colour and the frame before was not (ImageCompare.WentFlat).</summary>
        public bool WentFlat;
        /// <summary>Project-relative image paths.</summary>
        public string Before = "", After = "", Diff = "";
    }

    /// <summary>
    /// Verdict for one scene. Two independent verdicts (architect decision after M1, item 3):
    /// Static — from the scanner, the worst finding among everything the scene uses;
    /// Observed — from the before/after captures (Verify). A pink object may be off-camera, so Static is never softened.
    /// </summary>
    [Serializable]
    public sealed class SceneVerdict
    {
        public string Path;
        /// <summary>Static verdict.</summary>
        public Severity Severity;
        public FixTier Tier;
        /// <summary>Assets that decide the static verdict (those with the worst severity).</summary>
        public string[] Causes = Array.Empty<string>();
        /// <summary>Rule ids behind the static verdict, distinct.</summary>
        public string[] Rules = Array.Empty<string>();
        /// <summary>Every breaking rule in the scene as "Severity RuleId", worst first; a Pink verdict can hide Silent breaks.</summary>
        public string[] Breaks = Array.Empty<string>();

        /// <summary>Worst verdict over the scene's viewpoints; NotCaptured without captures.</summary>
        public ObservedVerdict Observed;
        public List<ViewpointComparison> Viewpoints = new List<ViewpointComparison>();
        /// <summary>Console errors while the scene played after the migration, new against the capture before.</summary>
        public string[] RenderErrors = Array.Empty<string>();
        /// <summary>What the comparison adds to the observed verdict: errors during rendering, a capture problem.</summary>
        public string ObservedNote = "";

        /// <summary>True when the scene has breaks of this severity (categories overlap).</summary>
        public bool Has(Severity s) => s == Severity.Info
            ? Breaks.Length == 0
            : Breaks.Any(b => b.StartsWith(s + " ", StringComparison.Ordinal));

        /// <summary>The scanner found pink objects, but none of the captured frames shows new pink.</summary>
        public bool PinkNotInFrame => Severity == Severity.Pink && Observed != ObservedVerdict.NotCaptured && Observed != ObservedVerdict.Pink;
    }

    /// <summary>Per-asset roll-up used by the window and the report.</summary>
    [Serializable]
    public sealed class AssetSummary
    {
        public string Path;
        public AssetKind Kind;
        public Severity Severity;
        public FixTier Tier;
        public int FindingCount;
        public string[] UsedBy = Array.Empty<string>();
        /// <summary>Severities present among the asset's findings (categories overlap).</summary>
        public Severity[] Severities = Array.Empty<Severity>();
    }

    /// <summary>
    /// Counts for the summary line. Scene and asset counts are per category and overlap: a scene with a pink
    /// material and a silent script counts in both. "Unaffected" means no breaking finding at all.
    /// </summary>
    [Serializable]
    public sealed class ReportSummary
    {
        public int ScenesTotal, ScenesPink, ScenesSilent, ScenesDegraded, ScenesUnaffected;
        public int AssetsWithFindings;
        public int AssetsPink, AssetsSilent, AssetsDegraded, AssetsInfo;
        public int TierUnityConverter, TierAuto, TierAutoApprox, TierNotSupportedYet, TierManual, TierReview;
        /// <summary>Assets that break and that Urpify converts (Auto or AutoApprox) out of assets that break and Unity's converter does not fix.</summary>
        public int KitConvertible, KitNeeded;
        public int ParseFailures;
        /// <summary>Observed verdicts of captured scenes; exclusive (worst viewpoint).</summary>
        public int ObservedPink, ObservedChanged, ObservedSame, ObservedNotCaptured;
    }

    /// <summary>One shader of an exported package and what the converter made of it.</summary>
    [Serializable]
    public sealed class ConvertedShader
    {
        public string Path = "";
        public string Converter = "";
        /// <summary>Auto, AutoApprox, Manual (not converted) or Restored (converted but did not compile, left as it was).</summary>
        public string Outcome = "";
        public List<string> Notes = new List<string>();
    }

    /// <summary>Export of a converted .unitypackage (TZ 2: Scope .unitypackage with export, Pro).</summary>
    [Serializable]
    public sealed class PackageExportSummary
    {
        public string File = "";
        /// <summary>ShaderOutput name: ReplaceOriginal or KeepBuiltInSupport.</summary>
        public string Output = "";
        public int Replaced, Added;
        public List<ConvertedShader> Shaders = new List<ConvertedShader>();
        /// <summary>URP Asset settings the converted shaders need in the project that imports the package.</summary>
        public List<string> UrpSettings = new List<string>();
    }

    [Serializable]
    public sealed class MigrationReport
    {
        /// <summary>2: overlapping summary counts, observed verdicts per scene. 3: packageFile, packageExport.</summary>
        public const int CurrentSchemaVersion = 5; // 5: fix tier Review, observed notes, render errors, largest changed area, kit outcomes

        public int SchemaVersion = CurrentSchemaVersion;
        public string GeneratedUtc = "";
        public string UnityVersion = "";
        public string UrpVersion = "";
        public string KitVersion = "";
        public string ProjectName = "";
        public string Scope = "";
        public string[] ScopePaths = Array.Empty<string>();
        public double ScanSeconds;
        public int FilesScanned;
        public int FilesFromCache;
        public bool Cancelled;
        /// <summary>Seconds per scan phase, "phase=seconds", in order.</summary>
        public List<string> Timings = new List<string>();
        /// <summary>Capture labels the observed verdicts compare, e.g. "before" and "after"; empty without Verify.</summary>
        public string CompareBefore = "", CompareAfter = "";
        public ReportSummary Summary = new ReportSummary();
        public List<Finding> Findings = new List<Finding>();
        public List<AssetSummary> Assets = new List<AssetSummary>();
        public List<SceneVerdict> Scenes = new List<SceneVerdict>();
        /// <summary>Scope "Package": the .unitypackage the report is about; "" otherwise.</summary>
        public string PackageFile = "";
        /// <summary>What exporting the converted package did (Pro); File is "" without an export.</summary>
        public PackageExportSummary PackageExport = new PackageExportSummary();

        /// <summary>Recomputes Assets and Summary from Findings and Scenes.</summary>
        public void Summarize()
        {
            Assets = Findings.GroupBy(f => f.AssetPath)
                .Select(g => new AssetSummary
                {
                    Path = g.Key,
                    Kind = g.First().Kind,
                    Severity = SeverityUtil.Worst(g),
                    Tier = BreakingTier(g),
                    FindingCount = g.Count(),
                    UsedBy = g.SelectMany(f => f.UsedBy).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                    Severities = g.Select(f => f.Severity).Distinct().OrderByDescending(x => x).ToArray(),
                })
                .OrderByDescending(a => a.Severity).ThenBy(a => a.Kind).ThenBy(a => a.Path, StringComparer.Ordinal)
                .ToList();

            var s = new ReportSummary
            {
                ScenesTotal = Scenes.Count,
                ScenesPink = Scenes.Count(x => x.Has(Severity.Pink)),
                ScenesSilent = Scenes.Count(x => x.Has(Severity.Silent)),
                ScenesDegraded = Scenes.Count(x => x.Has(Severity.Degraded)),
                ScenesUnaffected = Scenes.Count(x => x.Has(Severity.Info)),
                AssetsWithFindings = Assets.Count,
                AssetsPink = Assets.Count(a => a.Severities.Contains(Severity.Pink)),
                AssetsSilent = Assets.Count(a => a.Severities.Contains(Severity.Silent)),
                AssetsDegraded = Assets.Count(a => a.Severities.Contains(Severity.Degraded)),
                AssetsInfo = Assets.Count(a => a.Severity == Severity.Info),
                ParseFailures = Findings.Count(f => f.RuleId == RuleIds.ParseFailed),
                ObservedPink = Scenes.Count(x => x.Observed == ObservedVerdict.Pink),
                ObservedChanged = Scenes.Count(x => x.Observed == ObservedVerdict.Changed),
                ObservedSame = Scenes.Count(x => x.Observed == ObservedVerdict.Same),
                ObservedNotCaptured = Scenes.Count(x => x.Observed == ObservedVerdict.NotCaptured),
            };
            foreach (var a in Assets)
            {
                switch (a.Tier)
                {
                    case FixTier.UnityConverter: s.TierUnityConverter++; break;
                    case FixTier.Auto: s.TierAuto++; break;
                    case FixTier.AutoApprox: s.TierAutoApprox++; break;
                    case FixTier.NotSupportedYet: s.TierNotSupportedYet++; break;
                    case FixTier.Manual: s.TierManual++; break;
                    case FixTier.Review: s.TierReview++; break;
                }
                if (a.Severity > Severity.Info && a.Tier >= FixTier.Auto)
                {
                    s.KitNeeded++;
                    if (a.Tier == FixTier.Auto || a.Tier == FixTier.AutoApprox || a.Tier == FixTier.Review) s.KitConvertible++;
                }
            }
            Summary = s;
        }

        /// <summary>
        /// The tier that decides how an asset gets fixed: the worst tier among findings that actually break something,
        /// or among all findings when nothing breaks.
        /// </summary>
        public static FixTier BreakingTier(IEnumerable<Finding> findings)
        {
            var list = findings as IList<Finding> ?? findings.ToList();
            var breaking = list.Where(f => f.Severity > Severity.Info).ToList();
            var source = breaking.Count > 0 ? breaking : list;
            var t = FixTier.None;
            foreach (var f in source) t = SeverityUtil.Max(t, f.Tier);
            return t;
        }
    }
}
