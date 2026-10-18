using System;
using System.Linq;
using UnityEngine.UIElements;

namespace Urpify.UI
{
    /// <summary>Right side with nothing selected: the lead number, scene tiles, who fixes it, the scene list.</summary>
    public static class OverviewPanel
    {
        public static void Build(VisualElement right, IWindowContext ctx)
        {
            var report = ctx.Report;
            var s = report.Summary;
            var lead = new VisualElement();
            lead.AddToClassList("urpify-lead");
            // No scenes in scope (a folder of shaders, a package of assets): the lead counts assets instead.
            bool byAssets = s.ScenesTotal == 0;
            var leadSev = byAssets
                ? (s.AssetsPink > 0 ? Severity.Pink : s.AssetsSilent > 0 ? Severity.Silent : Severity.Info)
                : (s.ScenesPink > 0 ? Severity.Pink : s.ScenesSilent > 0 ? Severity.Silent : Severity.Info);
            lead.Add(new SeverityIcon(leadSev));
            var leadNumber = byAssets
                ? (leadSev == Severity.Pink ? s.AssetsPink : leadSev == Severity.Silent ? s.AssetsSilent : s.AssetsWithFindings - s.AssetsPink - s.AssetsSilent)
                : (leadSev == Severity.Pink ? s.ScenesPink : leadSev == Severity.Silent ? s.ScenesSilent : s.ScenesUnaffected);
            var num = new Label(leadNumber.ToString());
            num.AddToClassList("urpify-lead-number");
            num.AddToClassList(SeverityIcon.Class(leadSev));
            lead.Add(num);
            var leadKey = leadSev == Severity.Pink ? "ui.overview.leadPink" : leadSev == Severity.Silent ? "ui.overview.leadSilent" : "ui.overview.leadNone";
            var leadText = new Label(byAssets ? Loc.Get(leadKey + "Assets", s.AssetsWithFindings) : Loc.Get(leadKey, s.ScenesTotal));
            leadText.AddToClassList("urpify-lead-text");
            lead.Add(leadText);
            lead.tooltip = Loc.Get("report.severityNote");
            right.Add(lead);

            // Scope = a package (architect decision 8 after M5): the asset author's main action, on the overview.
            if (Pipeline.PackageScope.IsOpen && ctx is UrpifyWindow window)
            {
                var row = new VisualElement();
                row.AddToClassList("urpify-pro-row");
                var export = new Button(() => window.ExportPackage(ShaderOutput.KeepBuiltInSupport)) { text = Loc.Get("ui.overview.exportBoth"), tooltip = Loc.Get("ui.overview.exportBothTip") };
                export.AddToClassList("urpify-primary");
                export.AddToClassList("urpify-big-button");
                export.SetEnabled(Edition.ProInstalled);
                row.Add(export);
                if (!Edition.ProInstalled) row.Add(UiKit.ProBadge(Loc.Get("ui.overview.exportBothPro")));
                right.Add(row);
            }

            // Show what breaks (owner's check 19.10.2026, item 3): the owner's own first step with the Free package, one button.
            if (!Pipeline.PackageScope.IsOpen && report.Scope == "Project" && report.Scenes.Count > 0
                && Pipeline.MigrationRunner.ShowBreaksPlan(Pipeline.MigrationState.Load(), true).Count > 0)
            {
                var card = new VisualElement();
                card.AddToClassList("urpify-card");
                card.AddToClassList("urpify-breaks-card");
                var t = new Label(Loc.Get("ui.breaks.title"));
                t.AddToClassList("urpify-card-title");
                card.Add(t);
                var d = new Label(Loc.Get("ui.breaks.text"));
                d.AddToClassList("urpify-text");
                card.Add(d);
                var b = new Button(MigrateWindow.StartShowWhatBreaks) { text = Loc.Get("ui.breaks.button"), tooltip = Loc.Get("ui.breaks.tip") };
                b.AddToClassList("urpify-primary");
                b.AddToClassList("urpify-big-button");
                b.SetEnabled(!Pipeline.MigrationRunner.Busy);
                card.Add(b);
                right.Add(card);
            }

            // The scene counts are in the strip above; the overview does not repeat them as tiles.
            // Who fixes the broken assets (TZ M8 item 4), counted by asset like the list: Unity's converter and the
            // Free package, Pro (a lock in Free), a person.
            var (byFree, byPro, byPerson) = SummaryStrip.Owners(report);
            right.Add(UiKit.H2(Loc.Get("ui.overview.whoFixes", byFree + byPro + byPerson)));
            right.Add(UiKit.Kv(Loc.Get("ui.overview.byFree"), byFree));
            var pro = UiKit.Kv(Edition.ProInstalled ? Loc.Get("ui.overview.byPro") : Loc.Get("ui.overview.needPro"), byPro);
            if (!Edition.ProInstalled) pro.Add(UiKit.ProBadge(Loc.Get("ui.summary.fixesProTip", byPro)));
            right.Add(pro);
            right.Add(UiKit.Kv(Loc.Get("ui.overview.byHand"), byPerson));

            if (report.Scenes.Count > 0)
            {
                right.Add(UiKit.H2(Loc.Get("ui.overview.scenes", report.Scenes.Count)));
                foreach (var sc in report.Scenes.OrderByDescending(x => x.Severity).ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
                {
                    var row = new VisualElement();
                    row.AddToClassList("urpify-scene-row");
                    row.Add(new SeverityIcon(sc.Severity) { tooltip = Loc.Get("ui.scene.static") + ": " + Loc.Get("severity." + sc.Severity) });
                    if (sc.Observed != ObservedVerdict.NotCaptured)
                        row.Add(new SeverityIcon(UiKit.ObservedIcon(sc.Observed)) { tooltip = Loc.Get("ui.scene.observed") + ": " + Loc.Get("observed." + sc.Observed) });
                    var path = sc.Path;
                    var l = UiKit.Link(path, () => ctx.Select(path));
                    l.AddToClassList("urpify-scene-path");
                    l.style.unityTextOverflowPosition = TextOverflowPosition.Middle;
                    l.tooltip = sc.Causes.Length > 0 ? Loc.Get("ui.overview.causes", string.Join("\n", sc.Causes.Take(10))) : Loc.Get("severity." + sc.Severity);
                    row.Add(l);
                    right.Add(row);
                }
            }
        }

        /// <summary>No report yet: what Urpify does and the first action.</summary>
        public static void BuildEmpty(VisualElement right, IWindowContext ctx)
        {
            var box = new VisualElement();
            box.AddToClassList("urpify-empty");
            var t = new Label(Loc.Get("ui.empty.title"));
            t.AddToClassList("urpify-empty-title");
            box.Add(t);
            var d = new Label(Loc.Get("ui.empty.text"));
            d.AddToClassList("urpify-empty-text");
            box.Add(d);
            var b = new Button(ctx.StartScan) { text = Loc.Get("ui.empty.scan") };
            b.AddToClassList("urpify-primary");
            box.Add(b);
            // The demo, for trying Urpify before running it on a real project (architect decision 3 of 13.10.2026).
            if (DemoImport.Available)
            {
                var demo = new Button(DemoImport.Import) { text = Loc.Get("ui.demo.import"), tooltip = Loc.Get("ui.demo.tip") };
                demo.style.marginTop = 6;
                box.Add(demo);
            }
            // The documentation: a .unitypackage install has no Package Manager page to find it from (18.10.2026).
            var docs = new Button(DocumentationLink.Open) { text = Loc.Get("ui.docs.open"), tooltip = Loc.Get("ui.docs.tip") };
            docs.AddToClassList("urpify-link");
            docs.style.marginTop = 6;
            docs.style.minHeight = 24;
            box.Add(docs);
            right.Add(box);
        }
    }
}
