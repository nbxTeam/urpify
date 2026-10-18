using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Urpify.Pipeline
{
    /// <summary>
    /// Whether a running step is alive, by its real progress (QA-008): what the step itself reports (a shader converted,
    /// a frame captured, a phase reached) and files it writes in the project. Lines in Unity's log do not count: Unity
    /// writes "Curl error" every 30 s while a step is stuck. Urpify/progress.json holds the last report, for the
    /// window, scripts and CI.
    ///
    /// In batch mode a watchdog thread stops the Editor when the step makes no progress for
    /// <see cref="TimeoutMinutes"/> (command line: -urpifyStepTimeout minutes, 0 = never). The step is then interrupted like a
    /// killed Editor: the next session closes it with the reason, and a rollback undoes what it changed. An open Editor
    /// is never stopped: the user sees it and decides.
    /// </summary>
    public static class StepProgress
    {
        public const string PathName = "Urpify/progress.json";
        public const string StoppedMarker = "Stopped by the watchdog";

        /// <summary>Minutes without progress before the watchdog stops a batch-mode Editor.</summary>
        public static double TimeoutMinutes = 20;

        [Serializable]
        public sealed class Data
        {
            public string Step = "";
            public string What = "";
            public int Done;
            public int Total;
            public string UpdatedUtc = "";
            /// <summary>Set by the watchdog when it stops the Editor; the next session reports it.</summary>
            public string Stopped = "";
        }

        static long s_LastTicks;
        static string s_Step = "";
        static Thread s_Thread;
        static volatile bool s_Stop;
        static DateTime s_LastFileWrite;
        static string s_Root;

        /// <summary>The step made progress: <paramref name="what"/> (a file, a scene, a phase), <paramref name="done"/> of <paramref name="total"/>.</summary>
        public static void Report(string what, int done = 0, int total = 0)
        {
            if (s_Step.Length == 0) return; // no step is running (a capture or a conversion on its own)
            var now = DateTime.UtcNow;
            var last = new DateTime(Interlocked.Read(ref s_LastTicks), DateTimeKind.Utc);
            Interlocked.Exchange(ref s_LastTicks, now.Ticks);
            // The file is for people and scripts; once every two seconds is enough.
            if ((now - last).TotalSeconds < 2 && done != total) return;
            Write(new Data { Step = s_Step, What = what ?? "", Done = done, Total = total, UpdatedUtc = now.ToString("yyyy-MM-ddTHH:mm:ssZ") });
        }

        /// <summary>A step starts (or resumes after a domain reload): progress counts from now, the watchdog runs in batch mode.</summary>
        public static void Begin(string step)
        {
            s_Step = step;
            s_Root = Path.GetFullPath(".");
            Interlocked.Exchange(ref s_LastTicks, DateTime.UtcNow.Ticks);
            Report("started");
            // Read here, not once: a domain reload in the middle of a step resets static fields.
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-urpifyStepTimeout");
            if (i >= 0 && i + 1 < args.Length && double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var minutes))
                TimeoutMinutes = minutes;
            if (!Application.isBatchMode || TimeoutMinutes <= 0) return;
            s_Stop = false;
            if (s_Thread != null && s_Thread.IsAlive) return;
            s_LastFileWrite = LatestWrite();
            s_Thread = new Thread(Watch) { IsBackground = true, Name = "Urpify step watchdog" };
            s_Thread.Start();
        }

        /// <summary>The step ended: no watchdog, no progress file.</summary>
        public static void Clear()
        {
            s_Stop = true;
            s_Step = "";
            try { if (File.Exists(PathName)) File.Delete(PathName); } catch { }
        }

        public static Data Read(string root = ".")
        {
            var p = Path.Combine(root, PathName);
            try { return File.Exists(p) ? JsonUtility.FromJson<Data>(File.ReadAllText(p)) : null; }
            catch { return null; }
        }

        static void Write(Data d)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PathName));
                BackupStore.WriteAtomically(Path.Combine(s_Root ?? ".", PathName), JsonUtility.ToJson(d, true));
            }
            catch { }
        }

        /// <summary>Newest write time of the project's own files; a step that converts writes them.</summary>
        static DateTime LatestWrite()
        {
            var newest = DateTime.MinValue;
            foreach (var folder in BackupStore.ProjectFolders)
            {
                var abs = Path.Combine(s_Root, folder);
                if (!Directory.Exists(abs)) continue;
                try
                {
                    foreach (var f in Directory.EnumerateFiles(abs, "*", SearchOption.AllDirectories))
                    {
                        var t = File.GetLastWriteTimeUtc(f);
                        if (t > newest) newest = t;
                    }
                }
                catch { }
            }
            return newest;
        }

        static void Watch()
        {
            while (!s_Stop)
            {
                for (int i = 0; i < 30 && !s_Stop; i++) Thread.Sleep(1000);
                if (s_Stop) return;
                var write = LatestWrite();
                if (write > s_LastFileWrite)
                {
                    s_LastFileWrite = write;
                    var t = Interlocked.Read(ref s_LastTicks);
                    if (write.Ticks > t) Interlocked.Exchange(ref s_LastTicks, Math.Min(write.Ticks, DateTime.UtcNow.Ticks));
                }
                var idle = DateTime.UtcNow - new DateTime(Interlocked.Read(ref s_LastTicks), DateTimeKind.Utc);
                if (idle.TotalMinutes < TimeoutMinutes || s_Stop) continue;
                var last = Read(s_Root) ?? new Data { Step = s_Step };
                last.Stopped = $"{StoppedMarker}: no progress for {TimeoutMinutes:0} minutes (last: {(last.What.Length > 0 ? last.What : "nothing")}{(last.Total > 0 ? $", {last.Done} of {last.Total}" : "")}).";
                Write(last);
                try
                {
                    Directory.CreateDirectory(Path.Combine(s_Root, "Urpify"));
                    File.WriteAllText(Path.Combine(s_Root, "Urpify", "urpify-step-result.txt"), $"{s_Step} TimedOut");
                }
                catch { }
                // The main thread is stuck, so nothing can end the Editor in order. Stopping it is what a user would do; the
                // next session closes the step as interrupted and its changes can be rolled back.
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                return;
            }
        }

        /// <summary>The reason the watchdog left in progress.json, or null; read when the next session closes the step.</summary>
        public static string StoppedReason(string root = ".")
        {
            var d = Read(root);
            return d != null && d.Stopped.Length > 0 ? d.Stopped : null;
        }
    }
}
