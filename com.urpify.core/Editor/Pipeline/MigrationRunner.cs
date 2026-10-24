using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using Urpify.Reporting;
using Urpify.Scanner;
using Urpify.Generation;
using Urpify.Parsing;
using Urpify.Verify;
using Debug = UnityEngine.Debug;

namespace Urpify.Pipeline
{
    /// <summary>
    /// Runs the migration steps of TZ 8.1, one at a time. The window and the CLI call the same code.
    /// Steps that outlive a domain reload (captures, installing URP) are resumed from Urpify/state.json.
    /// </summary>
    [InitializeOnLoad]
    public static class MigrationRunner
    {
        public const string BeforeLabel = "before";
        public const string AfterLabel = "after";
        public const string ReportsDir = "Urpify/Reports";

        public static CaptureSettings Capture = new CaptureSettings();

        /// <summary>The project's Compare settings (TZ 4.3) as capture settings; the window uses them, the command line its arguments.</summary>
        public static void CaptureFromSettings()
        {
            var s = KitProjectSettings.instance;
            var (width, height) = CaptureSize(s.CompareSize, s.CompareWidth, s.CompareHeight, GameViewSize());
            Capture = new CaptureSettings
            {
                Width = width, Height = height, Frames = s.CompareFrames,
                Scenes = s.CompareScenes == CompareScenes.All ? SceneSelection.All : s.CompareScenes == CompareScenes.Selected ? SceneSelection.Selected : SceneSelection.BuildSettings,
                Selected = s.CompareSelected,
            };
        }
        /// <summary>
        /// The capture size the settings ask for: the Game view's (rounded, at least 64 x 64) when there is one, else the
        /// custom size. The command line has no Game view and passes its own size (-urpifyWidth, 960 x 540 by default).
        /// </summary>
        public static (int width, int height) CaptureSize(CompareSize mode, int width, int height, Vector2? gameView)
        {
            if (mode == CompareSize.GameView && gameView.HasValue && gameView.Value.x >= 64 && gameView.Value.y >= 64)
                return (Mathf.Min(7680, Mathf.RoundToInt(gameView.Value.x)), Mathf.Min(4320, Mathf.RoundToInt(gameView.Value.y)));
            return (width, height);
        }

        /// <summary>The size the main Game view renders at, in pixels; null in batch mode.</summary>
        static Vector2? GameViewSize()
        {
            if (Application.isBatchMode) return null;
            try { return Handles.GetMainGameViewSize(); }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// The frames after are taken the way the frames before were: same size, frames, time step and seed. A Game view resized
        /// between the two captures would otherwise give frames that cannot be compared.
        /// </summary>
        public static CaptureSettings LikeBefore(CaptureSettings current, CaptureSettings before)
        {
            var s = JsonUtility.FromJson<CaptureSettings>(JsonUtility.ToJson(current));
            if (before == null || before.Width < 1 || before.Height < 1) return s;
            s.Width = before.Width; s.Height = before.Height; s.Frames = before.Frames; s.Fps = before.Fps; s.Seed = before.Seed;
            return s;
        }

        public static CompareThresholds Thresholds = new CompareThresholds();

#if !URPIFY_ASSET_STORE
        static AddRequest addRequest;
        static int addAttempts;
#endif
        static int reloadWaitTicks;

        /// <summary>Raised when a step finishes (done, failed or skipped). The window refreshes on it.</summary>
        public static event Action<StepRecord> StepFinished;

        /// <summary>Raised when Show what breaks or Fix with Urpify ends: its kind, and whether every step succeeded.</summary>
        public static event Action<string, bool> ChainFinished;

        public static bool Busy => MigrationState.Load().Running >= 0;

        static MigrationRunner()
        {
            // Asset import workers are separate Unity processes that also run [InitializeOnLoad] code; the steps are not theirs.
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            CaptureRunner.Finished += OnCaptureFinished;
            CaptureRunner.StepProgressHook = StepProgress.Report;
            try
            {
                var undone = AfterRollback.Settle(".");
                if (undone.Count > 0) Debug.Log("[Urpify] Settings Unity wrote while the command-line rollback quit, put back: " + string.Join(", ", undone));
            }
            catch (Exception e) { Debug.LogWarning("[Urpify] Could not check the settings after the rollback: " + e.Message); }
            var st = MigrationState.Load();
            if (st.PendingWindowEnd.Length > 0)
            {
                // The previous step exited the Editor. What Unity wrote while exiting and while starting this session
                // (settings saved on quit, imports) belongs to that step; nobody could work in the project meanwhile.
                try { new BackupStore(".").RecordStep(st.PendingWindowEnd, false); }
                catch (Exception e) { Debug.LogWarning("[Urpify] Could not record the file list after the step: " + e.Message); }
                st.PendingWindowEnd = "";
                st.Save();
            }
            // A window an earlier session left open (it was killed before the Editor went idle) closes when this one is idle.
            if (st.InterruptedWindow.Length > 0 && st.Running < 0) WhenIdle(() => CloseInterruptedWindow(MigrationState.Load(), "."));
            if (st.Running < 0) return;
            if (st.RunningSession != EditorSession.Id)
            {
                // The step was running in an Editor that is gone (closed, crashed or killed). Never resume it here, and
                // never quit this Editor for a command-line step that belonged to another process.
                Debug.LogWarning($"[Urpify] Step {(StepId)st.Running} was started by another Editor ({st.RunningSession}); closed as interrupted in this one ({EditorSession.Id}).");
                CloseInterrupted(st, "Interrupted: the Editor was closed during the step. Run it again or roll back.", closeWindowNow: false);
                return;
            }
            var rec = st.Get((StepId)st.Running);
            // The watchdog thread of the previous domain is gone with it.
            StepProgress.Begin(rec.Id.ToString());
            StepProgress.Report("domain reload, phase " + rec.Phase);
            if ((StepId)st.Running == StepId.InstallUrp && rec.Phase >= 1)
            {
                // Package added; this reload is the recompile that brings URP in. Phase 2: the wait ran out and the package
                // cache was repaired, which reloads again (6000.3.26f1 imported URP for 66 s in the closing run of
                // 17.10.2026; the step was then closed as interrupted and the batch Editor never quit).
                EditorApplication.update -= WaitForUrp;
                EditorApplication.update += WaitForUrp;
            }
            else if ((StepId)st.Running == StepId.UnityConverter && rec.Phase == PhaseWaitingForPerson)
            {
                // Asset Store build: the person converts in Unity's window; the step waits for Continue.
            }
            else if ((StepId)st.Running == StepId.KitConverters && rec.Phase == 1)
            {
                // The rewritten scripts compiled; this reload brings in the code they use (phase 2).
                EditorApplication.delayCall += () =>
                {
                    var st2 = MigrationState.Load();
                    try { CompleteKitConverters(st2, st2.Get(StepId.KitConverters)); }
                    catch (Exception e) { Finish(st2, st2.Get(StepId.KitConverters), StepStatus.Failed, e.GetType().Name + ": " + e.Message); }
                };
            }
            else if ((StepId)st.Running == StepId.CaptureBefore || (StepId)st.Running == StepId.CaptureAfter)
            {
                if (!CaptureRunner.IsRunning) Guarded(rec.Id, () => CompleteCapture(st, rec, CaptureManifest.Load(st.Running == (int)StepId.CaptureBefore ? BeforeLabel : AfterLabel)));
            }
            else if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // A synchronous step cannot be running after a reload: the Editor was closed or crashed during it.
                bool exit = st.ExitEditorWhenDone && Application.isBatchMode;
                CloseInterrupted(st, "Interrupted. Run the step again or roll back.", closeWindowNow: false);
                // A command-line step is over: say so and quit, never leave a batch Editor waiting for its timeout.
                if (exit) ExitWithStepResult(rec, StepStatus.Interrupted, rec.Message);
            }
        }

        /// <summary>
        /// Closes the running step as interrupted (QA-001). Its file-list window ends now, when the interruption is found,
        /// and its backup is completed with what changed since it began: the files the step changed before it was stopped
        /// are its changes, and a rollback undoes them. Nobody could work in the project between the stop and now.
        /// </summary>
        public static void CloseInterrupted(MigrationState st, string message, string root = ".", bool closeWindowNow = true)
        {
            if (st.Running < 0) return;
            var rec = st.Get((StepId)st.Running);
            var reason = StepProgress.StoppedReason(root);
            if (reason != null) message = reason + " Run the step again or roll back.";
            rec.Status = StepStatus.Interrupted;
            rec.Message = message;
            rec.FinishedUtc = Now();
            rec.Phase = 0;
            st.Running = -1;
            st.RunAll = false;
            st.Chain.Clear();
            st.ChainKind = "";
            st.ExitEditorWhenDone = false;
            st.InterruptedWindow = rec.Id.ToString();
            st.Save(root);
            if (root == ".") StepProgress.Clear();
            if (closeWindowNow) CloseInterruptedWindow(st, root);
            else if (root == ".") WhenIdle(() => CloseInterruptedWindow(MigrationState.Load(), "."));
        }

