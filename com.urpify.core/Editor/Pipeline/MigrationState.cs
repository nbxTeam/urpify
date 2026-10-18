using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Urpify.Pipeline
{
    /// <summary>
    /// Steps of TZ 8.1, in order. Backup comes before Capture BEFORE: playing the scenes lets Unity re-save old assets and
    /// lets packages create files, and the backup must hold the project as it was (QA-005). State files of version 1 had
    /// the two the other way round (<see cref="MigrationState.Load"/> swaps them).
    /// </summary>
    public enum StepId { Scan = 0, Backup = 1, CaptureBefore = 2, InstallUrp = 3, UnityConverter = 4, KitConverters = 5, CaptureAfter = 6, Review = 7 }

    public enum StepStatus { NotRun, Running, Done, Failed, Skipped, RolledBack, Interrupted }

    [Serializable]
    public sealed class StepRecord
    {
        public StepId Id;
        public StepStatus Status;
        public string StartedUtc = "", FinishedUtc = "";
        /// <summary>Backup taken before the step changed files; empty for read-only steps.</summary>
        public string SnapshotId = "";
        /// <summary>
        /// Backups of earlier runs of this step (interrupted, failed or done before the step ran again), oldest first. Their
        /// changes stay in the project until a rollback undoes them, after the current run's (QA-001).
        /// </summary>
        public List<string> EarlierSnapshots = new List<string>();
        public string Message = "";
        /// <summary>Step-specific result lines, e.g. converter counts.</summary>
        public List<string> Details = new List<string>();
        /// <summary>
        /// The step's result in plain words: a localization key and its arguments, so the window shows it in its own language
        /// whatever language the step ran in (design review 13.10.2026, item 8). The technical lines stay in Details.
        /// </summary>
        public string ResultKey = "";
        public List<string> ResultArgs = new List<string>();
        /// <summary>Internal sub-phase for steps that span domain reloads (Install URP).</summary>
        public int Phase;

        public void SetResult(string key, params object[] args)
        {
            ResultKey = key ?? "";
            ResultArgs = (args ?? new object[0]).Select(a => Convert.ToString(a, CultureInfo.InvariantCulture) ?? "").ToList();
        }

        /// <summary>The result line in the current language; "" when the step has none.</summary>
        public string ResultText => ResultKey.Length == 0 ? "" : Loc.Get(ResultKey, ResultArgs.Cast<object>().ToArray());
    }

    /// <summary>
    /// A change the user asks for outside the steps (Bake environment in the scene panel), with its own backup. Rollback
    /// undoes these first, newest first, then the steps.
    /// </summary>
    [Serializable]
    public sealed class ExtraRecord
    {
        public string Kind = "";
        /// <summary>The asset it was done for (a scene path).</summary>
        public string Target = "";
        public StepStatus Status;
        public string StartedUtc = "", FinishedUtc = "";
        public string SnapshotId = "";
        public string Message = "";
    }

    [Serializable]
    public sealed class KitConverterItem
    {
        public string Path = "";
        public string Converter = "";
        /// <summary>Auto, AutoApprox, Manual (not converted) or Restored (converted but did not compile).</summary>
        public string Outcome = "";
        public List<string> Notes = new List<string>();
    }

    [Serializable]
    public sealed class KitConverterReport
    {
        public List<KitConverterItem> Items = new List<KitConverterItem>();
        /// <summary>The Output the shaders were written with (ShaderOutput name).</summary>
        public string Output = "";
        /// <summary>URP Asset and renderer settings changed for the converted shaders.</summary>
        public List<string> Settings = new List<string>();
        /// <summary>URP settings the converted shaders need (UrpRequirement names), kept for the step's second phase.</summary>
        public List<string> Requirements = new List<string>();
    }

    /// <summary>&lt;Project&gt;/Urpify/state.json (outside Assets). Every step is idempotent and runs on its own.</summary>
    [Serializable]
    public sealed class MigrationState
    {
        public const string PathName = "Urpify/state.json";
        /// <summary>2: Backup before Capture BEFORE. 0 or 1: files written before that change.</summary>
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;
        public List<StepRecord> Steps = new List<StepRecord>();
        public List<ExtraRecord> Extras = new List<ExtraRecord>();
        /// <summary>Step currently running, -1 when idle.</summary>
        public int Running = -1;
        public bool ExitEditorWhenDone;
        /// <summary>Step whose file-list window closes at the next domain load (CLI: the Editor exits right after the step).</summary>
        public string PendingWindowEnd = "";
        /// <summary>
        /// Step closed as interrupted whose file-list window is still open: what Unity writes while it starts after the stop
        /// (URP creating its settings because the stopped step installed it) belongs to that step. It closes when the Editor
        /// is idle, or before a rollback or the next step, whichever comes first.
        /// </summary>
        public string InterruptedWindow = "";
        /// <summary><see cref="EditorSession.Id"/> of the Editor running the step in <see cref="Running"/>.</summary>
        public string RunningSession = "";
        /// <summary>Run all: after a step succeeds the next one starts, across domain reloads.</summary>
        public bool RunAll;
        /// <summary>
        /// Show what breaks / Fix with Urpify: the steps still to run after the current one, in order (the first of the chain is
        /// running). Like <see cref="RunAll"/>, it outlives domain reloads; a step that fails ends it.
        /// </summary>
        public List<StepId> Chain = new List<StepId>();
        /// <summary><see cref="MigrationRunner.ChainShowBreaks"/> or <see cref="MigrationRunner.ChainFix"/> while one runs, else empty.</summary>
        public string ChainKind = "";
        public string UpdatedUtc = "";

        public static bool IsModifying(StepId id) => id == StepId.InstallUrp || id == StepId.UnityConverter || id == StepId.KitConverters;

        /// <summary>
        /// Steps whose backup a rollback undoes: the modifying ones, and Backup, whose snapshot also takes what Unity
        /// re-saved and packages created while Capture BEFORE played the scenes.
        /// </summary>
        public static bool HasRollback(StepId id) => id == StepId.Backup || IsModifying(id);

        /// <summary>Every backup of this step a rollback would undo (earlier runs and the current one), oldest first.</summary>
        public static IEnumerable<string> Snapshots(StepRecord rec)
        {
            if (rec.Status == StepStatus.NotRun || rec.Status == StepStatus.RolledBack) yield break;
            foreach (var id in rec.EarlierSnapshots) if (id.Length > 0) yield return id;
            if (rec.SnapshotId.Length > 0) yield return rec.SnapshotId;
        }

        public StepRecord Get(StepId id)
        {
            var r = Steps.FirstOrDefault(s => s.Id == id);
            if (r == null) { r = new StepRecord { Id = id }; Steps.Add(r); Steps.Sort((a, b) => a.Id.CompareTo(b.Id)); }
            return r;
        }

        public static MigrationState Load(string root = ".")
        {
            var p = Path.Combine(root, PathName);
            MigrationState s = null;
            if (File.Exists(p))
            {
                try
                {
                    var text = File.ReadAllText(p);
                    s = JsonUtility.FromJson<MigrationState>(text);
                    // A file without "Version" was written before it existed (version 1): its step numbers 1 and 2 are swapped.
                    if (s != null && !text.Contains("\"Version\"")) s.Version = 1;
                }
                catch { s = null; }
            }
            s = s ?? new MigrationState();
            if (s.Version < 2)
            {
                StepId Swap(StepId id) => id == StepId.Backup ? StepId.CaptureBefore : id == StepId.CaptureBefore ? StepId.Backup : id;
                foreach (var r in s.Steps) r.Id = Swap(r.Id);
                if (s.Running >= 0) s.Running = (int)Swap((StepId)s.Running);
                s.Steps.Sort((a, b) => a.Id.CompareTo(b.Id));
                s.Version = CurrentVersion;
            }
            foreach (StepId id in Enum.GetValues(typeof(StepId))) s.Get(id);
            return s;
        }

        public void Save(string root = ".")
        {
            UpdatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var p = Path.Combine(root, PathName);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            BackupStore.WriteAtomically(p, JsonUtility.ToJson(this, true));
        }
    }
}
