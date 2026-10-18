using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Urpify.Pipeline;
using Urpify.Reporting;
using Urpify.Scanner;

namespace Urpify.UI
{
    /// <summary>
    /// Main window: Window > Rendering > Urpify. The window owns the toolbar, the scan and the report; the
    /// panels draw: <see cref="AssetListPanel"/> (left), <see cref="SummaryStrip"/>, and on the right
    /// <see cref="OverviewPanel"/>, <see cref="AssetPanel"/>, <see cref="ScenePanel"/> or <see cref="ShaderPanel"/> (any
    /// .shader, also one dragged in from the Project window or from disk). Migrate is its own window
    /// (<see cref="MigrateWindow"/>).
    /// </summary>
    public sealed class UrpifyWindow : EditorWindow, IWindowContext
    {
        public const string LastReportPath = "Library/Urpify/last-report.json";
        const double FrameBudgetMs = 25;

        // State
        MigrationReport report;
        MigrationSummary summary;
        /// <summary>The window's own scan is older than the last change a step made: scan again when the Editor is free.</summary>
        bool rescanDue;
        string[] scopePaths = { "Assets" };
        string scopeLabel = "Project";
        ProjectScanner scanner;
        IEnumerator<float> scanJob;
        string selectedPath;

        // UI
        AssetListPanel list;
        ScrollView right;
        VisualElement summaryStrip, progress;
        ProgressBar progressBar;
        ToolbarButton scanButton;
        Label scopeText;

        [MenuItem("Window/Rendering/Urpify", false, 400)]
        public static void Open() => GetWindow<UrpifyWindow>().Show();

        [MenuItem("Assets/Urpify/Analyze", false, 2000)]
        static void AnalyzeSelection()
        {
            var paths = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToArray();
            if (paths.Length == 0) return;
            var w = GetWindow<UrpifyWindow>();
            w.Show();
            w.SetScope(paths, paths.Length == 1 && !AssetDatabase.IsValidFolder(paths[0]) ? "File" : "Folder");
            w.StartScan();
        }

        [MenuItem("Assets/Urpify/Analyze", true)]
        static bool AnalyzeSelectionValidate() =>
            Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Any(p => p.StartsWith("Assets", StringComparison.Ordinal));

        // ------------------------------------------------------------------ IWindowContext and automation

        public MigrationReport Report => report;
        public MigrationReport CurrentReport => report;
        public bool IsScanning => scanJob != null;
        public MigrationSummary Summary => summary;
        public string ActiveFilter => list?.Quick?.Id;

        public void ToggleFilter(QuickFilter filter)
        {
            list?.ToggleQuick(filter);
            if (summaryStrip != null) SummaryStrip.Build(summaryStrip, this);
        }

        /// <summary>Shows the overview (before the migration) or the result (after it), as after Show what breaks.</summary>
        public void ShowResult()
        {
            selectedPath = null;
            LoadLastReport();
            RefreshAll();
            Focus();
        }

        public void Select(string path)
        {
            selectedPath = path;
            list?.Highlight(path);
            ShowDetails();
        }

        public void SelectAsset(string path) => Select(path);

        /// <summary>Automation: opens a .unitypackage as the scope and scans it (what Scope > Unity package does after its file dialog).</summary>
        public void ScanPackage(string file)
        {
            PackageScope.Open(file);
            SetScope(PackageScope.ScanRoots(), "Package");
            StartScan();
        }

        public void ScanNow(string[] roots, string label)
        {
            SetScope(roots, label);
            StartScan();
        }

        void OnEnable()
        {
            titleContent = new GUIContent(Loc.Get("ui.windowTitle"));
            minSize = new Vector2(560, 320);
            Loc.LanguageChanged -= OnLanguageChanged;
            Loc.LanguageChanged += OnLanguageChanged;
            // After a script reload the package being analyzed is still in its temporary folder.
            if (PackageScope.IsOpen) { scopePaths = PackageScope.ScanRoots(); scopeLabel = "Package"; }
            LoadLastReport();
        }

        void OnDisable()
        {
            StopScan();
            // A static event would keep a closed window alive and call into it (QA-021: 11 live window objects, one visible).
            MigrationRunner.StepFinished -= OnStepFinished;
            MigrationRunner.ChainFinished -= OnChainFinished;
            EditorApplication.update -= RescanWhenFree;
            Loc.LanguageChanged -= OnLanguageChanged;
        }

