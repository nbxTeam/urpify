using System;
using System.Linq;
using UnityEngine.UIElements;
using Urpify.Pipeline;

namespace Urpify.UI
{
    /// <summary>A filter of the list that a count in the strip turns on: the assets the count counts.</summary>
    public sealed class QuickFilter
    {
        public string Id = "";
        /// <summary>What the list says it shows while the filter is on (the count's own words).</summary>
        public string Label = "";
        public Func<MigrationReport, AssetSummary, bool> Match = (r, a) => true;
    }

    /// <summary>
    /// The strip under the toolbar (owner's check 19.10.2026, item 4): counts in groups, each group with its name, a divider
    /// between groups, the groups wrapping as whole groups in a narrow window, and the scan time at the end. Every count is a
    /// button that filters the list to what it counts and shows as pressed while it does; its words are in the tooltip.
    /// Before the migration the groups are Scenes, Assets and Captured; after it, Result (fixed, to check, to do) and Captured.
    /// Who fixes what is on the overview.
    /// </summary>
    public static class SummaryStrip
    {
        public static void Build(VisualElement summary, IWindowContext ctx)
        {
            var report = ctx.Report;
            summary.Clear();
            if (report == null)
            {
                var none = new Label(Loc.Get("ui.summary.none"));
                none.AddToClassList("urpify-strip-empty");
                summary.Add(none);
                return;
            }
            var s = report.Summary;
            var groups = new VisualElement();
            groups.AddToClassList("urpify-strip-groups");
            summary.Add(groups);
            VisualElement group = null;
            void Group(string label)
            {
                if (group != null)
                {
                    var divider = new VisualElement();
                    divider.AddToClassList("urpify-strip-divider");
                    group.Add(divider);
                }
                group = new VisualElement();
                group.AddToClassList("urpify-strip-group");
                var g = new Label(label);
                g.AddToClassList("urpify-strip-label");
                group.Add(g);
                groups.Add(group);
            }
            void Chip(VisualElement icon, int n, string tip, QuickFilter filter)
            {
                var chip = new Button(() => ctx.ToggleFilter(filter)) { tooltip = tip };
                chip.AddToClassList("urpify-chip");
                if (ctx.ActiveFilter == filter.Id) chip.AddToClassList("urpify-chip-on");
                chip.SetEnabled(n > 0 || ctx.ActiveFilter == filter.Id);
                chip.Add(icon);
                var number = new Label(n.ToString());
                number.AddToClassList("urpify-chip-number");
                chip.Add(number);
                group.Add(chip);
            }
            void Sev(Severity sev, int n, string key, QuickFilter f) { f.Label = Loc.Get(key, n); Chip(new SeverityIcon(sev), n, f.Label, f); }

            var migration = ctx.Summary;
            if (migration != null && migration.Migrated)
            {
                Group(Loc.Get("ui.strip.result"));
                int fixedN = migration.Fixed.Count(), check = migration.Entries.Count(e => e.Outcome == AssetOutcome.Check), left = migration.Entries.Count(e => e.Outcome == AssetOutcome.Left);
                void Outcome(AssetOutcome o, int n, VisualElement icon)
                {
                    var f = new QuickFilter { Id = "outcome." + o, Label = Loc.Get("ui.strip.outcome." + o, n), Match = (r, a) => migration.Get(a.Path)?.Outcome == o };
                    Chip(icon, n, f.Label, f);
                }
                Outcome(AssetOutcome.Fixed, fixedN, new StatusIcon(StepStatus.Done));
                Outcome(AssetOutcome.Check, check, new SeverityIcon(Severity.Degraded));
                Outcome(AssetOutcome.Left, left, new SeverityIcon(Severity.Silent));
            }
            else
            {
                Group(Loc.Get("ui.summary.scenes", s.ScenesTotal));
                Sev(Severity.Pink, s.ScenesPink, "summary.scenePink", SceneFilter("scenes.pink", Severity.Pink));
                Sev(Severity.Silent, s.ScenesSilent, "summary.sceneSilent", SceneFilter("scenes.silent", Severity.Silent));
                if (s.ScenesDegraded > 0) Sev(Severity.Degraded, s.ScenesDegraded, "summary.sceneDegraded", SceneFilter("scenes.degraded", Severity.Degraded));
                Sev(Severity.Info, s.ScenesUnaffected, "summary.sceneUnaffected", SceneFilter("scenes.info", Severity.Info));
                Group(Loc.Get("ui.summary.assets", s.AssetsWithFindings));
                Sev(Severity.Pink, s.AssetsPink, "summary.assetPink", AssetFilter("assets.pink", Severity.Pink));
                Sev(Severity.Silent, s.AssetsSilent, "summary.assetSilent", AssetFilter("assets.silent", Severity.Silent));
                if (s.AssetsDegraded > 0) Sev(Severity.Degraded, s.AssetsDegraded, "summary.assetDegraded", AssetFilter("assets.degraded", Severity.Degraded));
            }
            if (report.CompareAfter.Length > 0)
            {
                Group(Loc.Get("ui.summary.captured"));
                Sev(Severity.Pink, s.ObservedPink, "summary.observedPink", ObservedFilter("observed.pink", ObservedVerdict.Pink));
                Sev(Severity.Silent, s.ObservedChanged, "summary.observedChanged", ObservedFilter("observed.changed", ObservedVerdict.Changed));
                Sev(Severity.Info, s.ObservedSame, "summary.observedSame", ObservedFilter("observed.same", ObservedVerdict.Same));
            }
            var time = new Label(Loc.Get("ui.summary.lastScan", UiKit.LocalTime(report.GeneratedUtc)))
            {
                tooltip = Loc.Get("report.scanStats", report.FilesScanned, report.FilesFromCache, report.ScanSeconds.ToString("0.0", UiKit.Inv), s.ParseFailures)
                          + "\n" + Loc.Get("ui.summary.overlap"),
            };
            time.AddToClassList("urpify-strip-time");
            summary.Add(time);
        }