        /// <summary>
        /// Ends the file-list window of a step closed as interrupted and completes its backup with everything that changed
        /// since the backup began: the step's own writes before the stop and what Unity wrote while starting again.
        /// </summary>
        public static void CloseInterruptedWindow(MigrationState st, string root = ".")
        {
            if (st.InterruptedWindow.Length == 0) return;
            if (Enum.TryParse(st.InterruptedWindow, out StepId id))
            {
                var rec = st.Get(id);
                var store = new BackupStore(root);
                try { store.RecordStep(id.ToString(), false); }
                catch (Exception e) { Debug.LogWarning("[Urpify] Could not record the file list after the interrupted step: " + e.Message); }
                if (rec.SnapshotId.Length > 0)
                {
                    try
                    {
                        if (!store.CompleteIfOpen(rec.SnapshotId)) rec.SnapshotId = "";
                        else if (MigrationState.HasRollback(id)) KeepTextureImportSettings(store, store.Load(rec.SnapshotId), rec, root == ".");
                    }
                    catch (Exception e) { Debug.LogWarning("[Urpify] Could not close the backup of the interrupted step: " + e.Message); }
                }
            }
            st.InterruptedWindow = "";
            st.Save(root);
        }

        /// <summary>Runs <paramref name="action"/> once the Editor has compiled, imported and stood still for three seconds.</summary>
        static void WhenIdle(Action action)
        {
            double quietSince = -1;
            void Tick()
            {
                var now = EditorApplication.timeSinceStartup;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || quietSince < 0) { quietSince = now; return; }
                if (now - quietSince < 3) return;
                EditorApplication.update -= Tick;
                try { action(); } catch (Exception e) { Debug.LogException(e); }
            }
            EditorApplication.update += Tick;
        }

        // ------------------------------------------------------------------ entry points

        /// <summary>Starts a step. Returns an error message, or null when it started (it may finish later).</summary>
        public static string Run(StepId id, bool exitEditorWhenDone = false)
        {
            var st = MigrationState.Load();
            if (st.Running >= 0) return $"Step {(StepId)st.Running} is still running.";
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Exit Play Mode first.";
            var blocked = Blocker(id, st);
            if (blocked != null) { st.RunAll = false; st.Chain.Clear(); st.Save(); return blocked; }
            CloseInterruptedWindow(st);
            SettleIfInProgress(st);
            new BackupStore(".").RecordStep(id.ToString(), true);
            var rec = st.Get(id);
            KeepEarlierRun(rec);
            rec.Status = StepStatus.Running;
            rec.StartedUtc = Now();
            rec.FinishedUtc = "";
            rec.Message = "";
            rec.Details.Clear();
            rec.SetResult("");
            rec.Phase = 0;
            st.Running = (int)id;
            st.RunningSession = EditorSession.Id;
            st.ExitEditorWhenDone = exitEditorWhenDone;
            st.Save();
            StepProgress.Begin(id.ToString());
            try
            {
                switch (id)
                {
                    case StepId.Scan: RunScan(st, rec); break;
                    case StepId.CaptureBefore: StartCapture(st, rec, BeforeLabel); break;
                    case StepId.Backup: RunBackup(st, rec); break;
                    case StepId.InstallUrp: StartInstallUrp(st, rec); break;
                    case StepId.UnityConverter: RunUnityConverter(st, rec); break;
                    case StepId.KitConverters: RunKitConverters(st, rec); break;
                    case StepId.CaptureAfter: StartCapture(st, rec, AfterLabel); break;
                    case StepId.Review: RunReview(st, rec); break;
                }
            }
            catch (NotEnoughDiskSpaceException e)
            {
                // Nothing changed yet: the step did not start (code review R-09).
                Finish(st, rec, StepStatus.Failed, e.Message);
            }
            catch (Exception e)
            {
                Finish(st, rec, StepStatus.Failed, e.GetType().Name + ": " + e.Message);
            }
            return null;
        }

        /// <summary>URP renders the project now (its asset is in Graphics or Quality settings).</summary>
        public static bool UrpActive => UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;

        /// <summary>
        /// Why <paramref name="id"/> cannot run now, in the window's language, or null when it can. The window disables the
        /// step's button and shows this; the command line fails the step with it (owner's check 19.10.2026, item 2: "after" was
        /// captured on Built-in, and Urpify converters ran before Unity's converter).
        /// </summary>
        public static string Blocker(StepId id, MigrationState st)
        {
            var key = BlockerKey(id, st, UrpInstaller.IsLoaded(), UrpActive, CaptureManifest.Load(BeforeLabel)?.Pipeline, EffectiveShaderOutput);
            return key == null ? null : Loc.Get(key);
        }

