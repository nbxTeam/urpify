using System.IO;
using System.Linq;
using UnityEngine.UIElements;
using Urpify.Pipeline;

namespace Urpify.UI
{
    /// <summary>
    /// The right side after the migration, with nothing selected (owner's check 19.10.2026, item 14, and the rule for new
    /// screens): the result in one large line, the scene before and after with the curtain, then only what needs the user, each
    /// with its button, and what was fixed folded away. After Show what breaks (URP without Urpify's fixes) the same screen
    /// says what broke and offers Fix with Urpify or Roll back. Numbers stay in second place.
    /// </summary>
    public static class ResultPanel
    {
        const int MaxCards = 8;

        public static void Build(VisualElement right, IWindowContext ctx)
        {
            var s = ctx.Summary;
            var report = ctx.Report;
            var scenes = report.Scenes.Where(x => x.Observed != ObservedVerdict.NotCaptured).ToList();
            var badScenes = scenes.Where(x => x.Observed == ObservedVerdict.Pink || x.Observed == ObservedVerdict.Changed).ToList();
            var attention = s.Attention.OrderByDescending(e => e.Severity).ThenBy(e => Path.GetFileName(e.Path), System.StringComparer.OrdinalIgnoreCase).ToList();

            var page = new VisualElement();
            page.AddToClassList("urpify-result-page");
            right.Add(page);

            var (key, args) = Headline(s, report);
            var head = new VisualElement();
            head.AddToClassList("urpify-result-head");
            VisualElement icon = key.StartsWith("ui.result.done", System.StringComparison.Ordinal) ? new StatusIcon(StepStatus.Done)
                : (VisualElement)new SeverityIcon(badScenes.Any(x => x.Observed == ObservedVerdict.Pink) ? Severity.Pink : Severity.Silent);
            icon.AddToClassList("urpify-result-icon");
            head.Add(icon);
            var title = new Label(Loc.Get(key, args));
            title.AddToClassList("urpify-result-title");
            head.Add(title);
            page.Add(head);
            var sub = new Label(Subline(s));
            sub.AddToClassList("urpify-result-sub");
            page.Add(sub);

            // URP without Urpify's fixes: what to do next comes first.
            if (!s.UrpifyDone)
            {
                var actions = new VisualElement();
                actions.AddToClassList("urpify-buttons");
                var fix = new Button(() => Report(ctx, MigrationRunner.FixWithUrpify())) { text = Loc.Get("ui.result.fix"), tooltip = Loc.Get("ui.result.fixTip") };
                fix.AddToClassList("urpify-primary");
                fix.AddToClassList("urpify-big-button");
                fix.SetEnabled(!MigrationRunner.Busy);
                actions.Add(fix);
                var back = new Button(() => MigrateWindow.ConfirmRollback(null)) { text = Loc.Get("ui.result.rollBack"), tooltip = Loc.Get("ui.result.rollBackTip") };
                back.AddToClassList("urpify-big-button");
                back.SetEnabled(!MigrationRunner.Busy);
                actions.Add(back);
                page.Add(actions);
            }

            var worst = MigrationSummary.WorstScene(scenes);
            if (worst != null)
            {
                var view = worst.Viewpoints.OrderByDescending(v => v.ChangedPixelsPct).FirstOrDefault();
                var sceneHead = new VisualElement();
                sceneHead.AddToClassList("urpify-result-scene");
                var name = new Label(Path.GetFileNameWithoutExtension(worst.Path)) { tooltip = worst.Path };
                name.AddToClassList("urpify-h2");
                sceneHead.Add(name);
                var verdict = new Label(SceneText(worst.Observed, view?.ChangedPixelsPct ?? 0));
                verdict.AddToClassList("urpify-result-verdict");
                sceneHead.Add(verdict);
                page.Add(sceneHead);
                if (view != null) page.Add(new CompareView(view.Before, view.After, view.Diff));
                if (scenes.Count > 1)
                    page.Add(UiKit.Link(Loc.Get("ui.result.allScenes", scenes.Count), CompareScenesWindow.Open));
            }

            if (badScenes.Count + attention.Count > 0)
            {
                page.Add(UiKit.H2(Loc.Get("ui.result.attention")));
                foreach (var sc in badScenes.Take(MaxCards))
                {
                    var path = sc.Path;
                    page.Add(Card(new SeverityIcon(sc.Observed == ObservedVerdict.Pink ? Severity.Pink : Severity.Silent), Path.GetFileName(path), path,
                        Loc.Get(sc.Observed == ObservedVerdict.Pink ? "ui.result.card.scenePink" : "ui.result.card.sceneChanged"),
                        Loc.Get("ui.result.compare"), () => ctx.Select(path), false));
                }
                foreach (var e in attention.Take(MaxCards))
                {
                    var path = e.Path;
                    bool locked = e.By == FixOwner.Pro && !Edition.ProInstalled;
                    page.Add(Card(new SeverityIcon(e.Outcome == AssetOutcome.Check ? Severity.Degraded : e.Severity), Path.GetFileName(path),
                        path + (e.Finding != null ? "\n" + e.Finding.LocalTitle : ""), OutcomeText(e, s), Loc.Get("ui.result.show"), () => ctx.Select(path), locked));
                }
                int more = attention.Count - MaxCards;
                if (more > 0)
                    page.Add(UiKit.Link(Loc.Get("ui.result.more", more), () => ctx.ToggleFilter(new QuickFilter
                    {
                        Id = "outcome." + AssetOutcome.Left,
                        Label = Loc.Get("ui.strip.outcome." + AssetOutcome.Left, attention.Count),
                        Match = (r, a) => s.Get(a.Path) is OutcomeEntry x && (x.Outcome == AssetOutcome.Left || x.Outcome == AssetOutcome.Check),
                    })));
            }

            var fixedList = s.Fixed.OrderBy(e => e.By).ThenBy(e => Path.GetFileName(e.Path), System.StringComparer.OrdinalIgnoreCase).ToList();
            if (fixedList.Count > 0)
            {
                var fold = new Foldout { text = Loc.Get("ui.result.fixedFold", fixedList.Count), value = false };
                fold.AddToClassList("urpify-result-fixed");
                foreach (var e in fixedList)
                {
                    var row = new VisualElement();
                    row.AddToClassList("urpify-scene-row");
                    row.Add(new StatusIcon(StepStatus.Done));
                    var path = e.Path;
                    var link = UiKit.Link(Path.GetFileName(path), () => ctx.Select(path));
                    link.tooltip = path;
                    link.AddToClassList("urpify-scene-path");
                    row.Add(link);
                    var by = new Label(OutcomeText(e, s));
                    by.AddToClassList("urpify-row-tier");
                    row.Add(by);
                    fold.Add(row);
                }
                page.Add(fold);
            }

            // The page ends with what can still be done with the migration as a whole.
            var foot = new VisualElement();
            foot.AddToClassList("urpify-result-foot");
            if (s.UrpifyDone)
            {
                var back = UiKit.Link(Loc.Get("ui.result.rollBackAll"), () => MigrateWindow.ConfirmRollback(null));
                back.tooltip = Loc.Get("ui.result.rollBackTip");
                foot.Add(back);
            }
            foot.Add(UiKit.Link(Loc.Get("ui.result.steps"), MigrateWindow.Open));
            foot.Add(UiKit.Link(Loc.Get("ui.result.report"), () => OpenReport()));
            page.Add(foot);
        }

