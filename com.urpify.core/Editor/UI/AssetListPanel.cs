using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Urpify.UI
{
    /// <summary>Left side: assets grouped by kind, with search, Issue / Who fixes / Type filters and the strip's filter.</summary>
    public sealed class AssetListPanel
    {
        sealed class Node
        {
            public AssetKind Kind;
            public AssetSummary Asset;   // null for group rows
            public int Count;
            public Severity Severity;
            /// <summary>Group row after the migration: every asset in it is fixed, so it shows a tick.</summary>
            public bool AllFixed;
        }

        readonly IWindowContext ctx;
        readonly TreeView tree;
        readonly Label emptyList;
        readonly HashSet<Severity> severityFilter = new HashSet<Severity>();
        readonly HashSet<FixOwner> ownerFilter = new HashSet<FixOwner>();
        readonly HashSet<AssetKind> kindFilter = new HashSet<AssetKind>();
        QuickFilter quick;
        string search = "";

        /// <summary>The strip's filter that is on, or null.</summary>
        public QuickFilter Quick => quick;

        /// <summary>Turns the strip's filter on, or off when it is the one already on.</summary>
        public void ToggleQuick(QuickFilter f)
        {
            quick = f == null || (quick != null && quick.Id == f.Id) ? null : f;
            Rebuild();
        }

        /// <summary>Raised when the user selects a row (null for a group row).</summary>
        public event Action<string> Selected;

        public AssetListPanel(VisualElement root, IWindowContext ctx)
        {
            this.ctx = ctx;
            emptyList = root.Q<Label>("empty-list");
            var searchField = root.Q<ToolbarSearchField>("search");
            searchField.tooltip = Loc.Get("ui.search.tooltip");
            searchField.RegisterValueChangedCallback(e => { search = e.newValue ?? ""; Rebuild(); });

            BuildFilterMenu(root.Q<ToolbarMenu>("issue-filter"), Loc.Get("ui.filter.issue"),
                new[] { Severity.Pink, Severity.Silent, Severity.Degraded, Severity.Info }, s => Loc.Get("severity." + s), severityFilter);
            // Who fixes it and whether it is free, not the converters' tiers (owner's check 19.10.2026, item 7).
            var fixMenu = root.Q<ToolbarMenu>("fix-filter");
            fixMenu.tooltip = Loc.Get("ui.filter.ownerTip");
            BuildFilterMenu(fixMenu, Loc.Get("ui.filter.owner"),
                new[] { FixOwner.Unity, FixOwner.Free, FixOwner.Pro, FixOwner.Person, FixOwner.None }, o => Loc.Get("ui.filter.owner." + o), ownerFilter);
            BuildFilterMenu(root.Q<ToolbarMenu>("type-filter"), Loc.Get("ui.filter.type"),
                new[] { AssetKind.Shader, AssetKind.Script, AssetKind.Material, AssetKind.Scene, AssetKind.Prefab, AssetKind.Animation, AssetKind.Settings, AssetKind.Project },
                k => Loc.Get("kind." + k), kindFilter);

            tree = root.Q<TreeView>("tree");
            tree.fixedItemHeight = 22;
            tree.selectionType = SelectionType.Single;
            tree.makeItem = MakeRow;
            tree.bindItem = BindRow;
            tree.selectionChanged += OnSelection;
            tree.itemsChosen += OnChosen;
        }

        void BuildFilterMenu<T>(ToolbarMenu menu, string label, T[] values, Func<T, string> name, HashSet<T> set)
        {
            void UpdateText() => menu.text = set.Count == 0 ? label : label + " (" + set.Count + ")";
            UpdateText();
            menu.menu.AppendAction(Loc.Get("ui.filter.all"), _ => { set.Clear(); UpdateText(); Rebuild(); },
                _ => set.Count == 0 ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            menu.menu.AppendSeparator();
            foreach (var v in values)
            {
                var value = v;
                menu.menu.AppendAction(name(value), _ => { if (!set.Remove(value)) set.Add(value); UpdateText(); Rebuild(); },
                    _ => set.Contains(value) ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
        }

        /// <summary>
        /// The severity a row shows. A scene shows its scene verdict (the worst break among everything it uses), as the
        /// overview and the scene panel do; its own findings alone (such as an informational one) would contradict them.
        /// </summary>
        static Severity Shown(MigrationReport report, AssetSummary a)
        {
            if (a.Kind != AssetKind.Scene || report == null) return a.Severity;
            var verdict = report.Scenes.FirstOrDefault(s => s.Path == a.Path);
            return verdict != null && verdict.Severity > a.Severity ? verdict.Severity : a.Severity;
        }

        bool Passes(MigrationReport report, AssetSummary a)
        {
            if (severityFilter.Count > 0 && !severityFilter.Contains(Shown(report, a))) return false;
            if (ownerFilter.Count > 0 && !ownerFilter.Contains(OwnerOf(report, a))) return false;
            if (kindFilter.Count > 0 && !kindFilter.Contains(a.Kind)) return false;
            if (quick != null && !quick.Match(report, a)) return false;
            if (search.Length > 0 && a.Path.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                && !report.Findings.Any(f => f.AssetPath == a.Path && (f.LocalTitle.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || f.RuleId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)))
                return false;
            return true;
        }

        public void Rebuild()
        {
            var report = ctx.Report;
            var items = new List<TreeViewItemData<Node>>();
            int id = 1;
            if (report != null)
            {
                foreach (var g in Rows(report).Where(a => Passes(report, a)).GroupBy(a => a.Kind == AssetKind.Project ? AssetKind.Settings : a.Kind).OrderBy(g => g.Key))
                {
                    var children = g.OrderByDescending(a => Shown(report, a)).ThenBy(a => Path.GetFileName(a.Path), StringComparer.OrdinalIgnoreCase)
                        .Select(a => new TreeViewItemData<Node>(id++, new Node { Kind = g.Key, Asset = a, Severity = Shown(report, a) })).ToList();
                    // After the migration the group's icon is that of what is left in it, a tick when nothing is (1.0.1).
                    var left = g.Where(a => !Fixed(a)).ToList();
                    bool allFixed = left.Count == 0 && g.Any(Fixed);
                    var severity = allFixed ? Severity.Info : (left.Count > 0 ? left : g.ToList()).Max(a => Shown(report, a));
                    items.Add(new TreeViewItemData<Node>(id++, new Node { Kind = g.Key, Count = children.Count, Severity = severity, AllFixed = allFixed }, children));
                }
            }
            tree.SetRootItems(items);
            tree.Rebuild();
            foreach (var it in items) tree.ExpandItem(it.id);
            bool empty = items.Count == 0;
            emptyList.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
            emptyList.text = report == null ? Loc.Get("ui.list.noScan") : quick != null ? Loc.Get("ui.list.noMatchQuick", quick.Label) : Loc.Get("ui.list.noMatch");
        }

        bool Fixed(AssetSummary a) => ctx.Summary?.Migrated == true && ctx.Summary.Get(a.Path)?.Outcome == Pipeline.AssetOutcome.Fixed;

        /// <summary>
        /// Who fixes the asset: after the migration, who fixed it or who is left to (the migration's result); before it, what
        /// the scan expects.
        /// </summary>
        FixOwner OwnerOf(MigrationReport report, AssetSummary a)
        {
            var e = ctx.Summary?.Migrated == true ? ctx.Summary.Get(a.Path) : null;
            return e != null ? e.By : a.Severity > Severity.Info || a.Tier != FixTier.None ? Edition.Owner(report, a) : FixOwner.None;
        }

        /// <summary>
        /// The report's assets, plus every scene the report has a verdict for and no finding of its own, so the Scenes group
        /// lists every scanned and captured scene and a count of the strip never filters to an empty list.
        /// </summary>
        public static IEnumerable<AssetSummary> Rows(MigrationReport report)
        {
            var listed = new HashSet<string>(report.Assets.Select(a => a.Path), StringComparer.Ordinal);
            return report.Assets.Concat(report.Scenes.Where(x => !listed.Contains(x.Path))
                .Select(x => new AssetSummary { Path = x.Path, Kind = AssetKind.Scene, Severity = Severity.Info, Tier = FixTier.None, Severities = new[] { Severity.Info } }));
        }

        /// <summary>Highlights the row of <paramref name="path"/> without raising <see cref="Selected"/>.</summary>
        public void Highlight(string path)
        {
            for (int i = 0; i < 100000; i++)
            {
                Node n;
                try { n = tree.GetItemDataForIndex<Node>(i); }
                catch { break; }
                if (n?.Asset?.Path == path) { tree.SetSelectionWithoutNotify(new[] { i }); tree.ScrollToItem(i); return; }
            }
        }

        VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("urpify-row");
            row.Add(new SeverityIcon(Severity.Info) { name = "icon" });
            // After the migration a fixed asset shows a tick instead of its severity (the severity was before the migration).
            row.Add(new StatusIcon(Pipeline.StepStatus.Done) { name = "done" });
            var name = new Label { name = "name" };
            name.AddToClassList("urpify-row-name");
            row.Add(name);
            var tier = new Label { name = "tier" };
            tier.AddToClassList("urpify-row-tier");
            row.Add(tier);
            var pro = new VisualElement { name = "pro" };
            pro.AddToClassList("urpify-row-pro");
            pro.Add(new Image { image = UiKit.LockIcon, scaleMode = UnityEngine.ScaleMode.ScaleToFit });
            pro.Add(new Label(Loc.Get("ui.list.pro")));
            row.Add(pro);
            return row;
        }

        void BindRow(VisualElement e, int index)
        {
            var n = tree.GetItemDataForIndex<Node>(index);
            var icon = e.Q<SeverityIcon>("icon");
            var name = e.Q<Label>("name");
            var tier = e.Q<Label>("tier");
            var pro = e.Q("pro");
            pro.style.display = DisplayStyle.None;
            icon.Set(n.Severity);
            e.EnableInClassList("urpify-row-fixed", false);
            if (n.Asset == null)
            {
                name.text = Loc.Get("kind." + n.Kind) + " (" + n.Count + ")";
                name.AddToClassList("urpify-row-group");
                tier.text = "";
                e.tooltip = "";
                e.EnableInClassList("urpify-row-fixed", n.AllFixed);
            }
            else
            {
                name.RemoveFromClassList("urpify-row-group");
                name.text = Path.GetFileName(n.Asset.Path);
                name.EnableInClassList("urpify-row-done", false);
                var entry = ctx.Summary?.Migrated == true ? ctx.Summary.Get(n.Asset.Path) : null;
                if (entry != null && entry.Outcome != Pipeline.AssetOutcome.None)
                {
                    // After the migration the column says what became of it: Fixed, Check, To do.
                    tier.text = Loc.Get("outcome.short." + entry.Outcome);
                    tier.tooltip = ResultPanel.OutcomeText(entry, ctx.Summary);
                    name.EnableInClassList("urpify-row-done", entry.Outcome == Pipeline.AssetOutcome.Fixed);
                    e.EnableInClassList("urpify-row-fixed", entry.Outcome == Pipeline.AssetOutcome.Fixed);
                }
                else
                {
                    // Before it: who fixes it and whether it is free (owner's check 19.10.2026, item 7).
                    var owner = ctx.Report != null ? OwnerOf(ctx.Report, n.Asset) : FixOwner.None;
                    var accuracy = UiKit.Accuracy(n.Asset.Tier);
                    tier.text = UiKit.OwnerShort(owner);
                    tier.tooltip = owner == FixOwner.None ? "" : UiKit.OwnerLong(owner) + (accuracy.Length > 0 ? "\n" + accuracy : "");
                    // Fixed only by Pro and Pro is not installed: a lock instead of the label; the row still opens its findings.
                    if (ctx.Report != null && Edition.Locked(ctx.Report, n.Asset))
                    {
                        tier.text = "";
                        pro.style.display = DisplayStyle.Flex;
                        pro.tooltip = Loc.Get("owner.Pro");
                    }
                }
                e.tooltip = n.Asset.Path + "\n" + Loc.Get("severity." + n.Severity);
            }
        }

        void OnSelection(IEnumerable<object> selection) => Selected?.Invoke(selection.OfType<Node>().FirstOrDefault()?.Asset?.Path);

        void OnChosen(IEnumerable<object> chosen)
        {
            var n = chosen.OfType<Node>().FirstOrDefault();
            if (n?.Asset == null || ctx.Report == null) return;
            var f = ctx.Report.Findings.FirstOrDefault(x => x.AssetPath == n.Asset.Path && x.Line > 0);
            UiKit.OpenAsset(n.Asset.Path, f?.Line ?? 0);
        }
    }
}
