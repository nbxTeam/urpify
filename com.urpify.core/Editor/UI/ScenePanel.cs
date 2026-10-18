using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using Urpify.Pipeline;
using Urpify.Verify;

namespace Urpify.UI
{
    /// <summary>
    /// Right side for a selected scene: the static verdict (scanner) and the observed verdict (captures) side by side,
    /// the breaks and their assets, then every captured frame with the before/after comparison.
    /// </summary>
    public static class ScenePanel
    {
        public static void Build(VisualElement right, IWindowContext ctx, SceneVerdict sc, AssetSummary asset)
        {
            var report = ctx.Report;
            right.Add(UiKit.Header(sc.Severity, Path.GetFileName(sc.Path)));
            right.Add(UiKit.PathSubtitle(sc.Path));
            right.Add(UiKit.AssetButtons(sc.Path));

            // Only while a pink break is left: after the Urpify converters step a pink asset it converted (or one Unity's converter
            // upgraded) no longer asks for a viewpoint, and the scan's verdict says the migration fixed it (design review
            // 13.10.2026, item 1: the demo, converted in full, said "will turn pink, add a Migration Viewpoint" next to "Same").
            bool kitDone = MigrationState.Load().Get(StepId.KitConverters).Status == StepStatus.Done;
            var pinkCauses = report.Findings.Where(f => f.Severity == Severity.Pink && sc.Causes.Contains(f.AssetPath) && (!kitDone || !VerifyRunner.Fixed(f)))
                .Select(f => f.AssetPath).Distinct().ToList();
            var staticValue = Loc.Get("severity." + sc.Severity);
            if (kitDone && sc.Severity == Severity.Pink && pinkCauses.Count == 0) staticValue += " · " + Loc.Get("ui.scene.fixedByMigration");
            right.Add(Verdict(Loc.Get("ui.scene.static"), sc.Severity, staticValue, Loc.Get("ui.scene.staticWhat")));
            if (sc.Observed == ObservedVerdict.NotCaptured)
                right.Add(Verdict(Loc.Get("ui.scene.observed"), Severity.Info, Loc.Get("observed.NotCaptured"), Loc.Get("ui.scene.observedNone")));
            else
                right.Add(Verdict(Loc.Get("ui.scene.observed"), UiKit.ObservedIcon(sc.Observed), Loc.Get("observed." + sc.Observed),
                    Loc.Get("ui.scene.observedWhat", report.CompareBefore, report.CompareAfter)));

            // What the comparison adds: errors during rendering, a silent break the frames do not show (QA-002, QA-009).
            if (!string.IsNullOrEmpty(sc.ObservedNote))
            {
                var note = new Label(sc.ObservedNote) { tooltip = sc.RenderErrors != null && sc.RenderErrors.Length > 0 ? string.Join("\n", sc.RenderErrors) : "" };
                note.AddToClassList("urpify-manual");
                right.Add(note);
            }

            if (sc.PinkNotInFrame && pinkCauses.Count > 0)
            {
                var note = new Label(Loc.Get("ui.scene.pinkNotInFrame"));
                note.AddToClassList("urpify-manual");
                right.Add(note);
                foreach (var c in pinkCauses.Take(12)) { var link = c; right.Add(UiKit.PathLink(link, () => UiKit.Ping(link))); }
            }

            if (sc.Breaks.Length > 0)
            {
                right.Add(UiKit.H2(Loc.Get("ui.scene.breaks", sc.Breaks.Length)));
                foreach (var b in sc.Breaks)
                {
                    var sp = b.IndexOf(' ');
                    var sev = sp > 0 && Enum.TryParse(b.Substring(0, sp), out Severity s) ? s : Severity.Info;
                    var id = sp > 0 ? b.Substring(sp + 1) : b;
                    var row = new VisualElement();
                    row.AddToClassList("urpify-scene-row");
                    row.Add(new SeverityIcon(sev));
                    var example = report.Findings.FirstOrDefault(x => x.RuleId == id && x.Severity == sev);
                    var l = new Label(example != null ? example.LocalTitle : id) { tooltip = id };
                    l.AddToClassList("urpify-scene-path");
                    row.Add(l);
                    right.Add(row);
                }
            }
            if (sc.Causes.Length > 0)
            {
                right.Add(UiKit.H2(Loc.Get("ui.scene.causes", sc.Causes.Length)));
                foreach (var c in sc.Causes.Take(30)) { var link = c; right.Add(UiKit.PathLink(link, () => ctx.Select(link))); }
            }

            foreach (var v in sc.Viewpoints)
            {
                right.Add(UiKit.H2(Loc.Get("ui.scene.view." + v.Verdict, v.Name)));
                var metrics = UiKit.Note(Loc.Get("ui.scene.metricsShort", v.ChangedPixelsPct.ToString("0.##", UiKit.Inv), v.PinkAfterPct.ToString("0.###", UiKit.Inv)));
                metrics.tooltip = Loc.Get("ui.scene.metrics", v.PinkBeforePct.ToString("0.###", UiKit.Inv), v.PinkAfterPct.ToString("0.###", UiKit.Inv),
                    v.ChangedPixelsPct.ToString("0.##", UiKit.Inv), v.MeanDiff.ToString("0.#", UiKit.Inv));
                right.Add(metrics);
                right.Add(new CompareView(v.Before, v.After, v.Diff));
            }
            if (asset != null)
                foreach (var f in report.Findings.Where(x => x.AssetPath == asset.Path).OrderByDescending(x => x.Severity))
                    right.Add(AssetPanel.FindingCard(f));
            var bake = BakeSection(ctx, sc.Path, report.Findings.Any(f => f.AssetPath == sc.Path && f.RuleId == RuleIds.EnvironmentNotBaked));
            if (bake != null) right.Add(bake);
        }

