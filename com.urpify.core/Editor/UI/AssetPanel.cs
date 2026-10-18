using System.IO;
using System.Linq;
using UnityEngine.UIElements;
using Urpify.Pipeline;

namespace Urpify.UI
{
    /// <summary>Right side for a selected asset: header, its result after the migration, its findings, and what uses it.</summary>
    public static class AssetPanel
    {
        public static void Build(VisualElement right, IWindowContext ctx, AssetSummary asset)
        {
            var report = ctx.Report;
            var entry = ctx.Summary?.Migrated == true ? ctx.Summary.Get(asset.Path) : null;
            bool after = entry != null && entry.Outcome != AssetOutcome.None;
            // Who fixes it, or after the migration what became of it, instead of the converters' tiers (owner's check 19.10.2026, item 7).
            var owner = asset.Severity > Severity.Info || asset.Tier != FixTier.None ? Edition.Owner(report, asset) : FixOwner.None;
            var badge = after ? Loc.Get("outcome.short." + entry.Outcome) : owner != FixOwner.None ? UiKit.OwnerShort(owner) : null;
            right.Add(UiKit.Header(asset.Severity, Path.GetFileName(asset.Path), badge));
            right.Add(UiKit.PathSubtitle(asset.Path));
            right.Add(UiKit.AssetButtons(asset.Path));
            if (after)
            {
                var result = new Label(ResultPanel.OutcomeText(entry, ctx.Summary));
                result.AddToClassList(entry.Outcome == AssetOutcome.Fixed ? "urpify-result" : "urpify-manual");
                right.Add(result);
            }

            foreach (var f in report.Findings.Where(x => x.AssetPath == asset.Path).OrderByDescending(x => x.Severity).ThenBy(x => x.Line))
                right.Add(FindingCard(f, after && entry.Outcome == AssetOutcome.Fixed));

            if (asset.UsedBy.Length > 0)
            {
                right.Add(UiKit.H2(Loc.Get("ui.details.usedBy", asset.UsedBy.Length)));
                foreach (var u in asset.UsedBy.Take(50)) { var path = u; right.Add(UiKit.PathLink(path, () => UiKit.Ping(path))); }
                if (asset.UsedBy.Length > 50) right.Add(UiKit.Note(Loc.Get("report.andMore", asset.UsedBy.Length - 50).Trim()));
            }
        }

        /// <summary>One finding: title, who fixes it, explanation, manual steps, evidence, link to the line.</summary>
        /// <param name="fixedAlready">The migration fixed the asset: the card says what broke, without asking for work.</param>
        public static VisualElement FindingCard(Finding f, bool fixedAlready = false)
        {
            var card = new VisualElement();
            card.AddToClassList("urpify-card");
            var head = new VisualElement();
            head.AddToClassList("urpify-card-head");
            head.Add(new SeverityIcon(f.Severity));
            // The rule id is for reports and settings overrides; the card shows it only in the title's tooltip.
            var t = new Label(f.LocalTitle) { tooltip = f.RuleId };
            t.AddToClassList("urpify-card-title");
            head.Add(t);
            var owner = Edition.Owner(f);
            if (owner != FixOwner.None && !fixedAlready)
            {
                var accuracy = UiKit.Accuracy(f.Tier);
                var who = new Label(UiKit.OwnerLong(owner)) { tooltip = accuracy };
                who.AddToClassList("urpify-card-tier");
                head.Add(who);
            }
            card.Add(head);
            var ex = new Label(f.LocalExplanation);
            ex.AddToClassList("urpify-text");
            card.Add(ex);
            if (!string.IsNullOrEmpty(f.ManualSteps) && !fixedAlready)
            {
                var m = new Label(f.LocalManualSteps);
                m.AddToClassList("urpify-manual");
                card.Add(m);
            }
            if (!string.IsNullOrEmpty(f.Evidence))
            {
                var code = new Label(f.Evidence);
                code.AddToClassList("urpify-code");
                card.Add(code);
            }
            var foot = new VisualElement();
            foot.AddToClassList("urpify-used");
            if (f.Line > 0) foot.Add(UiKit.Link(Loc.Get("ui.details.openAtLine", f.Line), () => UiKit.OpenAsset(f.AssetPath, f.Line)));
            if (foot.childCount > 0) card.Add(foot);
            return card;
        }
    }
}