        /// <summary>Scenes whose verdict (the worst break among what they use) is <paramref name="sev"/>; "unaffected" = Info.</summary>
        public static QuickFilter SceneFilter(string id, Severity sev) => new QuickFilter
        {
            Id = id,
            Match = (r, a) => a.Kind == AssetKind.Scene && (r.Scenes.FirstOrDefault(x => x.Path == a.Path)?.Has(sev) ?? a.Severity == sev),
        };

        /// <summary>Assets other than scenes with a finding of <paramref name="sev"/> (the strip's asset counts count those).</summary>
        public static QuickFilter AssetFilter(string id, Severity sev) => new QuickFilter
        {
            Id = id,
            Match = (r, a) => a.Kind != AssetKind.Scene && a.Severities.Contains(sev),
        };

        /// <summary>Scenes whose frames after the migration got this verdict.</summary>
        public static QuickFilter ObservedFilter(string id, ObservedVerdict v) => new QuickFilter
        {
            Id = id,
            Match = (r, a) => a.Kind == AssetKind.Scene && r.Scenes.Any(x => x.Path == a.Path && x.Observed == v),
        };

        /// <summary>
        /// Who fixes the broken assets (TZ M8 item 4: "Free fixed N, Pro fixes M more"): Unity's converter and the Free
        /// package, Pro, a person. Counted by asset, the way the list shows them.
        /// </summary>
        public static (int byFree, int byPro, int byPerson) Owners(MigrationReport report)
        {
            int byFree = 0, byPro = 0, byPerson = 0;
            foreach (var a in report.Assets.Where(a => a.Severity > Severity.Info))
                switch (Edition.Owner(report, a))
                {
                    case FixOwner.Unity: case FixOwner.Free: byFree++; break;
                    case FixOwner.Pro: byPro++; break;
                    case FixOwner.Person: byPerson++; break;
                }
            return (byFree, byPro, byPerson);
        }
    }
}