        static void Report(IWindowContext ctx, string error)
        {
            if (error != null && ctx is UnityEditor.EditorWindow w) w.ShowNotification(new UnityEngine.GUIContent(error));
        }

        static void OpenReport()
        {
            foreach (var kind in new[] { "review", "verify", "scan" })
            {
                var p = Path.Combine(MigrationRunner.ReportsDir, $"urp-migration-{kind}-latest.html");
                if (File.Exists(p)) { UnityEditor.EditorUtility.OpenWithDefaultApp(Path.GetFullPath(p)); return; }
            }
        }

        static VisualElement Card(VisualElement icon, string name, string tip, string text, string button, System.Action action, bool locked)
        {
            var card = new VisualElement();
            card.AddToClassList("urpify-card");
            card.AddToClassList("urpify-result-card");
            var body = new VisualElement();
            body.AddToClassList("urpify-result-card-body");
            var head = new VisualElement();
            head.AddToClassList("urpify-card-head");
            head.Add(icon);
            var t = new Label(name) { tooltip = tip };
            t.AddToClassList("urpify-card-title");
            head.Add(t);
            if (locked) head.Add(UiKit.ProBadge(Loc.Get("owner.Pro")));
            body.Add(head);
            var l = new Label(text);
            l.AddToClassList("urpify-text");
            body.Add(l);
            card.Add(body);
            var b = new Button(action) { text = button };
            b.AddToClassList("urpify-result-card-button");
            card.Add(b);
            return card;
        }