        /// <summary>
        /// Bake environment (architect decision 2 after M8): offered for a scene with the SCENE.ENVIRONMENT_NOT_BAKED finding,
        /// run only when the user asks, with a backup; a done bake can be rolled back here.
        /// </summary>
        static VisualElement BakeSection(IWindowContext ctx, string scenePath, bool finding)
        {
            var done = EnvironmentBake.Done(scenePath);
            if (!finding && done == null) return null;
            var box = new VisualElement();
            box.Add(UiKit.H2(Loc.Get("ui.scene.bake.title")));
            // The finding card above says what this is for; the section holds the action, its meaning in the tooltip.
            if (done != null)
            {
                var text = new Label(done.Message) { tooltip = Loc.Get("ui.scene.bake.undoWhat") };
                text.AddToClassList("urpify-text");
                box.Add(text);
            }
            var status = new Label();
            status.AddToClassList("urpify-note");
            var buttons = new VisualElement();
            buttons.AddToClassList("urpify-buttons");
            if (done == null)
            {
                var why = EnvironmentBake.CannotBake(scenePath);
                var run = new Button(() =>
                {
                    if (!EditorUtility.DisplayDialog(Loc.Get("ui.scene.bake.confirmTitle"), Loc.Get("ui.scene.bake.confirmText", scenePath),
                            Loc.Get("ui.scene.bake.button"), Loc.Get("ui.cancel"))) return;
                    var error = EnvironmentBake.Bake(scenePath);
                    if (error != null) status.text = error;
                    else ctx.StartScan();
                }) { text = Loc.Get("ui.scene.bake.button"), tooltip = Loc.Get("ui.scene.bake.what") + " " + Loc.Get("ui.scene.bake.whatTip") };
                run.SetEnabled(why == null);
                if (why != null) status.text = why;
                buttons.Add(run);
            }
            else
            {
                buttons.Add(new Button(() =>
                {
                    if (!EditorUtility.DisplayDialog(Loc.Get("ui.scene.bake.undoTitle"), Loc.Get("ui.scene.bake.undoText", scenePath),
                            Loc.Get("ui.scene.bake.undo"), Loc.Get("ui.cancel"))) return;
                    var r = EnvironmentBake.Undo(scenePath);
                    if (r.Errors.Count > 0) status.text = string.Join("\n", r.Errors);
                    else ctx.StartScan();
                }) { text = Loc.Get("ui.scene.bake.undo") });
            }
            box.Add(buttons);
            box.Add(status);
            return box;
        }

        static VisualElement Verdict(string label, Severity icon, string value, string what)
        {
            var box = new VisualElement();
            box.AddToClassList("urpify-card");
            var head = new VisualElement();
            head.AddToClassList("urpify-card-head");
            head.Add(new SeverityIcon(icon));
            var t = new Label(label + ": " + value);
            t.AddToClassList("urpify-card-title");
            head.Add(t);
            box.Add(head);
            // What the verdict is based on goes in the tooltip; the card itself is the verdict.
            box.tooltip = what;
            return box;
        }
    }
}