        /// <summary>
        /// <see cref="Blocker"/> without the Editor: what each step needs from the ones before it.
        ///   Capture before: the Backup (Unity can re-save assets while the scenes play), and Built-in still rendering.
        ///   Unity converter: URP installed. Urpify converters: URP installed and Unity's converter done (with Keep Built-in
        ///   support the project stays on Built-in, so only URP installed). Capture after: frames from before, and URP rendering.
        /// </summary>
        /// <param name="beforePipeline">The pipeline of the capture before, null when there is none.</param>
        public static string BlockerKey(StepId id, MigrationState st, bool urpLoaded, bool urpActive, string beforePipeline, ShaderOutput output)
        {
            switch (id)
            {
                case StepId.CaptureBefore:
                    if (st.Get(StepId.Backup).Status != StepStatus.Done) return "step.blocked.backupFirst";
                    return urpActive ? "step.blocked.beforeOnUrp" : null;
                case StepId.UnityConverter:
                    return urpLoaded ? null : "step.blocked.installUrpFirst";
                case StepId.KitConverters:
                    if (!urpLoaded) return "step.blocked.installUrpFirst";
                    if (output != ShaderOutput.KeepBuiltInSupport && st.Get(StepId.UnityConverter).Status != StepStatus.Done) return "step.blocked.unityConverterFirst";
                    return null;
                case StepId.CaptureAfter:
                    // No scenes: Capture before was skipped, and this step skips as well.
                    if (st.Get(StepId.CaptureBefore).Status == StepStatus.Skipped) return null;
                    if (beforePipeline == null) return "step.blocked.captureBeforeFirst";
                    return urpActive ? null : "step.blocked.afterOnBuiltIn";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Show what breaks (owner's check 19.10.2026, item 3): the frames before, URP with Unity's converter and nothing of
        /// Urpify's, the frames after. The steps already done are left out. Returns an error message, or null when it started.
        /// </summary>
        public static string ShowWhatBreaks() => StartChain(ChainShowBreaks, ShowBreaksPlan(MigrationState.Load(), CaptureManifest.Load(BeforeLabel) != null));

        /// <summary>After Show what breaks: Urpify's fixes, the frames after again, the final report.</summary>
        public static string FixWithUrpify() => StartChain(ChainFix, FixPlan(MigrationState.Load()));

        public const string ChainShowBreaks = "ShowBreaks", ChainFix = "Fix";

        /// <summary>The steps of Show what breaks that are left to run, in order; empty when URP is already in place.</summary>
        public static List<StepId> ShowBreaksPlan(MigrationState st, bool haveBefore)
        {
            var plan = new List<StepId>();
            if (st.Get(StepId.UnityConverter).Status == StepStatus.Done) return plan;
            bool Done(StepId id) => st.Get(id).Status == StepStatus.Done || st.Get(id).Status == StepStatus.Skipped;
            if (!Done(StepId.Scan)) plan.Add(StepId.Scan);
            if (!Done(StepId.Backup)) plan.Add(StepId.Backup);
            if (!Done(StepId.CaptureBefore) || !haveBefore) plan.Add(StepId.CaptureBefore);
            if (!Done(StepId.InstallUrp)) plan.Add(StepId.InstallUrp);
            plan.Add(StepId.UnityConverter);
            plan.Add(StepId.CaptureAfter);
            return plan;
        }

        /// <summary>The steps of Fix with Urpify: Urpify converters, the frames after (when there were frames before), Review.</summary>
        public static List<StepId> FixPlan(MigrationState st)
        {
            var plan = new List<StepId> { StepId.KitConverters };
            if (st.Get(StepId.CaptureBefore).Status == StepStatus.Done) plan.Add(StepId.CaptureAfter);
            plan.Add(StepId.Review);
            return plan;
        }

        static string StartChain(string kind, List<StepId> plan)
        {
            var st = MigrationState.Load();
            if (st.Running >= 0) return $"Step {(StepId)st.Running} is still running.";
            if (plan.Count == 0) return Loc.Get("ui.breaks.alreadyOnUrp");
            st.RunAll = false;
            st.Chain.Clear();
            st.ChainKind = "";
            st.ChainKind = kind;
            st.Chain = plan.Skip(1).ToList();
            st.Save();
            var error = Run(plan[0]);
            if (error != null) { st = MigrationState.Load(); st.Chain.Clear(); st.ChainKind = ""; st.Save(); }
            return error;
        }

        /// <summary>
        /// The step a chain (Run all, Show what breaks, Fix with Urpify) runs after a step ended, or null when it stops: after a
        /// step that did not succeed, at its end, or when the next step is blocked (its button says why).
        /// </summary>
        public static StepId? NextInChain(MigrationState st, bool ok)
        {
            if (st.RunAll)
            {
                var next = ok ? NextStep(st) : null;
                if (next == null) st.RunAll = false;
                return next;
            }
            if (st.Chain.Count == 0) return null;
            if (!ok) { st.Chain.Clear(); return null; }
            var id = st.Chain[0];
            st.Chain.RemoveAt(0);
            return id;
        }

        /// <summary>
        /// A step about to run again keeps the backup of its last run (interrupted, failed or done): a rollback undoes the new
        /// run, then that one (QA-001).
        /// </summary>
        public static void KeepEarlierRun(StepRecord rec)
        {
            if (rec.SnapshotId.Length > 0 && rec.Status != StepStatus.RolledBack && rec.Status != StepStatus.NotRun)
                rec.EarlierSnapshots.Add(rec.SnapshotId);
            if (rec.Status == StepStatus.RolledBack || rec.Status == StepStatus.NotRun) rec.EarlierSnapshots.Clear();
            rec.SnapshotId = "";
        }

        /// <summary>Runs every step that is not done yet, in order; stops at the first failure.</summary>
        public static string RunAll()
        {
            var st = MigrationState.Load();
            var next = NextStep(st);
            if (next == null) return "Every step is done.";
            st.RunAll = true;
            st.Save();
            return Run(next.Value);
        }

        static StepId? NextStep(MigrationState st) =>
            st.Steps.Where(s => s.Status != StepStatus.Done && s.Status != StepStatus.Skipped).OrderBy(s => s.Id).Select(s => (StepId?)s.Id).FirstOrDefault();

        /// <summary>
        /// Undoes every completed modifying step after <paramref name="target"/>, newest first (target itself stays).
        /// Pass null to undo all. Files changed by someone else after a step are left alone and listed.
        /// Unity keeps settings in memory: the Editor must restart to load the restored files (the CLI exits anyway).
        /// </summary>
        public static RollbackResult Rollback(StepId? target) => Rollback(target, ".");

        /// <summary><see cref="Rollback(StepId?)"/> for the project at <paramref name="root"/> (tests use a temporary folder).</summary>
        public static RollbackResult Rollback(StepId? target, string root)
        {
            var st = MigrationState.Load(root);
            var store = new BackupStore(root);
            var total = new RollbackResult();
            if (st.Running >= 0 && st.RunningSession == EditorSession.Id)
            {
                total.Errors.Add($"Step {(StepId)st.Running} is still running; roll back after it ends.");
                return total;
            }
            // Changes made on request after the steps (Bake environment) go first, newest first: they changed files the
            // steps left behind, and a step's rollback skips a file whose content is not what the step left.
            if (root == ".")
                foreach (var extra in st.Extras.Where(e => e.Status == StepStatus.Done && e.SnapshotId.Length > 0).Reverse().ToList())
                {
                    var r = EnvironmentBake.Undo(extra);
                    total.Restored.AddRange(r.Restored); total.Removed.AddRange(r.Removed); total.Skipped.AddRange(r.Skipped); total.Errors.AddRange(r.Errors);
                }
            // A step that is still marked running belongs to an Editor that is gone: its changes are part of the rollback.
            if (st.Running >= 0) CloseInterrupted(st, "Interrupted: the Editor was closed during the step.", root);
            CloseInterruptedWindow(st, root);
            // A failed step's backup is closed when it fails; one closed by an older version of Urpify may still be open.
            foreach (var rec in st.Steps.Where(s => MigrationState.HasRollback(s.Id)))
                foreach (var id in MigrationState.Snapshots(rec).ToList())
                    try { store.CompleteIfOpen(id); } catch (Exception e) { total.Errors.Add($"{rec.Id}: backup {id}: {e.Message}"); }
            SettleIfInProgress(st, root);
            // Every run of every step after the target that changed files, newest first: a step run again after an
            // interruption is undone run by run, back to what the project held before its first run (QA-001).
            var targets = st.Steps.Where(s => (target == null || s.Id > target.Value) && MigrationState.HasRollback(s.Id)).ToList();
            var runs = targets.SelectMany(rec => MigrationState.Snapshots(rec).Select(id => (rec, id)))
                .OrderByDescending(x => x.id, StringComparer.Ordinal).ToList();
            var undone = new Dictionary<StepRecord, RollbackResult>();
            // A step with a run that could not be undone fully (a file held by another program, a backup that did not load)
            // keeps its backups and its status, so rolling back again finishes the job (code review R-01, 18.10.2026).
            var unfinished = new Dictionary<StepRecord, int>();
            foreach (var (rec, id) in runs)
            {
                var snap = store.Load(id);
                if (snap == null || !snap.Complete)
                {
                    total.Errors.Add($"{rec.Id}: backup {id} is missing or incomplete");
                    unfinished[rec] = unfinished.TryGetValue(rec, out var n0) ? n0 + 1 : 1;
                    continue;
                }
                RollbackResult r;
                try { r = store.Rollback(snap); }
                catch (Exception e) { r = new RollbackResult(); r.Errors.Add($"backup {id}: {e.Message}"); }
                if (r.Errors.Count > 0) unfinished[rec] = (unfinished.TryGetValue(rec, out var n) ? n : 0) + r.Errors.Count;
                total.Restored.AddRange(r.Restored.Where(p => !total.Restored.Contains(p)));
                total.Removed.AddRange(r.Removed.Where(p => !total.Removed.Contains(p)));
                total.Skipped.AddRange(r.Skipped.Where(p => !total.Skipped.Contains(p)));
                total.Errors.AddRange(r.Errors);
                total.Between.AddRange(r.Between.Where(b => !total.Between.Contains(b))); total.Kept.AddRange(r.Kept);
                if (!undone.TryGetValue(rec, out var sum)) undone[rec] = sum = new RollbackResult();
                sum.Restored.AddRange(r.Restored); sum.Removed.AddRange(r.Removed); sum.Skipped.AddRange(r.Skipped);
            }
            foreach (var rec in targets.Where(r => unfinished.ContainsKey(r)))
            {
                rec.Message = $"Rollback not finished: {unfinished[rec]} files could not be restored. Close the programs that use them and roll back again.";
                rec.SetResult("step.rollbackUnfinished", unfinished[rec]);
            }
            foreach (var rec in targets.Where(r => undone.ContainsKey(r) && !unfinished.ContainsKey(r)))
            {
                var r = undone[rec];
                rec.Status = StepStatus.RolledBack;
                rec.Message = $"Rolled back: {r.Restored.Distinct().Count()} restored, {r.Removed.Distinct().Count()} removed, {r.Skipped.Distinct().Count()} left as changed later.";
                rec.SetResult("step.rolledBack", r.Restored.Distinct().Count(), r.Removed.Distinct().Count(), r.Skipped.Distinct().Count());
                rec.FinishedUtc = Now();
                rec.EarlierSnapshots.Clear();
            }
            // A file a later run left changed was skipped there, then restored by an earlier run: it is not skipped.
            total.Skipped.RemoveAll(p => total.Restored.Contains(p));
            // Steps after the target that did not change files are reset so the list reads correctly.
            foreach (var rec in st.Steps.Where(s => (target == null || s.Id > target.Value) && !undone.ContainsKey(s) && !unfinished.ContainsKey(s) && s.Status != StepStatus.NotRun))
            {
                rec.Status = StepStatus.NotRun;
                rec.Message = "";
                rec.Details.Clear();
                rec.SetResult("");
                rec.SnapshotId = "";
                rec.EarlierSnapshots.Clear();
            }
            st.Running = -1;
            st.RunAll = false;
            st.Chain.Clear();
            st.ChainKind = "";
            st.Save(root);
            if (target == null && unfinished.Count == 0) store.ClearSteps(); // a later migration starts with fresh step windows
            if (root != ".") return total;
            // A command-line rollback quits right after this, and Unity writes settings of packages it still has loaded on
            // the way out (ShaderGraphSettings.asset, a re-saved InputManager.asset; QA-005). The next Editor puts them back.
            if (Application.isBatchMode)
                try { AfterRollback.Record("."); } catch (Exception e) { Debug.LogWarning("[Urpify] Could not record the settings after the rollback: " + e.Message); }
            // The restored settings must win over the Editor's in-memory copy: nothing may save the old one on exit.
            foreach (var f in Directory.GetFiles("ProjectSettings", "*.asset"))
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(f.Replace('\\', '/')))
                    if (o != null) EditorUtility.ClearDirty(o);
            return total;
        }

        // ------------------------------------------------------------------ steps

        static void RunScan(MigrationState st, StepRecord rec)
        {
            var scanner = new ProjectScanner();
            scanner.RunToEnd(new ScanOptions());
            var paths = WriteReport(scanner.Report, "scan");
            var s = scanner.Report.Summary;
            rec.Details.Add($"{scanner.Report.FilesScanned} files, {scanner.Report.Findings.Count} findings, scenes: {s.ScenesPink} with pink objects, {s.ScenesSilent} with silent breaks, {s.ScenesUnaffected} unaffected");
            rec.Details.AddRange(paths);
            rec.SetResult("step.Scan.result", scanner.Report.FilesScanned, s.ScenesPink, s.ScenesSilent, s.ScenesUnaffected);
            Finish(st, rec, StepStatus.Done, "");
        }

        static void StartCapture(MigrationState st, StepRecord rec, string label)
        {
            // Nothing to capture (a project without scenes, QA-022): the step has nothing to do and the migration goes on.
            if (CaptureRunner.ResolveScenes(Capture).Length == 0)
            {
                rec.SetResult("step.Capture.noScenes");
                Finish(st, rec, StepStatus.Skipped, "No scenes to capture, so there is nothing to compare.");
                return;
            }
            var settings = label == AfterLabel ? LikeBefore(Capture, CaptureManifest.Load(BeforeLabel)?.Settings) : Capture;
            var error = CaptureRunner.Start(label, settings, exitEditorWhenDone: false);
            if (error != null) Finish(st, rec, StepStatus.Failed, error);
            // Otherwise OnCaptureFinished completes the step after Play Mode.
        }

        static void OnCaptureFinished(CaptureManifest m)
        {
            var st = MigrationState.Load();
            if (st.Running != (int)StepId.CaptureBefore && st.Running != (int)StepId.CaptureAfter) return;
            var id = (StepId)st.Running;
            Guarded(id, () => CompleteCapture(st, st.Get(id), m));
        }

        static void CompleteCapture(MigrationState st, StepRecord rec, CaptureManifest m)
        {
            if (m == null) { Finish(st, rec, StepStatus.Failed, "The capture produced no results."); return; }
            rec.Details.Add($"{m.Scenes.Count} scenes, {m.Scenes.Sum(s => s.Views.Count)} images, pipeline {m.Pipeline}, {CaptureManifest.Folder(m.Label)}");
            foreach (var s in m.Scenes.Where(s => s.Error.Length > 0)) rec.Details.Add(s.Scene + ": " + s.Error);
            if (m.Error.Length > 0) { Finish(st, rec, StepStatus.Failed, m.Error); return; }
            if (m.Cancelled) { Finish(st, rec, StepStatus.Failed, "Cancelled."); return; }
            rec.SetResult("step.Capture.result", m.Scenes.Count, m.Scenes.Sum(s => s.Views.Count));
            if (rec.Id == StepId.CaptureAfter && CaptureManifest.Load(BeforeLabel) != null)
            {
                var report = LatestScanReport();
                var kit = KitOutcomes.Load();
                KitOutcomes.Apply(report, kit);
                var cmp = VerifyRunner.Compare(BeforeLabel, AfterLabel, Thresholds, true, VerifyRunner.KnownBrokenShaders(report));
                KitOutcomes.MarkReview(kit, VerifyRunner.Apply(report, cmp, BeforeLabel, AfterLabel, kit != null));
                report.UrpVersion = UrpInstaller.LoadedVersion() ?? report.UrpVersion;
                rec.Details.AddRange(WriteReport(report, "verify"));
                var s = report.Summary;
                rec.Details.Add($"Observed: {s.ObservedPink} pink, {s.ObservedChanged} changed, {s.ObservedSame} same");
                if (VerifyRunner.SamePipeline(CaptureManifest.Load(BeforeLabel), m)) rec.SetResult("step.CaptureAfter.samePipeline", VerifyRunner.PipelineName(m.Pipeline));
                else rec.SetResult("step.CaptureAfter.result", m.Scenes.Count, m.Scenes.Sum(x => x.Views.Count), s.ObservedPink, s.ObservedChanged, s.ObservedSame);
            }
            Finish(st, rec, StepStatus.Done, "");
        }

        static void RunBackup(MigrationState st, StepRecord rec)
        {
            var store = new BackupStore(".");
            var snap = store.Begin("baseline");
            store.Complete(snap);
            rec.SnapshotId = snap.Id;
            rec.Details.Add($"{snap.Files.Count} files copied to {Path.Combine("Urpify", "Backup", snap.Id)}");
            rec.SetResult("step.Backup.result", snap.Files.Count);
            Finish(st, rec, StepStatus.Done, "");
        }

        static void StartInstallUrp(MigrationState st, StepRecord rec)
        {
            if (UrpInstaller.IsLoaded())
            {
                rec.SetResult("step.InstallUrp.already", UrpInstaller.LoadedVersion() ?? "");
                Finish(st, rec, StepStatus.Done, "URP is already installed and loaded.");
                return;
            }
            if (BuildInfo.AssetStore) { StartInstallUrpByHand(st, rec); return; }
#if !URPIFY_ASSET_STORE
            var version = UrpInstaller.BundledVersion();
            if (version == null) { Finish(st, rec, StepStatus.Failed, "This Editor does not ship URP in BuiltInPackages; install it from the Package Manager."); return; }
            var snap = new BackupStore(".").Begin(StepId.InstallUrp.ToString());
            rec.SnapshotId = snap.Id;
            rec.Details.Add("Installing " + UrpInstaller.PackageName + "@" + version);
            st.Save();
            if (UrpInstaller.InManifest())
            {
                // Listed but not loaded: the package cache is broken.
                UrpInstaller.RepairPackageCache();
                rec.Details.Add("URP was in Packages/manifest.json but not loaded; package cache repaired.");
            }
            else
            {
                addAttempts = 1;
                addRequest = Client.Add(UrpInstaller.PackageName + "@" + version);
            }
            rec.Phase = 1;
            st.Save();
            reloadWaitTicks = 0;
            EditorApplication.update -= WaitForUrp;
            EditorApplication.update += WaitForUrp;
#endif
        }

        /// <summary>Phase of Install URP in the Asset Store build while the step waits for URP to be installed by hand.</summary>
        public const int PhaseWaitingForPerson = 3;

        /// <summary>
        /// Install URP in the Asset Store build (Submission Guidelines 2.5.1.e: an offering must not add packages to the
        /// project): the Package Manager opens on URP, the person clicks Install there, and the step goes on once URP is loaded
        /// (it survives the domain reload the install brings). The command line cannot wait for a person: it asks for URP first.
        /// </summary>
        static void StartInstallUrpByHand(MigrationState st, StepRecord rec)
        {
            if (Application.isBatchMode)
            {
                Finish(st, rec, StepStatus.Failed, Loc.Get("step.InstallUrp.storeCli"));
                return;
            }
            var snap = new BackupStore(".").Begin(StepId.InstallUrp.ToString());
            rec.SnapshotId = snap.Id;
            rec.Phase = PhaseWaitingForPerson;
            rec.Details.Add("Package Manager opened on " + UrpInstaller.PackageName + "; waiting for it to be installed.");
            rec.SetResult("step.InstallUrp.storeWaiting");
            st.Save();
            OpenPackageManagerOnUrp();
            reloadWaitTicks = 0;
            EditorApplication.update -= WaitForUrp;
            EditorApplication.update += WaitForUrp;
        }

        /// <summary>Package Manager > Unity Registry, on URP (public API, also after a rollback to remove it).</summary>
        public static void OpenPackageManagerOnUrp() => UnityEditor.PackageManager.UI.Window.Open(UrpInstaller.PackageName);

        /// <summary>
        /// Stops waiting for the person (Asset Store build): the step ends as failed, its backup is closed with whatever
        /// changed meanwhile, so a rollback still undoes it.
        /// </summary>
        public static void StopWaitingForPerson()
        {
            var st = MigrationState.Load();
            if (st.Running < 0 || st.Get((StepId)st.Running).Phase != PhaseWaitingForPerson) return;
            EditorApplication.update -= WaitForUrp;
            Finish(st, st.Get((StepId)st.Running), StepStatus.Failed, Loc.Get("step.storeStopped"));
        }

        /// <summary>The running step waits for the person (Asset Store build: Install URP or Unity's converter by hand).</summary>
        public static bool WaitingForPerson(MigrationState st) => st.Running >= 0 && st.Get((StepId)st.Running).Phase == PhaseWaitingForPerson;

        /// <summary>A Package Manager add that it cancelled itself is asked once more (QA-019); anything else fails the step.</summary>
        public static bool RetryAdd(string error, int attempts) => attempts < 2 && (error ?? "").IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0;

        static void WaitForUrp() => Guarded(StepId.InstallUrp, WaitForUrpTick, () => EditorApplication.update -= WaitForUrp);

        static void WaitForUrpTick()
        {
#if !URPIFY_ASSET_STORE
            if (addRequest != null)
            {
                if (!addRequest.IsCompleted) return;
                if (addRequest.Status == StatusCode.Failure)
                {
                    // The Package Manager now and then cancels an add for reasons of its own ("Operation cancelled", 1 of
                    // 19 installs in the QA run; the same step run again succeeded): ask once more before failing (QA-019).
                    var error = addRequest.Error?.message ?? "";
                    if (RetryAdd(error, addAttempts))
                    {
                        addAttempts++;
                        var stRetry = MigrationState.Load();
                        stRetry.Get(StepId.InstallUrp).Details.Add("Package Manager: " + error + " - asked again.");
                        stRetry.Save();
                        StepProgress.Report("Install URP: asked the Package Manager again");
                        addRequest = Client.Add(UrpInstaller.PackageName + "@" + UrpInstaller.BundledVersion());
                        return;
                    }
                    EditorApplication.update -= WaitForUrp;
                    var st0 = MigrationState.Load();
                    Finish(st0, st0.Get(StepId.InstallUrp), StepStatus.Failed, "Package Manager: " + addRequest.Error?.message);
                    addRequest = null;
                    return;
                }
                addRequest = null;
            }
#endif
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { reloadWaitTicks = 0; return; }
            var st = MigrationState.Load();
            var rec = st.Get(StepId.InstallUrp);
            if (UrpInstaller.IsLoaded())
            {
                EditorApplication.update -= WaitForUrp;
                var store = new BackupStore(".");
                var snap = store.Load(rec.SnapshotId);
                if (snap != null) { store.Complete(snap); rec.Details.Add($"{snap.Changed.Count} files changed, {snap.Created.Count} created"); }
                var installed = rec.Phase == PhaseWaitingForPerson ? UrpInstaller.LoadedVersion() : UrpInstaller.BundledVersion();
                rec.SetResult("step.InstallUrp.result", installed ?? "");
                Finish(st, rec, StepStatus.Done, "URP " + installed + " installed and loaded.");
                return;
            }
            // The person installs URP in the Package Manager: no time limit.
            if (rec.Phase == PhaseWaitingForPerson) return;
#if !URPIFY_ASSET_STORE
            // Give the Package Manager time to resolve and compile after the reload.
            if (++reloadWaitTicks < 3000) return;
            EditorApplication.update -= WaitForUrp;
            if (rec.Phase == 1)
            {
                rec.Phase = 2;
                st.Save();
                UrpInstaller.RepairPackageCache();
                reloadWaitTicks = 0;
                EditorApplication.update += WaitForUrp;
                return;
            }
            Finish(st, rec, StepStatus.Failed,
                "URP is in Packages/manifest.json but its assemblies did not load. Close the Editor, delete Library/PackageManager/ProjectCache and open the project again.");
#endif
        }

        static void RunUnityConverter(MigrationState st, StepRecord rec)
        {
            if (!UrpInstaller.IsLoaded()) { Finish(st, rec, StepStatus.Failed, "URP is not loaded. Run Install URP first."); return; }
            var store = new BackupStore(".");
            var snap = store.Begin(StepId.UnityConverter.ToString());
            rec.SnapshotId = snap.Id;
            st.Save();
            // Post-processing v2 needs Unity Search, so the converter may finish in a later Editor update.
            UnityConverterRunner.RunAsync(result => Guarded(StepId.UnityConverter, () =>
            {
                if (result.ByHand) { WaitForConverterWindow(MigrationState.Load(), result); return; }
                AssetDatabase.SaveAssets();
                AfterPendingImports(StepId.UnityConverter, () => CompleteUnityConverter(MigrationState.Load(), store, snap, result));
            }));
        }

        /// <summary>
        /// Asset Store build, when Unity's batch converter cannot do the job with public API (Post-processing v2, URP 17.3):
        /// Unity's converter window opens, the person converts there, and the step waits, its backup open, until they press
        /// Continue (<see cref="ContinueAfterConverterWindow"/>), so the rollback undoes what they converted.
        /// </summary>
        static void WaitForConverterWindow(MigrationState st, ConverterRunResult result)
        {
            var rec = st.Get(StepId.UnityConverter);
            if (Application.isBatchMode)
            {
                rec.Details.AddRange(result.Notes);
                Finish(st, rec, StepStatus.Failed, Loc.Get("step.UnityConverter.storeCli"));
                return;
            }
            rec.Phase = PhaseWaitingForPerson;
            rec.Details.AddRange(result.Notes);
            rec.SetResult("step.UnityConverter.storeWaiting");
            st.Save();
            OpenConverterWindow();
        }

        public static void OpenConverterWindow() => EditorApplication.ExecuteMenuItem(UnityConverterRunner.ConverterWindowMenu);

        /// <summary>The person converted in Unity's window (Asset Store build): the step saves, rescans and ends as usual.</summary>
        public static void ContinueAfterConverterWindow()
        {
            var st = MigrationState.Load();
            if (st.Running != (int)StepId.UnityConverter || st.Get(StepId.UnityConverter).Phase != PhaseWaitingForPerson) return;
            var rec = st.Get(StepId.UnityConverter);
            var store = new BackupStore(".");
            var snap = store.Load(rec.SnapshotId);
            if (snap == null) { Finish(st, rec, StepStatus.Failed, "The step's backup is missing."); return; }
            var result = new ConverterRunResult { Ran = true, Api = "Render Pipeline Converter window, by hand" };
            result.Notes.AddRange(rec.Details);
            rec.Details.Clear();
            Guarded(StepId.UnityConverter, () =>
            {
                AssetDatabase.SaveAssets();
                AfterPendingImports(StepId.UnityConverter, () => CompleteUnityConverter(MigrationState.Load(), store, snap, result));
            });
        }

        static void CompleteUnityConverter(MigrationState st, BackupStore store, Snapshot snap, ConverterRunResult result)
        {
            var rec = st.Get(StepId.UnityConverter);
            store.Complete(snap);
            foreach (var k in KeepTextureImportSettings(store, snap, rec)) result.Notes.Add(k.Texture + ": " + k.Note);
            Directory.CreateDirectory(ReportsDir);
            File.WriteAllText(Path.Combine(ReportsDir, "unity-converter.json"), JsonUtility.ToJson(result, true));
            rec.Details.Add($"API {result.Api}: {result.Items.Count} items, {result.Success} succeeded, {result.Warnings} warnings, {result.Errors} errors, {result.NoUpgrader} without upgrader");
            rec.Details.AddRange(result.Notes);
            rec.Details.Add($"{snap.Changed.Count} files changed, {snap.Created.Count} created, {snap.Deleted.Count} deleted");
            // Success by the converter's log and a rescan, not by an exit code (TZ 8.2).
            var rescan = new ProjectScanner();
            rescan.RunToEnd(new ScanOptions());
            int left = rescan.Report.Findings.Count(f => f.RuleId == RuleIds.MaterialUnityConverter);
            rec.Details.Add($"Rescan: {left} materials still use a Built-in shader that Unity's converter upgrades");
            if (left == 0) rec.SetResult("step.UnityConverter.result", result.Success);
            else rec.SetResult("step.UnityConverter.resultLeft", result.Success, left);
            if (!result.Ran) Finish(st, rec, StepStatus.Failed, result.Error);
            else Finish(st, rec, StepStatus.Done, left == 0 ? "" : $"{left} materials were not upgraded; see the report.");
        }

        /// <summary>Pro converters found in the loaded assemblies, in their order.</summary>
        public static List<IShaderFileConverter> ShaderConverters() =>
            TypeCache.GetTypesDerivedFrom<IShaderFileConverter>().Where(t => !t.IsAbstract && !t.IsInterface && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => (IShaderFileConverter)Activator.CreateInstance(t)).OrderBy(c => c.Order).ToList();

        /// <summary>Output for the Urpify converters step when set (the command line's -urpifyShaderOutput); else the project setting.</summary>
        public static ShaderOutput? ShaderOutputOverride;

        public static ShaderOutput EffectiveShaderOutput => ShaderOutputOverride ?? KitProjectSettings.instance.Output;

        /// <summary>GrabPass source for the Urpify converters step when set (the command line's -urpifyGrabPass); else the project setting.</summary>
        public static GrabPassSource? GrabPassOverride;

        /// <summary>
        /// Urpify converters (TZ 6.1, 8.1 step 6). Without Urpify Pro the step still runs what the Free package does:
        /// URP Asset settings for shaders URP draws as they are, and the fixes after Unity's converter (materials, scenes,
        /// Shader.Find in scripts). With Pro, every Built-in shader a converter handles is rewritten in place (Output =
        /// Replace original, or the original plus a URP SubShader with Keep Built-in support), then compiled with the
        /// keywords its materials use; a shader that does not compile is restored and reported for manual work with the
        /// compiler's message. Then URP Asset settings, the asset fixes after Unity's converter (materials, scenes), and
        /// the run-time scripts. Scripts need Unity to compile them (phase 1); a script whose rewrite does not compile is
        /// restored. After the domain reload (phase 2) the renderer features the scripts need join every renderer.
        /// Everything is in this step's backup.
        /// </summary>
        static void RunKitConverters(MigrationState st, StepRecord rec)
        {
            var converters = ShaderConverters();
            if (!UrpInstaller.IsLoaded()) { Finish(st, rec, StepStatus.Failed, "URP is not loaded. Run Install URP first."); return; }
            var store = new BackupStore(".");
            var snap = store.Begin(StepId.KitConverters.ToString());
            rec.SnapshotId = snap.Id;
            st.Save();
            // A run after a failed one in the same session starts its compile rounds again (code review R-15.11).
            s_CompileRounds = 0;

            foreach (var conv in converters) InstallRuntimeFiles(conv);

            var output = EffectiveShaderOutput;
            ConversionOptions.FromProjectSettings();
            ConversionOptions.GrabPass = GrabPassOverride ?? KitProjectSettings.instance.GrabPass;
            ConversionOptions.KeepBuiltIn = output == ShaderOutput.KeepBuiltInSupport;
            var materials = AssetDatabase.FindAssets("t:Material", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m => m != null && m.shader != null).ToList();
            var shaderPaths = AssetDatabase.FindAssets("t:Shader", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) && !p.StartsWith("Assets/Urpify/", StringComparison.Ordinal)
                            && !PackageScopePaths.IsInside(p) && !KitPaths.IsKitFile(p))
                .OrderBy(p => p, StringComparer.Ordinal).ToList();
            var batch = ShaderBatchConverter.Run(shaderPaths, materials, output);
            var report = batch.Report;
            // Written now as well, so a failure later in the step still leaves the report of what was converted (QA-012).
            SaveKitReport(report);
            AssetDatabase.SaveAssets();
            int auto = report.Items.Count(i => i.Outcome == "Auto"), approx = report.Items.Count(i => i.Outcome == "AutoApprox");
            int manual = report.Items.Count(i => i.Outcome == "Manual" || i.Outcome == "NotSupportedYet"), restored = report.Items.Count(i => i.Outcome == "Restored");
            int review = report.Items.Count(i => i.Outcome == ShaderBatchConverter.ReviewOutcome);
            rec.Details.Add($"Output: {(output == ShaderOutput.KeepBuiltInSupport ? "Keep Built-in support" : output == ShaderOutput.WriteCopy ? "Write copy" : "Replace original")}");
            if (converters.Count > 0)
                rec.Details.Add($"{auto + approx + review} shaders converted ({auto} Auto, {approx} AutoApprox, {review} look different: review), {manual} not converted, {restored} did not compile and were restored");
            else
                rec.Details.Add("Shaders: converted by Urpify Pro. The Free package runs the fixes after Unity's converter.");
            // URP Asset settings the shaders need (TZ 8.1 step 6: URP Asset settings are part of this step).
            var settings = UrpAssetSettings.Apply(batch.Requirements);
            report.Settings = settings;
            report.Requirements = batch.Requirements.OrderBy(r => r, StringComparer.Ordinal).ToList();
            foreach (var line in settings.Take(6)) rec.Details.Add(line);
            if (settings.Count > 6) rec.Details.Add($"... and {settings.Count - 6} more settings (kit-converters.json)");

            // Materials, scenes and prefabs Unity's converter left broken (TZ 6.11).
            bool scanned = File.Exists(Path.Combine(ReportsDir, "scan-latest.internal.json"));
            var scan = LatestScanReport();
            // Without a Scan step the fresh scan is kept as one: with Built-in still active (Keep Built-in support) these
            // fixes wait, and the run after Unity's converter needs what the project had before it.
            if (!scanned) WriteReport(scan, "scan");
            // Projector components (TZ 6.10): Urpify's renderer feature draws them as Built-in does.
            if (scan.Findings.Any(f => f.RuleId == RuleIds.ProjectorComponent) && !report.Requirements.Contains(UrpRequirement.Projectors))
                report.Requirements.Add(UrpRequirement.Projectors);
            int fixes = 0;
            foreach (var conv in AssetConverters())
            {
                List<KitConverterItem> items;
                try { items = conv.Run(scan); }
                catch (Exception e) { items = new List<KitConverterItem> { new KitConverterItem { Converter = conv.Name, Outcome = "NotSupportedYet", Notes = { "Not supported yet: converter error: " + e.Message } } }; }
                report.Items.AddRange(items);
                fixes += items.Count(i => i.Outcome == "Auto" || i.Outcome == "AutoApprox");
            }
            AssetDatabase.SaveAssets();
            if (fixes > 0) rec.Details.Add($"{fixes} materials, scenes and prefabs fixed after Unity's converter");

            // Run-time scripts (TZ 6.7, 6.8, 6.11): Unity has to compile them before the step can finish.
            s_Scripts = null;
            if (!EditorUtility.scriptCompilationFailed)
            {
                var scripts = ScriptBatchConverter.Run(report);
                SaveKitReport(report);
                if (scripts.Paths.Count > 0 || scripts.RuntimeFiles.Count > 0)
                {
                    s_Scripts = scripts;
                    rec.Details.Add($"{scripts.Paths.Count} scripts rewritten, {scripts.RuntimeFiles.Count} runtime files; compiling");
                    rec.Phase = 1;
                    st.Save();
                    StartScriptCompile();
                    return;
                }
            }
            else
            {
                report.Items.Add(new KitConverterItem { Converter = "Scripts", Outcome = "Manual", Notes = { "Manual: the project's scripts do not compile, so Urpify did not change any. Fix the compiler errors and run the step again." } });
                SaveKitReport(report);
            }
            CompleteKitConverters(st, rec);
        }

        // ------------------------------------------------------------------ Urpify converters: scripts

        static ScriptBatchResult s_Scripts;
        static readonly HashSet<string> s_ErrorFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static int s_CompileRounds, s_CompileTicks;
        static bool s_CompileSeen;

        public static List<IKitAssetConverter> AssetConverters() =>
            TypeCache.GetTypesDerivedFrom<IKitAssetConverter>().Where(t => !t.IsAbstract && !t.IsInterface && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => (IKitAssetConverter)Activator.CreateInstance(t)).OrderBy(c => c.Order).ToList();

        static void SaveKitReport(KitConverterReport report)
        {
            Directory.CreateDirectory(ReportsDir);
            File.WriteAllText(Path.Combine(ReportsDir, "kit-converters.json"), JsonUtility.ToJson(report, true));
        }

        static KitConverterReport LoadKitReport()
        {
            var p = Path.Combine(ReportsDir, "kit-converters.json");
            try { return File.Exists(p) ? JsonUtility.FromJson<KitConverterReport>(File.ReadAllText(p)) ?? new KitConverterReport() : new KitConverterReport(); }
            catch { return new KitConverterReport(); }
        }

        static void StartScriptCompile()
        {
            s_ErrorFiles.Clear();
            s_CompileSeen = false;
            s_CompileTicks = 0;
            s_CompileRounds++;
            UnityEditor.Compilation.CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompiled;
            UnityEditor.Compilation.CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            UnityEditor.Compilation.CompilationPipeline.compilationFinished -= OnCompilationFinished;
            UnityEditor.Compilation.CompilationPipeline.compilationFinished += OnCompilationFinished;
            EditorApplication.update -= WaitForCompile;
            EditorApplication.update += WaitForCompile;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
        }

        static void OnAssemblyCompiled(string assembly, UnityEditor.Compilation.CompilerMessage[] messages)
        {
            s_CompileSeen = true;
            foreach (var m in messages)
                if (m.type == UnityEditor.Compilation.CompilerMessageType.Error) s_ErrorFiles.Add((m.file ?? "").Replace('\\', '/'));
        }

        /// <summary>
        /// Compilation ended in this domain. Without errors Unity now reloads the domain and the static constructor goes on
        /// with phase 2. With errors there is no reload: the scripts the errors name are restored (all of them when the
        /// errors are elsewhere) and Unity compiles again.
        /// </summary>
        static void OnCompilationFinished(object context)
        {
            s_CompileSeen = true;
            if (s_ErrorFiles.Count == 0) return;
            EditorApplication.delayCall += () => Guarded(StepId.KitConverters, RestoreFailedScripts, StopScriptCompile);
        }

        static void StopScriptCompile()
        {
            EditorApplication.update -= WaitForCompile;
            UnityEditor.Compilation.CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompiled;
            UnityEditor.Compilation.CompilationPipeline.compilationFinished -= OnCompilationFinished;
        }

        /// <summary>Safety net: a compilation that never starts (nothing to compile) must not leave the step running.</summary>
        static void WaitForCompile() => Guarded(StepId.KitConverters, WaitForCompileTick, StopScriptCompile);

        static void WaitForCompileTick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { s_CompileTicks = 0; return; }
            if (s_CompileSeen && s_ErrorFiles.Count > 0) { EditorApplication.update -= WaitForCompile; return; }
            if (++s_CompileTicks < 1200) return;
            EditorApplication.update -= WaitForCompile;
            var st = MigrationState.Load();
            if (st.Running == (int)StepId.KitConverters) CompleteKitConverters(st, st.Get(StepId.KitConverters));
        }