        /// <summary>The language setting changed: the window draws itself again in the new language.</summary>
        void OnLanguageChanged()
        {
            titleContent = new GUIContent(Loc.Get("ui.windowTitle"));
            CreateGUI();
        }

        /// <summary>Closing the window removes a package scope's temporary folder.</summary>
        void OnDestroy()
        {
            if (PackageScope.IsOpen) PackageScope.Close();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            var basePath = PackageAssetPath();
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(basePath + "/Editor/UI/Window.uxml");
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(basePath + "/Editor/UI/Window.uss");
            if (uxml == null) { root.Add(new Label("Window.uxml not found under " + basePath)); return; }
            uxml.CloneTree(root);
            if (uss != null) root.styleSheets.Add(uss);
            var top = root.Q("root");
            top.style.flexGrow = 1;
            top.AddToClassList(EditorGUIUtility.isProSkin ? "urpify-dark" : "urpify-light");

            scopeText = root.Q<Label>("scope-label");
            var scopeMenu = root.Q<ToolbarMenu>("scope-menu");
            scopeMenu.text = Loc.Get("ui.scope.change");
            scopeMenu.tooltip = Loc.Get("ui.scope.tooltip");
            scopeMenu.menu.AppendAction(Loc.Get("ui.scope.project"), _ => SetScope(new[] { "Assets" }, "Project"),
                _ => scopeLabel == "Project" ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            scopeMenu.menu.AppendAction(Loc.Get("ui.scope.selection"), _ => ScopeFromSelection(),
                _ => Selection.assetGUIDs.Length > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            scopeMenu.menu.AppendAction(Loc.Get("ui.scope.package"), _ => ScopeFromPackage(),
                _ => scopeLabel == "Package" ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            scanButton = root.Q<ToolbarButton>("scan-button");
            scanButton.text = Loc.Get("ui.scan");
            scanButton.tooltip = Loc.Get("ui.scan.tooltip");
            scanButton.clicked += StartScan;
            var migrate = root.Q<ToolbarButton>("migrate-button");
            migrate.text = Loc.Get("ui.migrate.button");
            migrate.tooltip = Loc.Get("ui.migrate.tooltip");
            migrate.clicked += MigrateWindow.Open;
            var compare = root.Q<ToolbarButton>("compare-button");
            compare.text = Loc.Get("ui.compare.button");
            compare.tooltip = Loc.Get("ui.compare.tooltip");
            compare.clicked += CompareScenesWindow.Open;
            var settings = root.Q<ToolbarButton>("settings-button");
            settings.text = Loc.Get("ui.settings.button");
            settings.tooltip = Loc.Get("ui.settings.tooltip");
            settings.clicked += KitSettingsProvider.Open;
            MigrationRunner.StepFinished -= OnStepFinished;
            MigrationRunner.StepFinished += OnStepFinished;
            MigrationRunner.ChainFinished -= OnChainFinished;
            MigrationRunner.ChainFinished += OnChainFinished;
            EditorApplication.update -= RescanWhenFree;
            EditorApplication.update += RescanWhenFree;

            progress = root.Q("progress");
            progressBar = root.Q<ProgressBar>("progress-bar");
            var cancel = root.Q<ToolbarButton>("cancel-button");
            cancel.text = Loc.Get("ui.cancel");
            cancel.clicked += () => { if (scanner != null) scanner.CancelRequested = true; };

            var reportMenu = root.Q<ToolbarMenu>("report-menu");
            reportMenu.text = Loc.Get("ui.report");
            reportMenu.menu.AppendAction(Loc.Get("ui.report.html"), _ => Export("html"), _ => report != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            reportMenu.menu.AppendAction(Loc.Get("ui.report.json"), _ => Export("json"), _ => report != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            reportMenu.menu.AppendSeparator();
            reportMenu.menu.AppendAction(Loc.Get("ui.report.sendReport"), _ => SendReportWindow.Open(selectedPath, report));
            reportMenu.menu.AppendSeparator();
            reportMenu.menu.AppendAction(Loc.Get("ui.report.exportPackage"), _ => ExportPackage(),
                _ => scopeLabel == "Package" && PackageScope.IsOpen && MigrationRunner.ShaderConverters().Count > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            summaryStrip = root.Q("summary");
            right = root.Q<ScrollView>("right");
            list = new AssetListPanel(root, this);
            list.Selected += path => { selectedPath = path; ShowDetails(); };

            // A shader dragged from the Project window or straight from disk opens in the shader panel.
            root.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                if (DroppedShader() != null) DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            });
            root.RegisterCallback<DragPerformEvent>(_ =>
            {
                var path = DroppedShader();
                if (path == null) return;
                DragAndDrop.AcceptDrag();
                Select(path);
            });

            UpdateScopeText();
            RefreshAll();
        }

        static string DroppedShader() =>
            DragAndDrop.paths.Select(ShaderPanelModel.Normalize).FirstOrDefault(p => ShaderPanelModel.IsShaderFile(p) && File.Exists(p));

        void OnStepFinished(StepRecord _)
        {
            LoadLastReport();
            RefreshAll();
        }

        /// <summary>Show what breaks or Fix with Urpify ended: the result, whatever was selected.</summary>
        void OnChainFinished(string kind, bool ok) => ShowResult();

        /// <summary>
        /// After a step changed the project, its findings are found again (owner's check 19.10.2026, item 10: Post-processing v2
        /// stayed "Unity's converter fixes it" after Unity's converter): a project scan as soon as no step, capture or compile
        /// is running.
        /// </summary>
        void RescanWhenFree()
        {
            if (!rescanDue || scanJob != null || scopeLabel != "Project") return;
            if (EditorApplication.timeSinceStartup < nextRescanCheck) return;
            nextRescanCheck = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            // Not between two steps of a chain either: the next step starts on the following update.
            var st = MigrationState.Load();
            if (st.Running >= 0 || st.RunAll || st.Chain.Count > 0 || Verify.CaptureRunner.IsRunning) return;
            rescanDue = false;
            StartScan();
        }

        double nextRescanCheck;

        static string PackageAssetPath()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UrpifyWindow).Assembly);
            return info != null ? info.assetPath : KitPaths.Root(KitPaths.Core);
        }

        // ------------------------------------------------------------------ scope and scan

        void ScopeFromSelection()
        {
            var paths = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Where(p => p.StartsWith("Assets", StringComparison.Ordinal)).Distinct().ToArray();
            if (paths.Length == 0) return;
            SetScope(paths, paths.Length == 1 && !AssetDatabase.IsValidFolder(paths[0]) ? "File" : "Folder");
        }

        /// <summary>Scope = a .unitypackage: imported into a temporary folder (<see cref="PackageScope"/>) and scanned there.</summary>
        void ScopeFromPackage()
        {
            var file = EditorUtility.OpenFilePanel(Loc.Get("ui.scope.packageTitle"), "", "unitypackage");
            if (string.IsNullOrEmpty(file)) return;
            try
            {
                PackageScope.Open(file, (what, f) => EditorUtility.DisplayProgressBar(Loc.Get("ui.scope.package"), what, f));
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(Loc.Get("ui.scope.package"), Loc.Get("ui.scope.packageFailed", e.Message), Loc.Get("ui.ok"));
                return;
            }
            finally { EditorUtility.ClearProgressBar(); }
            SetScope(PackageScope.ScanRoots(), "Package");
            StartScan();
        }

        void ExportPackage() => ExportPackage(KitProjectSettings.instance.Output);

        /// <summary>Converts the open package and writes the converted .unitypackage with the given Output.</summary>
        internal void ExportPackage(ShaderOutput output)
        {
            if (!PackageScope.IsOpen) { ShowNotification(new GUIContent(Loc.Get("ui.report.exportPackageNeedsScope"))); return; }
            if (MigrationRunner.ShaderConverters().Count == 0) { ShowNotification(new GUIContent(Loc.Get("ui.report.exportPackageNeedsPro"))); return; }
            var state = PackageScope.Load();
            var target = EditorUtility.SaveFilePanel(Loc.Get("ui.report.exportPackageTitle"), Path.GetDirectoryName(state.PackageFile),
                Path.GetFileNameWithoutExtension(state.PackageFile) + (output == ShaderOutput.KeepBuiltInSupport ? "_BuiltIn_URP" : "_URP"), "unitypackage");
            if (string.IsNullOrEmpty(target)) return;
            try
            {
                var export = PackageScope.Export(target, output, (what, f) => EditorUtility.DisplayProgressBar(Loc.Get("ui.report.exportPackageTitle"), what, f));
                var packageReport = PackageScope.ForPackage(report ?? new MigrationReport(), export);
                var html = Path.ChangeExtension(target, null) + "-report.html";
                File.WriteAllText(html, ReportHtml.Write(packageReport));
                File.WriteAllText(Path.ChangeExtension(html, ".json"), ReportJson.Write(packageReport));
                var items = export.Conversion.Items;
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(Loc.Get("ui.report.exportPackageTitle"),
                    Loc.Get("ui.report.exportPackageDone", Path.GetFileName(target), items.Count(i => i.Outcome == "Auto" || i.Outcome == "AutoApprox"), items.Count), Loc.Get("ui.ok"));
                EditorUtility.RevealInFinder(target);
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(Loc.Get("ui.report.exportPackageTitle"), Loc.Get("ui.report.exportPackageFailed", e.Message), Loc.Get("ui.ok"));
            }
        }

        void SetScope(string[] paths, string label)
        {
            // Leaving a package scope removes its temporary folder.
            if (scopeLabel == "Package" && label != "Package" && PackageScope.IsOpen) PackageScope.Close();
            scopePaths = paths;
            scopeLabel = label;
            UpdateScopeText();
        }

        void UpdateScopeText()
        {
            if (scopeText == null) return;
            var state = scopeLabel == "Package" ? PackageScope.Load() : null;
            scopeText.text = Loc.Get("ui.scope.label", scopeLabel == "Project" ? Loc.Get("ui.scope.project")
                : state != null ? Loc.Get("ui.scope.packageLabel", Path.GetFileName(state.PackageFile)) : string.Join(", ", scopePaths));
        }

        public void StartScan()
        {
            if (scanJob != null) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { ShowNotification(new GUIContent(Loc.Get("ui.scan.playMode"))); return; }
            scanner = new ProjectScanner();
            scanJob = scanner.Run(new ScanOptions { Roots = scopePaths, ScopeLabel = scopeLabel });
            EditorApplication.update += Pump;
            if (progress != null) progress.style.display = DisplayStyle.Flex;
            if (scanButton != null) scanButton.SetEnabled(false);
        }

        void Pump()
        {
            if (scanJob == null) { EditorApplication.update -= Pump; return; }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool more = true;
            try
            {
                while (sw.Elapsed.TotalMilliseconds < FrameBudgetMs && (more = scanJob.MoveNext())) { }
            }
            catch (Exception e)
            {
                Debug.LogError("[Urpify] Scan failed: " + e);
                more = false;
            }
            if (progressBar != null)
            {
                progressBar.value = scanner.Progress * 100f;
                progressBar.title = scanner.Status;
            }
            if (!more) FinishScan();
        }

        void FinishScan()
        {
            EditorApplication.update -= Pump;
            var r = scanner?.Report;
            scanJob = null;
            if (progress != null) progress.style.display = DisplayStyle.None;
            if (scanButton != null) scanButton.SetEnabled(true);
            if (r != null)
            {
                report = r;
                SaveLastReport();
                // A scan of the project after the migration is the "now" of its result.
                if (scopeLabel == "Project" && !r.Cancelled) LoadLastReport();
                if (r.Cancelled) ShowNotification(new GUIContent(Loc.Get("ui.scan.cancelled")));
            }
            scanner = null;
            RefreshAll();
        }

        void StopScan()
        {
            if (scanJob == null) return;
            EditorApplication.update -= Pump;
            scanJob = null;
            scanner = null;
        }

        // ------------------------------------------------------------------ report

        void Export(string format)
        {
            if (report == null) return;
            var ext = format == "html" ? "html" : "json";
            var path = EditorUtility.SaveFilePanel(Loc.Get("ui.report.saveTitle"), "", "urp-migration-report." + ext, ext);
            if (string.IsNullOrEmpty(path)) return;
            // A package's report names the package's own paths, not the temporary folder.
            var r = scopeLabel == "Package" && PackageScope.IsOpen ? PackageScope.ForPackage(report) : report;
            File.WriteAllText(path, format == "html" ? ReportHtml.Write(r) : ReportJson.Write(r));
            EditorUtility.RevealInFinder(path);
        }

        void SaveLastReport()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LastReportPath));
                File.WriteAllText(LastReportPath, JsonUtility.ToJson(report));
            }
            catch (Exception e) { Debug.LogWarning("[Urpify] Cannot save the last report: " + e.Message); }
        }

        /// <summary>
        /// The report the window shows. Before the migration: the newest of the window's own last scan and the reports the steps
        /// or the command line wrote. After it (<see cref="MigrationSummary"/>): the window's scan of the project as it is now,
        /// with what the scan before the migration found in assets that are fixed since, and every asset's result. Either way
        /// the latest before/after comparison is laid over it, so a later scan never hides it (owner's check 19.10.2026, item 8).
        /// </summary>
        void LoadLastReport()
        {
            report = LoadFor(".", scopeLabel, out summary, out rescanDue);
        }

        /// <summary>
        /// <see cref="LoadLastReport"/> for the project at <paramref name="root"/> (tests use a temporary folder): the report to
        /// show, the migration's result (null before anything was migrated), and whether the window's own scan is older than the
        /// last change a step made and the project must be scanned again.
        /// </summary>
        public static MigrationReport LoadFor(string root, string scopeLabel, out MigrationSummary summary, out bool rescanDue)
        {
            var st = MigrationState.Load(root);
            var ownPath = Path.Combine(root, LastReportPath);
            var own = File.Exists(ownPath) ? MigrationSummary.Read(ownPath) : null;
            bool ownIsProject = own != null && own.Scope == "Project";
            // The scan is current when it is newer than every change a step made or undid since.
            var changed = LastChangeUtc(st);
            rescanDue = scopeLabel == "Project" && changed.Length > 0 && (!ownIsProject || string.CompareOrdinal(own.GeneratedUtc, changed) < 0);
            summary = null;
            MigrationReport report;
            if (MigrationSummary.IsMigrated(st))
            {
                var before = MigrationSummary.Read(Path.Combine(root, MigrationRunner.ReportsDir, "scan-latest.internal.json"));
                var now = ownIsProject && !rescanDue ? own : null;
                summary = MigrationSummary.Build(st, before, now, KitOutcomes.Load(root), Edition.ProInstalled);
                report = now != null ? MigrationSummary.WithEarlierFindings(now, before, summary) : Newest(root);
            }
            else report = Newest(root);
            MigrationSummary.OverlayComparison(report, MigrationSummary.LatestComparison(root));
            return report;
        }

        /// <summary>The newest of the window's own scan and the reports of the steps and the command line.</summary>
        static MigrationReport Newest(string root)
        {
            var candidates = new List<string> { Path.Combine(root, LastReportPath) };
            var reports = Path.Combine(root, MigrationRunner.ReportsDir);
            if (Directory.Exists(reports)) candidates.AddRange(Directory.GetFiles(reports, "*-latest.internal.json"));
            foreach (var path in candidates.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc))
            {
                var r = MigrationSummary.Read(path);
                if (r != null) { KitOutcomes.Apply(r, KitOutcomes.Load(root)); return r; }
            }
            return null;
        }

        /// <summary>When a step last changed the project (or a rollback undid one), as the steps write the time; "" for never.</summary>
        public static string LastChangeUtc(MigrationState st) =>
            st.Steps.Where(x => (MigrationState.IsModifying(x.Id) || x.Status == StepStatus.RolledBack) && x.FinishedUtc.Length > 0)
                .Select(x => x.FinishedUtc).OrderByDescending(x => x, StringComparer.Ordinal).FirstOrDefault() ?? "";

        // ------------------------------------------------------------------ panels

        void RefreshAll()
        {
            if (summaryStrip == null) return;
            SummaryStrip.Build(summaryStrip, this);
            list.Rebuild();
            ShowDetails();
        }

        void ShowDetails()
        {
            right.Clear();
            if (ShaderPanelModel.IsShaderFile(selectedPath) && File.Exists(selectedPath)) { right.Add(new ShaderPanel(this, selectedPath)); return; }
            if (report == null) { OverviewPanel.BuildEmpty(right, this); return; }
            var asset = selectedPath == null ? null : report.Assets.FirstOrDefault(a => a.Path == selectedPath);
            var scene = selectedPath == null ? null : report.Scenes.FirstOrDefault(x => x.Path == selectedPath);
            if (scene != null) ScenePanel.Build(right, this, scene, asset);
            else if (asset != null) AssetPanel.Build(right, this, asset);
            else if (summary != null && summary.Migrated) ResultPanel.Build(right, this);
            else OverviewPanel.Build(right, this);
        }
    }
}
