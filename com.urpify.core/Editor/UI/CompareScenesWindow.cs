using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Urpify.Pipeline;

namespace Urpify.UI
{
    /// <summary>
    /// Compare scenes (TZ 4.2; owner's check 19.10.2026, item 9): a heading that says what the frames are, then every captured
    /// scene with its result in words. A scene opens its before/after comparison with the curtain in place (with one scene it
    /// is open from the start); Open scene is its own button. Changed only hides the scenes that look the same.
    /// </summary>
    public sealed class CompareScenesWindow : EditorWindow
    {
        static bool changedOnly;

        MigrationReport report;
        VisualElement list;
        Label summary;

        public static void Open() => GetWindow<CompareScenesWindow>().Show();

        void OnEnable()
        {
            titleContent = new GUIContent(Loc.Get("ui.compare.title"));
            minSize = new Vector2(420, 300);
            Loc.LanguageChanged += OnLanguageChanged;
            MigrationRunner.StepFinished += OnStepFinished;
        }

        void OnDisable()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            MigrationRunner.StepFinished -= OnStepFinished;
        }

        void OnLanguageChanged()
        {
            titleContent = new GUIContent(Loc.Get("ui.compare.title"));
            CreateGUI();
        }

        void OnStepFinished(StepRecord rec)
        {
            if (rec.Id != StepId.CaptureAfter && rec.Id != StepId.Review) return;
            Load();
            Fill();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(PackagePath() + "/Editor/UI/Window.uss");
            if (uss != null) root.styleSheets.Add(uss);
            root.AddToClassList("urpify-root");
            root.AddToClassList(EditorGUIUtility.isProSkin ? "urpify-dark" : "urpify-light");
            var bar = new Toolbar();
            var only = new ToolbarToggle { text = Loc.Get("ui.compare.changedOnly"), value = changedOnly, tooltip = Loc.Get("ui.compare.changedOnlyTip") };
            only.RegisterValueChangedCallback(e => { changedOnly = e.newValue; Fill(); });
            bar.Add(only);
            bar.Add(new ToolbarSpacer { flex = true });
            bar.Add(new ToolbarButton(() => { Load(); Fill(); }) { text = Loc.Get("ui.compare.reload") });
            root.Add(bar);
            var scroll = new ScrollView();
            scroll.AddToClassList("urpify-right");
            scroll.style.flexGrow = 1;
            var head = new VisualElement();
            head.AddToClassList("urpify-compare-head");
            var title = new Label(Loc.Get("ui.compare.heading"));
            title.AddToClassList("urpify-result-title");
            head.Add(title);
            var intro = new Label(Loc.Get("ui.compare.intro"));
            intro.AddToClassList("urpify-note");
            head.Add(intro);
            summary = new Label();
            summary.AddToClassList("urpify-result-sub");
            head.Add(summary);
            scroll.Add(head);
            list = new VisualElement();
            scroll.Add(list);
            root.Add(scroll);
            Load();
            Fill();
        }

