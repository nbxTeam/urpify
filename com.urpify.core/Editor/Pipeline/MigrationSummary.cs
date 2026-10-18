using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Urpify.Verify;

namespace Urpify.Pipeline
{
    /// <summary>Where an asset that broke stands after the migration.</summary>
    public enum AssetOutcome
    {
        /// <summary>Not migrated yet, or nothing breaks.</summary>
        None = 0,
        /// <summary>Unity's converter or Urpify fixed it (or it no longer breaks).</summary>
        Fixed = 1,
        /// <summary>Converted, but the result looks different from Built-in: someone should look at it.</summary>
        Check = 2,
        /// <summary>Still breaks: a later step fixes it, Urpify Pro does, or a person.</summary>
        Left = 3,
    }

    /// <summary>One asset of <see cref="MigrationSummary"/>.</summary>
    public sealed class OutcomeEntry
    {
        public string Path = "";
        public AssetKind Kind;
        public Severity Severity;
        public AssetOutcome Outcome;
        /// <summary>Who fixed it (Fixed, Check), or who fixes it (Left).</summary>
        public FixOwner By;
        /// <summary>The finding that says what breaks (from the scan before the migration when there was one).</summary>
        public Finding Finding;
        /// <summary>Urpify converters tried and gave up (its outcome Manual, Not supported yet or Restored).</summary>
        public bool UrpifyGaveUp;
    }

    /// <summary>
    /// The migration's result for the window (owner's check 19.10.2026, items 10 and 14): every asset the scan before the
    /// migration found broken, and every one a scan now finds broken, with what became of it. A scan of the migrated project
    /// alone cannot tell "already converted" from "will break" (a converted OnRenderImage script still has the method), so
    /// what the Urpify converters step did decides first, then the scan now, then the scan before.
    /// </summary>
    public sealed class MigrationSummary
    {
        /// <summary>Unity's converter (or, with Keep Built-in support, Urpify converters) has run: the project is migrated.</summary>
        public bool Migrated;
        /// <summary>The Urpify converters step is done.</summary>
        public bool UrpifyDone;
        public List<OutcomeEntry> Entries = new List<OutcomeEntry>();

        public IEnumerable<OutcomeEntry> Fixed => Entries.Where(e => e.Outcome == AssetOutcome.Fixed);
        public IEnumerable<OutcomeEntry> Attention => Entries.Where(e => e.Outcome == AssetOutcome.Check || e.Outcome == AssetOutcome.Left);

        public OutcomeEntry Get(string path) => Entries.FirstOrDefault(e => e.Path == path);

        /// <summary>True when the migration has started changing the project in a way the window should report as a result.</summary>
        public static bool IsMigrated(MigrationState st) =>
            st.Get(StepId.UnityConverter).Status == StepStatus.Done || st.Get(StepId.KitConverters).Status == StepStatus.Done;

        /// <param name="before">The scan before the migration (the Scan step's report); null when there is none.</param>
        /// <param name="now">A scan of the project as it is now; null when there is none yet.</param>
        /// <param name="kit">What the Urpify converters step did; null when it has not run.</param>
        public static MigrationSummary Build(MigrationState st, MigrationReport before, MigrationReport now, KitConverterReport kit, bool proInstalled)
        {
            var s = new MigrationSummary { Migrated = IsMigrated(st), UrpifyDone = st.Get(StepId.KitConverters).Status == StepStatus.Done };
            if (!s.UrpifyDone) kit = null;
            var outcomes = new Dictionary<string, string>(StringComparer.Ordinal);
            if (kit != null)
                foreach (var i in kit.Items.Where(i => i.Path.Length > 0 && KitOutcomes.Tier(i.Outcome) != null)) outcomes[i.Path] = i.Outcome;

            Finding Breaking(MigrationReport r, string path) => r?.Findings.Where(f => f.AssetPath == path && f.Severity > Severity.Info)
                .OrderByDescending(f => f.Severity).ThenByDescending(f => SeverityUtil.Rank(f.Tier)).FirstOrDefault();
            var paths = new List<string>();
            foreach (var r in new[] { before, now })
                if (r != null)
                    foreach (var a in r.Assets.Where(a => a.Severity > Severity.Info && !KitPaths.IsWrittenByUrpify(a.Path)))
                        if (!paths.Contains(a.Path)) paths.Add(a.Path);

            foreach (var path in paths)
            {
                var was = Breaking(before, path);
                var left = Breaking(now, path);
                var f = was ?? left;
                if (f == null) continue;
                var asset = (before ?? now).Assets.FirstOrDefault(a => a.Path == path) ?? now?.Assets.FirstOrDefault(a => a.Path == path);
                var e = new OutcomeEntry { Path = path, Kind = asset?.Kind ?? f.Kind, Severity = f.Severity, Finding = f, By = Edition.Owner(f) };
                s.Entries.Add(e);
                if (!s.Migrated) continue;
                if (outcomes.TryGetValue(path, out var outcome) || (f.RuleId == RuleIds.MaterialCustomShader && f.Evidence != null && outcomes.TryGetValue(f.Evidence, out outcome)))
                {
                    var tier = KitOutcomes.Tier(outcome).Value;
                    var owner = Edition.Owner(tier == FixTier.Review ? FixTier.Auto : tier, f.RuleId);
                    if (tier == FixTier.Auto || tier == FixTier.AutoApprox) { e.Outcome = AssetOutcome.Fixed; e.By = owner == FixOwner.Person ? FixOwner.Pro : owner; continue; }
                    if (tier == FixTier.Review) { e.Outcome = AssetOutcome.Check; e.By = FixOwner.Pro; continue; }
                    e.Outcome = AssetOutcome.Left; e.By = FixOwner.Person; e.UrpifyGaveUp = true;
                    continue;
                }
                if (now == null)
                {
                    // No scan since the migration: what the scan before expected of the steps that are done.
                    bool done = f.Tier == FixTier.UnityConverter || (s.UrpifyDone && VerifyRunner.Fixed(f));
                    e.Outcome = done ? AssetOutcome.Fixed : AssetOutcome.Left;
                    continue;
                }
                if (left == null)
                {
                    // It no longer breaks. Before Urpify converters ran, Urpify cannot be what fixed it (the owner's project: Unity's
                    // converter step left the Default-Material scene fine, and the result said "Fixed by Urpify").
                    var owner = was != null ? Edition.Owner(was) : FixOwner.Unity;
                    if (!s.UrpifyDone && (owner == FixOwner.Free || owner == FixOwner.Pro)) owner = FixOwner.None;
                    e.Outcome = AssetOutcome.Fixed;
                    e.By = owner;
                    continue;
                }
                e.Finding = left;
                e.Severity = left.Severity;
                e.By = Edition.Owner(left);
                // Urpify converters ran and did not take it: it is a person's now, or Pro's when Pro is not installed.
                if (s.UrpifyDone && (e.By == FixOwner.Free || (e.By == FixOwner.Pro && proInstalled))) e.By = FixOwner.Person;
                if (s.UrpifyDone && e.By == FixOwner.Unity) e.By = FixOwner.Person;
                e.Outcome = AssetOutcome.Left;
            }
            return s;
        }