        /// <summary>The large line's key and arguments for this result and report.</summary>
        public static (string key, object[] args) Headline(MigrationSummary s, MigrationReport report)
        {
            var scenes = report.Scenes.Where(x => x.Observed != ObservedVerdict.NotCaptured).ToList();
            return Headline(s, scenes.Count, scenes.Count(x => x.Observed == ObservedVerdict.Pink),
                scenes.Count(x => x.Observed == ObservedVerdict.Pink || x.Observed == ObservedVerdict.Changed), s.Attention.Count());
        }

        /// <summary>The large line's key and arguments (tested without a window).</summary>
        public static (string key, object[] args) Headline(MigrationSummary s, int captured, int pink, int bad, int attention)
        {
            if (!s.UrpifyDone)
            {
                if (captured == 0) return ("ui.result.breaks.noFrames", new object[0]);
                if (pink > 0) return captured == 1 ? ("ui.result.breaks.pinkOne", new object[0]) : ("ui.result.breaks.pink", new object[] { pink, captured });
                if (bad > 0) return captured == 1 ? ("ui.result.breaks.changedOne", new object[0]) : ("ui.result.breaks.changed", new object[] { bad, captured });
                return ("ui.result.breaks.same", new object[0]);
            }
            int things = attention + bad;
            if (things == 0) return captured > 0 ? ("ui.result.done.same", new object[0]) : ("ui.result.done", new object[0]);
            return things == 1 ? ("ui.result.almostOne", new object[0]) : ("ui.result.almost", new object[] { things });
        }

        /// <summary>The smaller line under the large one: what Urpify can still fix, or what it fixed.</summary>
        public static string Subline(MigrationSummary s)
        {
            int fixedN = s.Fixed.Count();
            if (!s.UrpifyDone)
            {
                var left = s.Entries.Where(e => e.Outcome == AssetOutcome.Left).ToList();
                int free = left.Count(e => e.By == FixOwner.Free), pro = left.Count(e => e.By == FixOwner.Pro);
                if (Edition.ProInstalled) return free + pro == 0 ? Loc.Get("ui.result.sub.nothingToFix") : Loc.Get(free + pro == 1 ? "ui.result.sub.canFixAllOne" : "ui.result.sub.canFixAll", free + pro);
                if (free + pro == 0) return Loc.Get("ui.result.sub.nothingToFix");
                if (pro > 0) return free == 0 ? Loc.Get(pro == 1 ? "ui.result.sub.onlyProOne" : "ui.result.sub.onlyPro", pro) : Loc.Get("ui.result.sub.canFixFreePro", free, pro);
                return Loc.Get(free == 1 ? "ui.result.sub.canFixFreeOne" : "ui.result.sub.canFixFree", free);
            }
            return fixedN == 0 ? Loc.Get("ui.result.sub.fixedNone") : Loc.Get(fixedN == 1 ? "ui.result.sub.fixedOne" : "ui.result.sub.fixed", fixedN);
        }

        /// <summary>What became of an asset, or who still fixes it, as one sentence.</summary>
        public static string OutcomeText(OutcomeEntry e, MigrationSummary s)
        {
            switch (e.Outcome)
            {
                case AssetOutcome.Fixed:
                    return e.By == FixOwner.Unity || e.By == FixOwner.Free || e.By == FixOwner.Pro ? Loc.Get("outcome.fixed." + e.By) : Loc.Get("outcome.fixed.None");
                case AssetOutcome.Check:
                    return Loc.Get("outcome.check");
                case AssetOutcome.Left:
                    if (e.UrpifyGaveUp) return Loc.Get("outcome.left.gaveUp");
                    if (e.By == FixOwner.Pro && !Edition.ProInstalled) return Loc.Get("outcome.left.needsPro");
                    if (s != null && !s.UrpifyDone && (e.By == FixOwner.Free || e.By == FixOwner.Pro)) return Loc.Get("outcome.left.nextStep");
                    if (e.By == FixOwner.Unity) return Loc.Get("outcome.left.unity");
                    return Loc.Get("outcome.left.manual");
                default:
                    return "";
            }
        }

        /// <summary>A scene's frames in words.</summary>
        public static string SceneText(ObservedVerdict v, double changedPct)
        {
            switch (v)
            {
                case ObservedVerdict.Same: return Loc.Get("ui.result.scene.Same");
                case ObservedVerdict.Pink: return Loc.Get("ui.result.scene.Pink");
                case ObservedVerdict.Changed: return Loc.Get("ui.result.scene.Changed", changedPct.ToString("0.#", UiKit.Inv));
                default: return Loc.Get("ui.result.scene.NotCaptured");
            }
        }
    }
}