        static string PackagePath()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(CompareScenesWindow).Assembly);
            return info != null ? info.assetPath : KitPaths.Root(KitPaths.Core);
        }

        void Load() => report = MigrationSummary.LatestComparison();

        void Fill()
        {
            list.Clear();
            var scenes = report?.Scenes.Where(s => s.Observed != ObservedVerdict.NotCaptured || !string.IsNullOrEmpty(s.ObservedNote)).ToList();
            if (scenes == null || scenes.Count == 0)
            {
                summary.text = "";
                list.Add(Empty());
                return;
            }
            summary.text = Summary(scenes.Count(s => s.Observed == ObservedVerdict.Pink), scenes.Count(s => s.Observed == ObservedVerdict.Changed),
                scenes.Count(s => s.Observed == ObservedVerdict.Same));
            summary.tooltip = Loc.Get("ui.compare.summaryTip", report.CompareBefore, report.CompareAfter);
            var shown = scenes.OrderByDescending(s => s.Observed == ObservedVerdict.Pink).ThenByDescending(s => s.Observed == ObservedVerdict.Changed)
                .ThenByDescending(s => s.Viewpoints.Count == 0 ? 0 : s.Viewpoints.Max(v => v.ChangedPixelsPct))
                .Where(s => !changedOnly || s.Observed != ObservedVerdict.Same).ToList();
            if (shown.Count == 0) { list.Add(UiKit.Note(Loc.Get("ui.compare.allSame"))); return; }
            foreach (var s in shown) list.Add(SceneCard(s, open: scenes.Count == 1));
        }

        /// <summary>One line for all scenes, the bad news first; only the verdicts that occur.</summary>
        public static string Summary(int pink, int changed, int same)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (pink > 0) parts.Add(Loc.Get(pink == 1 ? "ui.compare.countPinkOne" : "ui.compare.countPink", pink));
            if (changed > 0) parts.Add(Loc.Get(changed == 1 ? "ui.compare.countChangedOne" : "ui.compare.countChanged", changed));
            if (same > 0) parts.Add(Loc.Get(same == 1 ? "ui.compare.countSameOne" : "ui.compare.countSame", same));
            return string.Join(Loc.Get("ui.compare.countSeparator"), parts);
        }

        VisualElement Empty()
        {
            var box = new VisualElement();
            box.AddToClassList("urpify-empty");
            var t = new Label(Loc.Get("ui.compare.emptyTitle"));
            t.AddToClassList("urpify-empty-title");
            box.Add(t);
            var d = new Label(Loc.Get("ui.compare.emptyText"));
            d.AddToClassList("urpify-empty-text");
            box.Add(d);
            if (MigrationRunner.ShowBreaksPlan(MigrationState.Load(), true).Count > 0)
            {
                var b = new Button(MigrateWindow.StartShowWhatBreaks) { text = Loc.Get("ui.breaks.button"), tooltip = Loc.Get("ui.breaks.tip") };
                b.AddToClassList("urpify-primary");
                box.Add(b);
            }
            else box.Add(new Button(MigrateWindow.Open) { text = Loc.Get("ui.result.steps") });
            return box;
        }

        /// <summary>A scene: its name and result; a click on it shows or hides the comparison; Open scene opens it.</summary>
        public static VisualElement SceneCard(SceneVerdict s, bool open)
        {
            var card = new VisualElement();
            card.AddToClassList("urpify-card");
            card.AddToClassList("urpify-scene-card");
            var head = new VisualElement();
            head.AddToClassList("urpify-scene-card-head");
            var worst = s.Viewpoints.OrderByDescending(v => v.ChangedPixelsPct).FirstOrDefault();
            var body = new VisualElement();
            body.AddToClassList("urpify-scene-card-body");

            var toggle = new Button { tooltip = Loc.Get("ui.compare.toggleTip") };
            toggle.AddToClassList("urpify-scene-toggle");
            var thumb = new Image { scaleMode = ScaleMode.ScaleAndCrop };
            thumb.AddToClassList("urpify-scene-thumb");
            var tex = worst != null ? Verify.CaptureRunner.LoadFrame(worst.After) : null;
            if (tex != null) thumb.image = tex;
            thumb.RegisterCallback<DetachFromPanelEvent>(_ => { if (tex != null) Object.DestroyImmediate(tex); });
            toggle.Add(thumb);
            var words = new VisualElement();
            words.AddToClassList("urpify-scene-words");
            var line = new VisualElement();
            line.AddToClassList("urpify-card-head");
            line.Add(new SeverityIcon(UiKit.ObservedIcon(s.Observed)));
            var name = new Label(Path.GetFileNameWithoutExtension(s.Path)) { tooltip = s.Path };
            name.AddToClassList("urpify-card-title");
            line.Add(name);
            words.Add(line);
            var verdict = new Label(ResultPanel.SceneText(s.Observed, worst?.ChangedPixelsPct ?? 0));
            verdict.AddToClassList("urpify-text");
            words.Add(verdict);
            if (!string.IsNullOrEmpty(s.ObservedNote))
            {
                var note = new Label(s.ObservedNote);
                note.AddToClassList("urpify-note");
                words.Add(note);
            }
            toggle.Add(words);
            head.Add(toggle);

            var path = s.Path;
            var openScene = new Button(() => OpenScene(path)) { text = Loc.Get("ui.compare.openScene"), tooltip = path };
            openScene.AddToClassList("urpify-scene-open");
            head.Add(openScene);
            card.Add(head);

            void Show(bool on)
            {
                body.Clear();
                card.EnableInClassList("urpify-scene-card-open", on);
                thumb.style.display = on ? DisplayStyle.None : DisplayStyle.Flex;
                if (!on) return;
                foreach (var v in s.Viewpoints)
                {
                    if (s.Viewpoints.Count > 1) body.Add(UiKit.H2(Loc.Get("ui.scene.view." + v.Verdict, v.Name)));
                    body.Add(new CompareView(v.Before, v.After, v.Diff));
                }
            }
            bool isOpen = open;
            toggle.clicked += () => { isOpen = !isOpen; Show(isOpen); };
            Show(isOpen);
            card.Add(body);
            return card;
        }

        static void OpenScene(string path)
        {
            if (!File.Exists(path)) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }
}