        static void RestoreFailedScripts()
        {
            var st = MigrationState.Load();
            var rec = st.Get(StepId.KitConverters);
            var report = LoadKitReport();
            var named = s_Scripts.Paths.Where(p => s_ErrorFiles.Contains(p)).ToList();
            // An effect and the scripts its drawing is in go back together (ScriptBatchResult.Coupled).
            var restore = named.Count > 0 ? s_Scripts.WithCoupled(named) : s_Scripts.Paths.ToList();
            var errors = string.Join(" | ", s_ErrorFiles.Take(3));
            foreach (var path in restore)
            {
                File.WriteAllBytes(path, s_Scripts.Originals[path]);
                s_Scripts.Paths.Remove(path);
                var item = report.Items.LastOrDefault(i => i.Path == path);
                if (item == null) continue;
                item.Outcome = "Restored";
                item.Notes.Add(named.Count == 0 || named.Contains(path)
                    ? "Not supported yet: the rewritten script did not compile, the original is back. Errors in: " + errors
                    : "Not supported yet: restored with " + string.Join(", ", named.Select(Path.GetFileName)) + ", whose rewrite did not compile: the effect works only with both. Errors in: " + errors);
            }
            if (named.Count == 0)
                foreach (var f in s_Scripts.RuntimeFiles) { AssetDatabase.DeleteAsset(f); }
            SaveKitReport(report);
            rec.Details.Add($"{restore.Count} scripts did not compile after the rewrite and were restored");
            st.Save();
            if (s_CompileRounds < 3 && s_Scripts.Paths.Count > 0) { StartScriptCompile(); return; }
            // Nothing of ours is left to compile, or three rounds were not enough: compile the originals and finish there.
            if (s_Scripts.Paths.Count > 0)
                foreach (var path in s_Scripts.Paths.ToList()) { File.WriteAllBytes(path, s_Scripts.Originals[path]); s_Scripts.Paths.Remove(path); }
            StartScriptCompile();
        }

