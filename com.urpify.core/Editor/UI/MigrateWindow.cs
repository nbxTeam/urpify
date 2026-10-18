using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Urpify.Pipeline;
using Urpify.Verify;

namespace Urpify.UI
{
    /// <summary>Migrate dialog (TZ 4.2, 8.1): the steps with their state, Run all or one by one, roll back to any step.</summary>
    public sealed class MigrateWindow : EditorWindow
    {
        ScrollView list;
        VisualElement banner, breaks;
        Button runAll, rollbackAll;
        Label running;
        double nextPoll;

        public static void Open()
        {
            var w = GetWindow<MigrateWindow>(true, Loc.Get("ui.migrate.title"), true);
            w.minSize = new Vector2(560, 420);
            w.Show();
        }

        void OnEnable()
        {
            MigrationRunner.StepFinished += OnStepFinished;
            MigrationRunner.ChainFinished += OnChainFinished;
            EditorApplication.update += Poll;
            Loc.LanguageChanged += OnLanguageChanged;
        }

        void OnDisable()
        {
            MigrationRunner.StepFinished -= OnStepFinished;
            MigrationRunner.ChainFinished -= OnChainFinished;
            EditorApplication.update -= Poll;
            Loc.LanguageChanged -= OnLanguageChanged;
        }

        void OnLanguageChanged()
        {
            titleContent = new GUIContent(Loc.Get("ui.migrate.title"));
            CreateGUI();
        }

        void OnStepFinished(StepRecord rec) => Refresh();

        /// <summary>Show what breaks or Fix with Urpify ended: its result is on the main window, opened when it is closed.</summary>
        static void OnChainFinished(string kind, bool ok)
        {
            if (Application.isBatchMode) return;
            var w = GetWindow<UrpifyWindow>();
            w.Show();
            w.ShowResult();
        }

        void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            if (running == null) return;
            bool capturing = CaptureRunner.IsRunning;
            running.style.display = capturing || MigrationRunner.Busy ? DisplayStyle.Flex : DisplayStyle.None;
            var chain = MigrationState.Load().ChainKind;
            running.text = capturing ? Loc.Get("ui.migrate.capturing", CaptureRunner.CurrentLabel, Mathf.RoundToInt(CaptureRunner.Progress * 100), CaptureRunner.Status)
                         : chain == MigrationRunner.ChainShowBreaks ? Loc.Get("ui.breaks.running")
                         : chain == MigrationRunner.ChainFix ? Loc.Get("ui.result.fixing")
                         : Loc.Get("ui.migrate.running");
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(PackagePath() + "/Editor/UI/Window.uss");
            if (uss != null) root.styleSheets.Add(uss);
            root.AddToClassList("urpify-root");
            root.AddToClassList(EditorGUIUtility.isProSkin ? "urpify-dark" : "urpify-light");
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingTop = 10;

            var title = new Label(Loc.Get("ui.migrate.heading"));
            title.AddToClassList("urpify-title");
            // urpify-title grows and shrinks along a header row; in this column it must keep its height (QA-021: the title was
            // cut off at the top and ran into the first line).
            title.style.flexGrow = 0;
            title.style.flexShrink = 0;
            title.style.marginBottom = 4;
            root.Add(title);
            var intro = new Label(Loc.Get("ui.migrate.intro")) { tooltip = Loc.Get("ui.migrate.introTip") };
            intro.AddToClassList("urpify-note");
            intro.style.flexShrink = 0;
            root.Add(intro);
            // The column's scroll list takes the rest of the height; the lines above it keep theirs. A two-line git warning
            // (de, ru) was squeezed to one line and ran under the first step (design review 13.10.2026, item 4).
            banner = new VisualElement();
            banner.style.flexShrink = 0;
            root.Add(banner);
            running = new Label();
            running.AddToClassList("urpify-note");
            running.style.flexShrink = 0;
            root.Add(running);
            breaks = new VisualElement();
            breaks.style.flexShrink = 0;
            root.Add(breaks);

            list = new ScrollView();
            list.style.flexGrow = 1;
            root.Add(list);