        /// <summary>
        /// The report the window shows after the migration: the scan now, plus what the scan before found in the assets the scan
        /// now no longer lists (they are fixed, and the list still shows them with that status).
        /// </summary>
        public static MigrationReport WithEarlierFindings(MigrationReport now, MigrationReport before, MigrationSummary summary)
        {
            var r = JsonUtility.FromJson<MigrationReport>(JsonUtility.ToJson(now));
            if (before == null || summary == null) return r;
            var listed = new HashSet<string>(r.Findings.Select(f => f.AssetPath), StringComparer.Ordinal);
            foreach (var e in summary.Entries.Where(e => !listed.Contains(e.Path)))
                r.Findings.AddRange(before.Findings.Where(f => f.AssetPath == e.Path).Select(f => f.Clone()));
            r.Summarize();
            return r;
        }

        /// <summary>The worst-looking scene of a comparison: new pink first, then the most changed frame.</summary>
        public static SceneVerdict WorstScene(IEnumerable<SceneVerdict> scenes) =>
            (scenes ?? Enumerable.Empty<SceneVerdict>()).Where(x => x.Observed != ObservedVerdict.NotCaptured)
                .OrderByDescending(x => x.Observed == ObservedVerdict.Pink).ThenByDescending(x => x.Observed == ObservedVerdict.Changed)
                .ThenByDescending(x => x.Viewpoints.Count == 0 ? 0 : x.Viewpoints.Max(v => v.ChangedPixelsPct)).FirstOrDefault();

        // ------------------------------------------------------------------ latest comparison and report files

        public const string VerifyInternal = "verify-latest.internal.json";

        /// <summary>
        /// Puts the latest before/after comparison over a report that has none of its own (owner's check 19.10.2026, item 8: the
        /// window loaded a later scan and the scene said "Not captured" although the comparison had found pink). Scenes match by
        /// path; scenes only the comparison has are added. Nothing happens once the frames after are gone with a rollback.
        /// </summary>
        public static void OverlayComparison(MigrationReport report, MigrationReport comparison)
        {
            if (report == null || comparison == null || ReferenceEquals(report, comparison)) return;
            if (comparison.CompareAfter.Length == 0) return;
            var byPath = report.Scenes.ToDictionary(x => x.Path, StringComparer.OrdinalIgnoreCase);
            foreach (var c in comparison.Scenes.Where(x => x.Observed != ObservedVerdict.NotCaptured || !string.IsNullOrEmpty(x.ObservedNote)))
            {
                if (!byPath.TryGetValue(c.Path, out var sv))
                {
                    sv = new SceneVerdict { Path = c.Path, Severity = c.Severity, Tier = c.Tier, Causes = c.Causes, Rules = c.Rules, Breaks = c.Breaks };
                    report.Scenes.Add(sv);
                    byPath[c.Path] = sv;
                }
                sv.Observed = c.Observed;
                sv.Viewpoints = c.Viewpoints;
                sv.ObservedNote = c.ObservedNote;
                sv.RenderErrors = c.RenderErrors;
            }
            report.CompareBefore = comparison.CompareBefore;
            report.CompareAfter = comparison.CompareAfter;
            report.Summarize();
        }

        /// <summary>The latest comparison report (Capture after or Review), or null.</summary>
        public static MigrationReport LatestComparison(string root = ".")
        {
            MigrationReport best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (var name in new[] { VerifyInternal, "review-latest.internal.json" })
            {
                var p = Path.Combine(root, MigrationRunner.ReportsDir, name);
                if (!File.Exists(p)) continue;
                var r = Read(p);
                if (r == null || r.CompareAfter.Length == 0) continue;
                var t = File.GetLastWriteTimeUtc(p);
                if (t > bestTime) { best = r; bestTime = t; }
            }
            // The frames the comparison read must still be there: a rollback leaves them, but a deleted Urpify folder does not.
            if (best != null && CaptureManifest.Load(best.CompareAfter) == null && root == ".") return null;
            return best;
        }

        public static MigrationReport Read(string path)
        {
            try
            {
                var r = JsonUtility.FromJson<MigrationReport>(File.ReadAllText(path));
                return r != null && r.SchemaVersion == MigrationReport.CurrentSchemaVersion ? r : null;
            }
            catch { return null; }
        }
    }
}