        /// <summary>Phase 2, after the scripts compiled (a new domain): renderer features, backup, report, done.</summary>
        static void CompleteKitConverters(MigrationState st, StepRecord rec)
        {
            StopScriptCompile();
            var report = LoadKitReport();
            foreach (var conv in ScriptBatchConverter.Converters())
                foreach (var feature in conv.RendererFeatures())
                {
                    var type = RendererFeatureInstaller.FindType(feature.Key);
                    if (type == null) continue; // not installed, or the scripts that needed it were restored
                    var changes = RendererFeatureInstaller.AddToAllRenderers(type, feature.Value);
                    foreach (var instance in RendererFeatureInstaller.Instances(type))
                        changes.AddRange(conv.ConfigureRendererFeature(instance, report.Requirements));
                    AssetDatabase.SaveAssets();
                    report.Settings.AddRange(changes);
                    rec.Details.AddRange(changes.Take(4));
                }
            var scripts = report.Items.Where(i => ScriptBatchConverter.IsScript(i.Path)).ToList();
            if (scripts.Count > 0)
                rec.Details.Add($"{scripts.Count(i => i.Outcome == "Auto" || i.Outcome == "AutoApprox")} scripts converted, {scripts.Count(i => i.Outcome == "Manual" || i.Outcome == "NotSupportedYet")} not converted, {scripts.Count(i => i.Outcome == "Restored")} restored");
            SaveKitReport(report);
            st.Save();
            AssetDatabase.SaveAssets();
            AfterPendingImports(StepId.KitConverters, () =>
            {
                var st2 = MigrationState.Load();
                var rec2 = st2.Get(StepId.KitConverters);
                var store = new BackupStore(".");
                var snap = store.Load(rec2.SnapshotId);
                if (snap != null)
                {
                    store.Complete(snap);
                    foreach (var k in KeepTextureImportSettings(store, snap, rec2))
                        report.Items.Add(new KitConverterItem { Converter = "Texture import settings", Path = k.Texture, Outcome = "Info", Notes = { k.Note } });
                    SaveKitReport(report);
                    rec2.Details.Add($"{snap.Changed.Count} files changed, {snap.Created.Count} created");
                }
                SetKitResult(rec2, report);
                Finish(st2, rec2, StepStatus.Done, "");
            });
        }