            var bar = new VisualElement();
            bar.AddToClassList("urpify-buttons");
            bar.style.marginTop = 8;
            runAll = new Button(() => Confirmed(StepId.InstallUrp, () => { MigrationRunner.CaptureFromSettings(); Report(MigrationRunner.RunAll()); })) { text = Loc.Get("ui.migrate.runAll") };
            runAll.AddToClassList("urpify-primary");
            rollbackAll = new Button(() => ConfirmRollback(null)) { text = Loc.Get("ui.migrate.rollbackAll") };
            bar.Add(runAll);
            bar.Add(rollbackAll);
            root.Add(bar);
            Refresh();
        }

        static string PackagePath()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(MigrateWindow).Assembly);
            return info != null ? info.assetPath : KitPaths.Root(KitPaths.Core);
        }

        void Refresh()
        {
            if (list == null) return;
            var st = MigrationState.Load();
            banner.Clear();
            var (repo, changes, msg) = GitCheck.Status();
            if (repo && changes != 0)
                banner.Add(Warning(changes < 0 ? Loc.Get("ui.migrate.gitUnknown") : Loc.Get("ui.migrate.gitDirty", changes), msg));
            var interrupted = st.Steps.FirstOrDefault(s => s.Status == StepStatus.Interrupted);
            if (interrupted != null)
                banner.Add(Warning(Loc.Get("ui.migrate.interrupted", Loc.Get("step." + interrupted.Id + ".title")), ""));

            list.Clear();
            bool busy = st.Running >= 0 || CaptureRunner.IsRunning;
            breaks.Clear();
            if (MigrationRunner.ShowBreaksPlan(st, true).Count > 0) breaks.Add(ShowBreaksCard(busy));
            foreach (var rec in st.Steps.OrderBy(s => s.Id))
                list.Add(Row(st, rec, busy));
            runAll.SetEnabled(!busy);
            rollbackAll.SetEnabled(!busy && st.Steps.Any(s => MigrationState.HasRollback(s.Id) && MigrationState.Snapshots(s).Any()));
        }

        VisualElement Row(MigrationState st, StepRecord rec, bool busy)
        {
            var card = new VisualElement();
            card.AddToClassList("urpify-card");
            var head = new VisualElement();
            head.AddToClassList("urpify-card-head");
            head.Add(new StatusIcon(rec.Status));
            var t = new Label($"{(int)rec.Id + 1}. {Loc.Get("step." + rec.Id + ".title")}");
            t.AddToClassList("urpify-card-title");
            head.Add(t);
            var status = new Label(Loc.Get("status." + rec.Status));
            status.AddToClassList("urpify-card-tier");
            head.Add(status);
            card.Add(head);
            // The Free package runs this step too: its fixes are free, shaders and scripts need Pro (owner's check 19.10.2026, item 6).
            var what = new Label(Loc.Get(rec.Id == StepId.KitConverters && MigrationRunner.ShaderConverters().Count == 0 ? "step.KitConverters.whatFree" : "step." + rec.Id + ".what"));
            what.AddToClassList("urpify-text");
            card.Add(what);
            // One result line in plain words, in the window's language; what the step logged goes under Details (design
            // review 13.10.2026, item 8). A step that failed says why on its card: that message is what to act on.
            bool failed = rec.Status == StepStatus.Failed || rec.Status == StepStatus.Interrupted;
            var result = rec.ResultText;
            if (failed && !string.IsNullOrEmpty(rec.Message))
            {
                var m = new Label(rec.Message);
                m.AddToClassList("urpify-manual");
                card.Add(m);
            }
            else if (result.Length > 0 || !string.IsNullOrEmpty(rec.Message))
            {
                var m = new Label(result.Length > 0 ? result : rec.Message);
                m.AddToClassList("urpify-result");
                card.Add(m);
            }
            var technical = new System.Collections.Generic.List<string>();
            if (!failed && result.Length > 0 && !string.IsNullOrEmpty(rec.Message)) technical.Add(rec.Message);
            technical.AddRange(rec.Details);
            if (technical.Count > 0)
            {
                var fold = new Foldout { text = Loc.Get("ui.migrate.details"), value = false };
                fold.AddToClassList("urpify-step-details");
                foreach (var d in technical.Take(20))
                {
                    var l = new Label(d);
                    l.AddToClassList("urpify-rule");
                    fold.Add(l);
                }
                if (technical.Count > 20) fold.Add(UiKit.Note(Loc.Get("report.andMore", technical.Count - 20).Trim()));
                card.Add(fold);
            }
            var buttons = new VisualElement();
            buttons.AddToClassList("urpify-used");
            var id = rec.Id;
            var run = new Button(() => Confirmed(id, () => { MigrationRunner.CaptureFromSettings(); Report(MigrationRunner.Run(id)); })) { text = rec.Status == StepStatus.NotRun ? Loc.Get("ui.migrate.run") : Loc.Get("ui.migrate.runAgain") };
            // A step whose earlier steps are not done is unavailable and says why (owner's check 19.10.2026, item 2).
            var blocker = MigrationRunner.Blocker(id, st);
            run.SetEnabled(!busy && blocker == null);
            if (blocker != null && !busy)
            {
                run.tooltip = blocker;
                var hint = new Label(blocker);
                hint.AddToClassList("urpify-step-blocked");
                card.Add(hint);
            }
            buttons.Add(run);
            if (id == StepId.KitConverters && MigrationRunner.ShaderConverters().Count > 0)
            {
                var output = UiKit.OutputChooser(null);
                output.SetEnabled(!busy);
                buttons.Add(output);
            }
            bool laterChanges = st.Steps.Any(s => s.Id > id && MigrationState.HasRollback(s.Id) && MigrationState.Snapshots(s).Any());
            if (laterChanges)
            {
                var back = new Button(() => ConfirmRollback(id)) { text = Loc.Get("ui.migrate.rollbackTo") };
                back.SetEnabled(!busy);
                buttons.Add(back);
            }
            card.Add(buttons);
            return card;
        }

        static VisualElement Warning(string text, string tooltip)
        {
            var w = new Label(text) { tooltip = tooltip };
            w.AddToClassList("urpify-manual");
            w.style.marginBottom = 6;
            return w;
        }

        /// <summary>Before a step that changes files: a git working tree with uncommitted changes gets a warning first.</summary>
        void Confirmed(StepId id, Action action)
        {
            if (MigrationState.IsModifying(id) || id == StepId.InstallUrp)
            {
                var (repo, changes, _) = GitCheck.Status();
                if (repo && changes != 0 &&
                    !EditorUtility.DisplayDialog(Loc.Get("ui.migrate.gitDialogTitle"), Loc.Get("ui.migrate.gitDialogText", changes < 0 ? "?" : changes.ToString()),
                        Loc.Get("ui.migrate.gitContinue"), Loc.Get("ui.cancel")))
                    return;
            }
            action();
            Refresh();
        }

        void Report(string error)
        {
            if (error != null) ShowNotification(new GUIContent(error));
            Refresh();
        }

        /// <summary>Show what breaks (owner's check 19.10.2026, item 3), above the steps while the project is not on URP yet.</summary>
        VisualElement ShowBreaksCard(bool busy)
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
            var b = new Button(() => Confirmed(StepId.InstallUrp, () => { MigrationRunner.CaptureFromSettings(); Report(MigrationRunner.ShowWhatBreaks()); }))
            {
                text = Loc.Get("ui.breaks.button"), tooltip = Loc.Get("ui.breaks.tip"),
            };
            b.AddToClassList("urpify-primary");
            b.SetEnabled(!busy);
            var row = new VisualElement();
            row.AddToClassList("urpify-buttons");
            row.Add(b);
            card.Add(row);
            return card;
        }

        /// <summary>Show what breaks from the main window: the same as the button above the steps (git warning first).</summary>
        public static void StartShowWhatBreaks()
        {
            var (repo, changes, _) = GitCheck.Status();
            if (repo && changes != 0 &&
                !EditorUtility.DisplayDialog(Loc.Get("ui.migrate.gitDialogTitle"), Loc.Get("ui.migrate.gitDialogText", changes < 0 ? "?" : changes.ToString()),
                    Loc.Get("ui.migrate.gitContinue"), Loc.Get("ui.cancel")))
                return;
            MigrationRunner.CaptureFromSettings();
            var error = MigrationRunner.ShowWhatBreaks();
            if (error != null) EditorUtility.DisplayDialog(Loc.Get("ui.breaks.title"), error, Loc.Get("ui.ok"));
        }

        /// <summary>Roll back to a step (null: everything), after the confirmation dialog; the Editor restarts after it.</summary>
        public static void ConfirmRollback(StepId? target)
        {
            var what = target == null ? Loc.Get("ui.migrate.rollbackAllWhat") : Loc.Get("ui.migrate.rollbackToWhat", Loc.Get("step." + target + ".title"));
            var confirm = target == null ? Loc.Get("ui.migrate.rollbackAllConfirm") : Loc.Get("ui.migrate.rollbackToConfirm", Loc.Get("step." + target + ".title"));
            int bakes = MigrationState.Load().Extras.Count(e => e.Status == StepStatus.Done);
            if (bakes > 0) what += "\n\n" + Loc.Get("ui.migrate.rollbackBakes", bakes);
            what += "\n\n" + Loc.Get("ui.migrate.restartFirst");
            if (!EditorUtility.DisplayDialog(Loc.Get("ui.migrate.rollbackTitle"), what, confirm, Loc.Get("ui.cancel"))) return;
            // Unsaved scene changes are saved now or the rollback does not start: the restart after it must not offer to save
            // the migrated scene over the restored file. The open scenes are closed, so nothing of the project stays loaded.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RestartAfterRollback(RollbackAndReport(target));
        }

        const string SummaryFile = "Urpify/rollback-summary.txt";

        /// <summary>
        /// The Editor restarts right after a rollback (architect decision 4 of 13.10.2026). The session that ran the migration
        /// still has URP, the converted scripts and Urpify's runtime assembly loaded; on its next update Unity's compilation
        /// pipeline reads the assembly definitions it knew, the rollback has removed some (Urpify.Compat, packages the
        /// restored manifest drops), and the Console gets "Could not read file ...asmdef" (in batch mode it aborts). Refreshing
        /// the asset database instead lets the still loaded URP write its settings assets again, so the project would no longer
        /// be the one before the migration. Auto refresh is stopped until the restart; the summary is shown after it.
        /// </summary>
        public static void RestartAfterRollback(string summary, params string[] args)
        {
            try { Directory.CreateDirectory("Urpify"); File.WriteAllText(SummaryFile, summary); }
            catch (Exception e) { Debug.LogWarning("[Urpify] Could not keep the rollback summary for after the restart: " + e.Message); }
            Debug.Log("[Urpify] " + summary);
            AssetDatabase.DisallowAutoRefresh();
            EditorApplication.OpenProject(Directory.GetCurrentDirectory(), args);
        }

        /// <summary>After the restart that follows a rollback: the rollback's summary, once.</summary>
        [InitializeOnLoadMethod]
        static void ShowSummaryAfterRestart()
        {
            if (Application.isBatchMode || !File.Exists(SummaryFile)) return;
            EditorApplication.delayCall += () =>
            {
                string text;
                try { text = File.ReadAllText(SummaryFile); File.Delete(SummaryFile); }
                catch { return; }
                Debug.Log("[Urpify] " + text);
                EditorUtility.DisplayDialog(Loc.Get("ui.migrate.rollbackDoneTitle"), text, Loc.Get("ui.ok"));
            };
        }

        /// <summary>
        /// What Roll back does in the window, without its dialogs: the rollback, the files it left listed in the Console, and the
        /// summary the dialog shows. The window check (Tools/Editor/UmkScreenshots) runs it to see the Console after a rollback.
        /// </summary>
        public static string RollbackAndReport(StepId? target)
        {
            // An exception must not skip the restart: the Editor would keep URP loaded over the restored settings (R-02).
            RollbackResult r;
            try { r = MigrationRunner.Rollback(target); }
            catch (Exception e) { r = new RollbackResult(); r.Errors.Add(e.Message); Debug.LogException(e); }
            foreach (var path in r.Skipped) Debug.LogWarning("[Urpify] Rollback left as it is (changed after the migration): " + path);
            foreach (var e in r.Errors) Debug.LogError("[Urpify] Rollback: " + e);
            foreach (var path in r.Between) Debug.LogWarning("[Urpify] Rollback left as it is (appeared or changed between steps, may be yours): " + path);
            foreach (var kept in r.Kept) Debug.Log("[Urpify] Rollback replaced a file; the replaced version is kept: " + kept);
            var summary = Loc.Get("ui.migrate.rollbackDone", r.Restored.Count, r.Removed.Count, r.Skipped.Count, r.Errors.Count);
            if (r.Between.Count > 0) summary += " " + Loc.Get("ui.migrate.rollbackBetween", r.Between.Count);
            if (r.Skipped.Count + r.Between.Count + r.Errors.Count > 0) summary += " " + Loc.Get("ui.migrate.rollbackListed");
            return summary;
        }
    }
}