        /// <summary>
        /// Runs <paramref name="then"/> once the Editor has been through three updates with nothing compiling or importing.
        /// Unity 2022.3 switches a texture to Normal map one update after a material that uses it in a [Normal] slot was saved
        /// (QA-035, 2022.3.62f3: still Default right after AssetDatabase.SaveAssets, Normal map on the next update), so the
        /// converter steps complete their backup and put import settings back (<see cref="TextureImportGuard"/>) only after
        /// that; a command-line step that quit in the same update left the change to whichever Editor ran next.
        /// </summary>
        static void AfterPendingImports(StepId id, Action then)
        {
            int quiet = 0;
            void Tick()
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) { quiet = 0; return; }
                if (++quiet < 3) return;
                EditorApplication.update -= Tick;
                Guarded(id, then);
            }
            EditorApplication.update += Tick;
        }

        /// <summary>
        /// The Urpify converters step in plain words (design review 13.10.2026, item 8): with Pro, what was converted and how much
        /// is left by hand; without it, what the free fixes did and how many shaders and scripts need Pro.
        /// </summary>
        static void SetKitResult(StepRecord rec, KitConverterReport report)
        {
            bool Converted(KitConverterItem i) => i.Outcome == "Auto" || i.Outcome == "AutoApprox" || i.Outcome == ShaderBatchConverter.ReviewOutcome;
            bool Left(KitConverterItem i) => i.Outcome == "Manual" || i.Outcome == "NotSupportedYet" || i.Outcome == "Restored";
            bool IsShader(KitConverterItem i) => i.Path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase);
            var items = report.Items.Where(i => i.Path.Length > 0).GroupBy(i => i.Path).Select(g => g.Last()).ToList();
            int shaders = items.Count(i => IsShader(i) && Converted(i)), scripts = items.Count(i => ScriptBatchConverter.IsScript(i.Path) && Converted(i));
            int fixes = items.Count(i => !IsShader(i) && !ScriptBatchConverter.IsScript(i.Path) && (i.Outcome == "Auto" || i.Outcome == "AutoApprox"));
            if (ShaderConverters().Count > 0)
            {
                int left = items.Count(i => (IsShader(i) || ScriptBatchConverter.IsScript(i.Path)) && Left(i));
                if (left == 0) rec.SetResult("step.KitConverters.result", shaders, scripts, fixes);
                else rec.SetResult("step.KitConverters.resultLeft", shaders, scripts, fixes, left);
                return;
            }
            // The Free package: shaders and scripts that break and that only Urpify Pro converts.
            int pro = 0;
            try
            {
                var scan = LatestScanReport();
                pro = scan.Assets.Count(a => (a.Kind == AssetKind.Shader || a.Kind == AssetKind.Script) && a.Severity > Severity.Info
                                             && Edition.Owner(scan, a) == FixOwner.Pro);
            }
            catch (Exception e) { Debug.LogWarning("[Urpify] Could not count what needs Urpify Pro: " + e.Message); }
            if (pro == 0) rec.SetResult("step.KitConverters.resultFree", fixes);
            else rec.SetResult("step.KitConverters.resultFreePro", fixes, pro);
        }

        /// <summary>Copies the include files a converter's output needs into the project and imports them.</summary>
        public static void InstallRuntimeFiles(IShaderFileConverter conv)
        {
            foreach (var file in conv.RuntimeFiles()) InstallRuntimeFile(file.Key, file.Value, import: true);
        }

        /// <summary>
        /// Copies one file from a package into the project with a stable GUID, so the file has the same one in every project
        /// and every exported .unitypackage. Scripts are left for the next compilation to import (<paramref name="import"/> false).
        /// </summary>
        public static void InstallRuntimeFile(string packagePath, string projectPath, bool import)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(projectPath));
            File.Copy(Path.GetFullPath(packagePath), projectPath, true);
            var meta = projectPath + ".meta";
            if (!File.Exists(meta))
                File.WriteAllText(meta, Packaging.AssetGuids.DefaultMeta(Packaging.AssetGuids.Stable(projectPath), Packaging.AssetGuids.ImporterFor(projectPath)));
            if (import) AssetDatabase.ImportAsset(projectPath, ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>Keyword sets to compile a shader with: none, the common lighting keywords, and each project material's.</summary>
        public static List<string[]> KeywordSets(Shader shader) => ShaderBatchConverter.KeywordSets(shader);

        static void RunReview(MigrationState st, StepRecord rec)
        {
            // Last chance to attribute Unity's own follow-up rewrites to the migration; afterwards changes are the user's.
            SettleIfInProgress(st);
            // URP 14 re-imports every material of the project once URP is the active pipeline and the Editor has drawn a frame
            // (MaterialReimporter, URP 14.0.12 MaterialPostprocessor.cs): in the Free package that is Capture AFTER, after the
            // converter steps already put the import settings back. What it switched is in a backup now (Settle): put back too.
            var store = new BackupStore(".");
            foreach (var snap in st.Steps.Where(s => MigrationState.IsModifying(s.Id)).SelectMany(MigrationState.Snapshots)
                         .OrderByDescending(id => id, StringComparer.Ordinal).Select(store.Load).Where(s => s != null))
                KeepTextureImportSettings(store, snap, rec);
            var report = LatestScanReport();
            // What Urpify converters did, over what the scan expected (one tier for the report, the list and the panels).
            var kit = KitOutcomes.Load();
            KitOutcomes.Apply(report, kit);
            report.UrpVersion = UrpInstaller.LoadedVersion() ?? report.UrpVersion;
            if (CaptureManifest.Load(BeforeLabel) != null && CaptureManifest.Load(AfterLabel) != null)
                KitOutcomes.MarkReview(kit, VerifyRunner.Apply(report, VerifyRunner.Compare(BeforeLabel, AfterLabel, Thresholds, true, VerifyRunner.KnownBrokenShaders(report)), BeforeLabel, AfterLabel, kit != null));
            rec.Details.AddRange(WriteReport(report, "review"));
            var s = report.Summary;
            rec.Details.Add($"Fix: {s.TierUnityConverter} Unity converter, {s.TierAuto} auto, {s.TierAutoApprox} approximate, {s.TierReview} to review, {s.TierNotSupportedYet} not supported yet, {s.TierManual} manual");
            SetReviewResult(rec, report);
            Finish(st, rec, StepStatus.Done, "");
        }

        /// <summary>
        /// The Review step in plain words, per asset that breaks: fixed by the migration, to look at, left by hand, and, in the
        /// Free package, what only Urpify Pro fixes (the scan's Auto for those is not done without Pro).
        /// </summary>
        static void SetReviewResult(StepRecord rec, MigrationReport report)
        {
            int fixedCount = 0, check = 0, left = 0, pro = 0;
            foreach (var a in report.Assets.Where(a => a.Severity > Severity.Info))
            {
                var owner = Edition.Owner(report, a);
                if (owner == FixOwner.Pro && !Edition.ProInstalled) pro++;
                else if (a.Tier == FixTier.Review) check++;
                else if (a.Tier == FixTier.UnityConverter || a.Tier == FixTier.Auto || a.Tier == FixTier.AutoApprox) fixedCount++;
                else if (a.Tier == FixTier.NotSupportedYet || a.Tier == FixTier.Manual) left++;
            }
            if (pro == 0) rec.SetResult("step.Review.result", fixedCount, check, left);
            else rec.SetResult("step.Review.resultFree", fixedCount, check, left, pro);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Runs a continuation of step <paramref name="id"/> that Unity calls later (an update tick, a delayCall, the end of a
        /// capture). An exception in it fails the step (code review R-04): before, the step stayed Running until the Editor
        /// restarted, a rollback was refused, and a command-line step never quit. <paramref name="stop"/> unsubscribes the
        /// continuation first, so it is not called again.
        /// </summary>
        public static void Guarded(StepId id, Action continuation, Action stop = null, string root = ".")
        {
            try { continuation(); }
            catch (Exception e)
            {
                try { stop?.Invoke(); } catch (Exception e2) { Debug.LogException(e2); }
                Debug.LogException(e);
                var st = MigrationState.Load(root);
                if (st.Running != (int)id) return;
                Finish(st, st.Get(id), StepStatus.Failed, e.GetType().Name + ": " + e.Message, root);
            }
        }

        /// <summary>
        /// The texture import settings the step changed go back as they were (QA-035, <see cref="TextureImportGuard"/>), with a
        /// line in the step's details. Never stops the step.
        /// </summary>
        static List<TextureImportGuard.Kept> KeepTextureImportSettings(BackupStore store, Snapshot snap, StepRecord rec, bool import = true)
        {
            List<TextureImportGuard.Kept> kept;
            try { kept = TextureImportGuard.Restore(store, snap, import); }
            catch (Exception e) { Debug.LogWarning("[Urpify] Could not check the texture import settings after the step: " + e.Message); return new List<TextureImportGuard.Kept>(); }
            if (kept.Count > 0)
                rec.Details.Add($"Import settings of {kept.Count} textures put back as they were (Unity changed them while the step saved materials): "
                                + string.Join(", ", kept.Take(3).Select(k => k.Texture)) + (kept.Count > 3 ? ", ..." : ""));
            return kept;
        }

        /// <summary>Folds Unity's follow-up rewrites into the backups while the migration is not finished (Review not done).</summary>
        static void SettleIfInProgress(MigrationState st, string root = ".")
        {
            if (st.Get(StepId.Review).Status == StepStatus.Done) return;
            var store = new BackupStore(root);
            // Every backup a rollback would undo, in the order the runs happened (their ids start with the time).
            var snaps = st.Steps.Where(s => MigrationState.HasRollback(s.Id)).SelectMany(MigrationState.Snapshots)
                .OrderBy(id => id, StringComparer.Ordinal).Select(store.Load).Where(s => s != null).ToList();
            store.Settle(snaps);
        }

        /// <summary>The report of the Scan step (pre-migration static verdicts), or a fresh scan when there is none.</summary>
        public static MigrationReport LatestScanReport()
        {
            var p = Path.Combine(ReportsDir, "scan-latest.internal.json");
            if (File.Exists(p))
            {
                var r = JsonUtility.FromJson<MigrationReport>(File.ReadAllText(p));
                if (r != null && r.SchemaVersion == MigrationReport.CurrentSchemaVersion) return r;
            }
            var scanner = new ProjectScanner();
            scanner.RunToEnd(new ScanOptions());
            return scanner.Report;
        }

        /// <summary>Writes JSON and HTML with a stable "-latest" name; the internal copy keeps the in-memory model.</summary>
        public static List<string> WriteReport(MigrationReport report, string kind)
        {
            Directory.CreateDirectory(ReportsDir);
            var json = Path.Combine(ReportsDir, $"urp-migration-{kind}-latest.json");
            var html = Path.Combine(ReportsDir, $"urp-migration-{kind}-latest.html");
            File.WriteAllText(json, ReportJson.Write(report));
            File.WriteAllText(html, ReportHtml.Write(report));
            File.WriteAllText(Path.Combine(ReportsDir, kind + "-latest.internal.json"), JsonUtility.ToJson(report));
            return new List<string> { json.Replace('\\', '/'), html.Replace('\\', '/') };
        }

        static void Finish(MigrationState st, StepRecord rec, StepStatus status, string message, string root = ".")
        {
            rec.Status = status;
            rec.Message = message;
            rec.FinishedUtc = Now();
            rec.Phase = 0;
            st.Running = -1;
            bool exit = st.ExitEditorWhenDone;
            bool here = root == ".";
            if (here) { SaveDirtySettings(); StepProgress.Clear(); }
            // A step that stopped half way (failed) keeps what it changed until a rollback: its backup records that (QA-001).
            if (rec.SnapshotId.Length > 0)
            {
                try
                {
                    var store = new BackupStore(root);
                    if (!store.CompleteIfOpen(rec.SnapshotId)) rec.SnapshotId = "";
                    // A step that stopped half way puts the texture import settings back too (done steps already did, QA-035).
                    else if (status != StepStatus.Done && MigrationState.HasRollback(rec.Id)) KeepTextureImportSettings(store, store.Load(rec.SnapshotId), rec, here);
                }
                catch (Exception e) { Debug.LogWarning("[Urpify] Could not close the step's backup: " + e.Message); }
            }
            if (exit) st.PendingWindowEnd = rec.Id.ToString();
            else
            {
                try { new BackupStore(root).RecordStep(rec.Id.ToString(), false); }
                catch (Exception e) { Debug.LogWarning("[Urpify] Could not record the file list after the step: " + e.Message); }
            }
            bool ok = status == StepStatus.Done || status == StepStatus.Skipped;
            var chainKind = st.ChainKind;
            StepId? chain = NextInChain(st, ok);
            bool chainEnded = chainKind.Length > 0 && chain == null;
            if (chainEnded) st.ChainKind = "";
            st.Save(root);
            Debug.Log($"[Urpify] Step {rec.Id}: {status}{(message.Length > 0 ? " - " + message : "")}\n{string.Join("\n", rec.Details)}");
            if (!here) return;
            try { StepFinished?.Invoke(rec); }
            catch (Exception e) { Debug.LogException(e); }
            if (chainEnded)
                try { ChainFinished?.Invoke(chainKind, ok); }
                catch (Exception e) { Debug.LogException(e); }
            if (chain != null && !exit)
            {
                var next = chain.Value;
                EditorApplication.delayCall += () =>
                {
                    var error = Run(next);
                    if (error == null) return;
                    // The next step could not start (blocked, Play Mode): the chain ends here and says why.
                    Debug.LogWarning($"[Urpify] {next} did not start: {error}");
                    var st2 = MigrationState.Load();
                    var kind = st2.ChainKind;
                    st2.Chain.Clear(); st2.RunAll = false; st2.ChainKind = ""; st2.Save();
                    if (kind.Length > 0)
                        try { ChainFinished?.Invoke(kind, false); } catch (Exception e) { Debug.LogException(e); }
                };
            }
            if (exit) ExitWithStepResult(rec, status, message);
        }

        /// <summary>The command-line step's result files (text and JSON), then the Editor quits with the step's exit code.</summary>
        static void ExitWithStepResult(StepRecord rec, StepStatus status, string message)
        {
            bool ok = status == StepStatus.Done || status == StepStatus.Skipped;
            File.WriteAllText(Path.Combine("Urpify", "urpify-step-result.txt"), $"{rec.Id} {status}");
            var result = new CommandResult { command = "Migrate", step = rec.Id.ToString(), status = status.ToString(), exitCode = ok ? 0 : 1,
                error = ok ? "" : message };
            result.reports.AddRange(rec.Details.Where(d => d.StartsWith(ReportsDir, StringComparison.Ordinal)));
            result.Write(ReportsDir, "urpify-step-result.json");
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// Writes project settings a step left modified (URP registers its global settings in GraphicsSettings), so the
        /// write happens inside the step's file-list window rather than whenever the Editor quits.
        /// </summary>
        static void SaveDirtySettings()
        {
            try
            {
                foreach (var f in Directory.GetFiles("ProjectSettings", "*.asset"))
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(f.Replace('\\', '/')))
                        if (o != null && EditorUtility.IsDirty(o)) AssetDatabase.SaveAssetIfDirty(o);
            }
            catch (Exception e) { Debug.LogWarning("[Urpify] Could not save project settings after the step: " + e.Message); }
        }

        static string Now() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
}
