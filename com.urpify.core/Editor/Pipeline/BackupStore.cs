using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Urpify.Pipeline
{
    [Serializable]
    public sealed class FileHash { public string Path = ""; public string Hash = ""; }

    [Serializable]
    public sealed class FileChange { public string Path = ""; public string Before = ""; public string After = ""; }

    /// <summary>One backup: the state of project files before a step, and what the step changed.</summary>
    [Serializable]
    public sealed class Snapshot
    {
        public string Id = "";
        public string Step = "";
        public string CreatedUtc = "";
        public string CompletedUtc = "";
        /// <summary>Files copied into the backup, with their hash before the step.</summary>
        public List<FileHash> Files = new List<FileHash>();
        /// <summary>Every file that existed before the step (copied or not), to detect created files.</summary>
        public List<string> Existing = new List<string>();
        public List<FileChange> Changed = new List<FileChange>();
        public List<FileHash> Created = new List<FileHash>();
        public List<string> Deleted = new List<string>();
        /// <summary>Files that appeared or changed between steps (possibly the user's): never rolled back, only listed.</summary>
        public List<string> Between = new List<string>();
        public bool Complete;
    }

    /// <summary>The drive cannot hold a step's backup with room to spare (code review R-09). Its message is for the user.</summary>
    public sealed class NotEnoughDiskSpaceException : IOException
    {
        public readonly long Needed, Free;
        public NotEnoughDiskSpaceException(long needed, long free, string drive)
            : base($"Not enough free disk space for the backup: the step needs about {Size(needed)} on {drive} and {Size(free)} is free. Free up space and run the step again.")
        { Needed = needed; Free = free; }

        public static string Size(long bytes) => bytes >= 1024L * 1024 * 1024
            ? (bytes / (1024.0 * 1024 * 1024)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " GB"
            : (bytes / (1024.0 * 1024)).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " MB";
    }

    [Serializable]
    public sealed class RollbackResult
    {
        public List<string> Restored = new List<string>();
        public List<string> Removed = new List<string>();
        /// <summary>Files changed by the user after the step; left as they are.</summary>
        public List<string> Skipped = new List<string>();
        /// <summary>Files that appeared or changed between steps, not while a step ran; left as they are.</summary>
        public List<string> Between = new List<string>();
        public List<string> Errors = new List<string>();
        /// <summary>
        /// Files the rollback overwrote, each kept as it was before in the backup's "replaced" folder (code review R-16): a
        /// change made in the Editor to a file a step touched counts as the step's (QA-005), so it is never dropped unsaved.
        /// </summary>
        public List<string> Kept = new List<string>();
    }

    /// <summary>
    /// Backups in &lt;Project&gt;/Urpify/Backup/&lt;id&gt;/ (TZ section 9). Works on plain files relative to a project root,
    /// so it runs without the AssetDatabase and in tests.
    /// </summary>
    public sealed class BackupStore
    {
        /// <summary>Folders whose files belong to the project. Library, Temp, Logs, UserSettings and Urpify are never touched.</summary>
        public static readonly string[] ProjectFolders = { "Assets", "ProjectSettings", "Packages" };

        /// <summary>
        /// Content that converters never rewrite (images, audio, video, fonts, native code, archives). Their .meta files are
        /// still backed up because importers settings live there.
        /// </summary>
        static readonly HashSet<string> SkipContent = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".exr", ".hdr", ".bmp", ".gif", ".iff", ".pict",
            ".fbx", ".obj", ".blend", ".max", ".ma", ".mb", ".dae", ".3ds", ".dxf",
            ".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac", ".mod", ".it", ".s3m", ".xm",
            ".mp4", ".mov", ".webm", ".avi", ".asf", ".mpg", ".mpeg", ".m4v", ".ogv",
            ".ttf", ".otf", ".dll", ".so", ".dylib", ".bundle", ".a", ".lib", ".zip", ".7z", ".rar", ".unitypackage",
        };

        readonly string root;
        public string BackupRoot => Path.Combine(root, "Urpify", "Backup");

        public BackupStore(string projectRoot) { root = Path.GetFullPath(projectRoot); }

        static int s_Hashed, s_Listed;

        public static string Hash(string file)
        {
            // A large project hashes for minutes: the command line's watchdog must see that the step is alive (R-10).
            if (++s_Hashed % 200 == 0) StepProgress.Report("backup: hashing project files", s_Hashed);
            using (var sha = SHA1.Create())
            using (var s = File.OpenRead(file))
            {
                var h = sha.ComputeHash(s);
                var sb = new StringBuilder(40);
                foreach (var b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Every file of the project folders, project-relative with forward slashes.</summary>
        public IEnumerable<string> ListProjectFiles()
        {
            foreach (var folder in ProjectFolders)
            {
                var abs = Path.Combine(root, folder);
                if (!Directory.Exists(abs)) continue;
                foreach (var f in Directory.EnumerateFiles(abs, "*", SearchOption.AllDirectories))
                {
                    if (++s_Listed % 2000 == 0) StepProgress.Report("backup: listing project files", s_Listed);
                    var rel = Rel(f);
                    if (!LeftToPackageManager(rel)) yield return rel;
                }
            }
        }

        /// <summary>
        /// The project's package list, which the Asset Store build never backs up or rolls back (Submission Guidelines
        /// 2.5.1.e: an offering must not add, update or remove packages): URP stays installed after a rollback there.
        /// </summary>
        public static bool LeftToPackageManager(string rel) =>
            BuildInfo.AssetStore && (rel == "Packages/manifest.json" || rel == "Packages/packages-lock.json");

        public bool ShouldCopy(string rel) => !SkipContent.Contains(Path.GetExtension(rel));

        string Rel(string abs) => abs.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
        string Abs(string rel) => Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
        /// <summary>The absolute path of a project-relative file.</summary>
        public string AbsolutePath(string rel) => Abs(rel);
        string SnapshotDir(string id) => Path.Combine(BackupRoot, id);
        string ManifestPath(string id) => Path.Combine(SnapshotDir(id), "manifest.json");
        /// <summary>
        /// Copies live under a short name (hash of the path) so a deep project path plus the backup prefix never exceeds
        /// the Windows path limit (ShaderTutorials: Assets/Assets/Models/FightingUnityChan_FreeAsset/... failed at 260).
        /// The manifest keeps the path for each copy.
        /// </summary>
        string CopyPath(string id, string rel)
        {
            var h = HashText(rel);
            return Path.Combine(SnapshotDir(id), "files", h.Substring(0, 2), h);
        }

        /// <summary>
        /// The copy of a project file in a step's backup (the file before that step), or null. The fixes after Unity's
        /// converter read what the converter lost from it (Material Variants' own values, M8).
        /// </summary>
        public string CopyIn(string id, string rel)
        {
            if (string.IsNullOrEmpty(id) || !File.Exists(ManifestPath(id))) return null;
            var copy = CopyPath(id, rel);
            return File.Exists(copy) ? copy : null;
        }

        /// <summary>
        /// A project file as it was before the migration first changed it: its copy in the earliest backup whose step changed
        /// it, or null. The shader panel previews the Built-in original of a file that is already converted from it.
        /// </summary>
        public string OriginalCopy(string rel, out string id)
        {
            id = null;
            if (!Directory.Exists(BackupRoot)) return null;
            // Backup ids begin with the UTC time (yyyyMMdd-HHmmss-fff), so their order is the order of the steps.
            foreach (var dir in Directory.GetDirectories(BackupRoot).Select(Path.GetFileName).Where(d => d != "steps").OrderBy(d => d, StringComparer.Ordinal))
            {
                var snap = Load(dir);
                if (snap == null || !snap.Changed.Any(c => c.Path == rel)) continue;
                var copy = CopyIn(dir, rel);
                if (copy == null) continue;
                id = dir;
                return copy;
            }
            return null;
        }

        /// <summary>Where rollback keeps a file it removed; index.txt next to it maps names back to project paths.</summary>
        public string RemovedCopyPath(string id, string rel) => Path.Combine(SnapshotDir(id), "removed", HashText(rel));
        public string ReplacedCopyPath(string id, string rel) => Path.Combine(SnapshotDir(id), "replaced", HashText(rel));

        static string HashText(string s)
        {
            using (var sha = SHA1.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                var sb = new StringBuilder(40);
                foreach (var b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        string StepsDir => Path.Combine(BackupRoot, "steps");

        /// <summary>
        /// Records the project's file list (path, size, write time) when a step starts and when it ends, for every step,
        /// including the read-only ones. Settle attributes to the migration only what appeared or changed inside these
        /// windows; files that appear between steps may be the user's and are never rolled back.
        /// </summary>
        public void RecordStep(string step, bool start)
        {
            Directory.CreateDirectory(StepsDir);
            var existing = Directory.GetFiles(StepsDir, "*.tsv").Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToList();
            int seq = existing.Count == 0 ? 0 : int.Parse(existing[existing.Count - 1].Substring(0, 4));
            if (start) seq++;
            else if (existing.Count == 0 || !existing[existing.Count - 1].EndsWith("-start.tsv", StringComparison.Ordinal)) return;
            var sb = new StringBuilder();
            foreach (var rel in ListProjectFiles())
            {
                // A file the Editor is replacing can be gone for a moment (closing run T: ProjectAuditorSettings.asset,
                // ImageUltra6P): waited for, and left out of this list when it does not come back.
                var abs = Abs(rel);
                if (!WaitForFile(abs)) continue;
                long length, ticks;
                try { var fi = new FileInfo(abs); length = fi.Length; ticks = fi.LastWriteTimeUtc.Ticks; }
                catch (FileNotFoundException) { continue; }
                sb.Append(rel).Append('\t').Append(length).Append('\t').Append(ticks).Append('\n');
            }
            File.WriteAllText(Path.Combine(StepsDir, $"{seq:0000}-{step}-{(start ? "start" : "end")}.tsv"), sb.ToString());
        }

        /// <summary>Forgets the recorded step windows (after a full rollback, so a new migration starts clean).</summary>
        public void ClearSteps()
        {
            if (Directory.Exists(StepsDir)) Directory.Delete(StepsDir, true);
        }

        /// <summary>Paths created or changed while one of our steps ran (a start list and its end list both exist).</summary>
        public HashSet<string> TouchedDuringSteps()
        {
            var touched = new HashSet<string>(StringComparer.Ordinal);
            if (!Directory.Exists(StepsDir)) return touched;
            foreach (var end in Directory.GetFiles(StepsDir, "*-end.tsv"))
            {
                var start = end.Substring(0, end.Length - "-end.tsv".Length) + "-start.tsv";
                if (!File.Exists(start)) continue;
                var before = ReadList(start);
                foreach (var kv in ReadList(end))
                    if (!before.TryGetValue(kv.Key, out var sig) || sig != kv.Value) touched.Add(kv.Key);
            }
            return touched;
        }

        static Dictionary<string, string> ReadList(string file)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(file))
            {
                int tab = line.IndexOf('\t');
                if (tab > 0) d[line.Substring(0, tab)] = line.Substring(tab + 1);
            }
            return d;
        }

        /// <summary>Backs up the project before a step. Call <see cref="Complete"/> after the step.</summary>
        /// <exception cref="NotEnoughDiskSpaceException">The drive cannot hold the backup with room to spare; nothing was copied.</exception>
        public Snapshot Begin(string step)
        {
            var id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + step;
            var snap = new Snapshot { Id = id, Step = step, CreatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") };
            var files = ListProjectFiles().ToList();
            CheckFreeSpace(files.Where(ShouldCopy));
            try
            {
                foreach (var rel in files)
                {
                    snap.Existing.Add(rel);
                    if (!ShouldCopy(rel)) continue;
                    var dst = CopyPath(id, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    var abs = Abs(rel);
                    // A file the Editor rewrites while the step starts can be gone for a moment (closing run T, 18.10.2026:
                    // ProjectSettings/ProjectAuditorSettings.asset on 6000.5); one that does not come back is not copied.
                    try { if (!WaitForFile(abs)) continue; Retry(() => { File.Copy(abs, dst, true); return true; }); }
                    catch (FileNotFoundException) { continue; }
                    snap.Files.Add(new FileHash { Path = rel, Hash = Hash(dst) });
                }
                Save(snap);
            }
            catch
            {
                // A backup that could not be finished (the disk filled up after all) is no backup: its folder goes, nothing
                // of the project changed yet (code review R-09, R-15.3).
                try { if (Directory.Exists(SnapshotDir(id))) Directory.Delete(SnapshotDir(id), true); } catch (Exception) { }
                throw;
            }
            return snap;
        }

        /// <summary>
        /// The free space of a drive, in bytes, or -1 when it cannot be told (a network path). Tests replace it.
        /// </summary>
        public static Func<string, long> FreeSpace = path =>
        {
            try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))).AvailableFreeSpace; }
            catch (Exception) { return -1; }
        };

        /// <summary>Room kept free beyond the backup itself: the step's own writes and the rollback's copies need it too.</summary>
        public const long SpareBytes = 512L * 1024 * 1024;

        /// <summary>
        /// A step does not start a backup the drive cannot hold (code review R-09): running out of space half way through a
        /// step leaves cut-off project files, and a rollback then fails for the same reason. The backup needs its own size
        /// twice over (the step rewrites files, the rollback keeps the versions it replaces) plus <see cref="SpareBytes"/>.
        /// </summary>
        void CheckFreeSpace(IEnumerable<string> toCopy)
        {
            long free = FreeSpace(root);
            if (free < 0) return;
            long size = 0;
            foreach (var rel in toCopy)
            {
                try { size += new FileInfo(Abs(rel)).Length; } catch (Exception) { }
            }
            long need = size * 2 + SpareBytes;
            if (free < need) throw new NotEnoughDiskSpaceException(need, free, Path.GetPathRoot(root));
        }

        /// <summary>True when the file is there, waiting up to two seconds for one the Editor is replacing.</summary>
        static bool WaitForFile(string abs)
        {
            for (int i = 0; i < 20 && !File.Exists(abs); i++) System.Threading.Thread.Sleep(100);
            return File.Exists(abs);
        }

        /// <summary>The file's hash, or null when it is gone (see <see cref="WaitForFile"/>).</summary>
        static string HashIfThere(string abs)
        {
            try { return WaitForFile(abs) ? Retry(() => Hash(abs)) : null; }
            catch (FileNotFoundException) { return null; }
        }

        /// <summary>Records what the step changed: files whose hash differs, files created and files deleted.</summary>
        public void Complete(Snapshot snap)
        {
            var existing = new HashSet<string>(snap.Existing, StringComparer.Ordinal);
            var now = ListProjectFiles().ToList();
            var nowSet = new HashSet<string>(now, StringComparer.Ordinal);
            snap.Changed.Clear(); snap.Created.Clear(); snap.Deleted.Clear();
            foreach (var f in snap.Files)
            {
                var h = nowSet.Contains(f.Path) ? HashIfThere(Abs(f.Path)) : null;
                if (h == null) { snap.Deleted.Add(f.Path); continue; }
                if (h != f.Hash) snap.Changed.Add(new FileChange { Path = f.Path, Before = f.Hash, After = h });
            }
            foreach (var rel in now)
            {
                if (existing.Contains(rel)) continue;
                var h = HashIfThere(Abs(rel));
                if (h != null) snap.Created.Add(new FileHash { Path = rel, Hash = h });
            }
            // Content files that were not copied but disappeared cannot be restored; they are listed so nobody is surprised.
            foreach (var rel in snap.Existing)
                if (!nowSet.Contains(rel) && !ShouldCopy(rel)) snap.Deleted.Add(rel);
            snap.Complete = true;
            snap.CompletedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            Save(snap);
        }

        /// <summary>
        /// While a migration is in progress, Unity keeps rewriting what a step converted (URP re-serializes upgraded
        /// materials, registers its settings when the Editor quits, creates settings on the first Play Mode). Settle
        /// looks at every gap: from the end of each modifying step to the start of the next one, and from the last one to
        /// now. What appeared or changed in a gap while one of our steps ran (<see cref="RecordStep"/>) is folded into the
        /// step before the gap, so its rollback undoes it. Anything else in a gap may be the user's: it is never rolled
        /// back and is listed in the last snapshot's <c>Between</c>.
        /// Call it before each step and before a rollback until the migration is finished.
        /// </summary>
        public void Settle(IList<Snapshot> modifyingSnapshots)
        {
            var list = modifyingSnapshots.Where(s => s != null && s.Complete).ToList();
            if (list.Count == 0) return;
            var touched = TouchedDuringSteps();
            var between = new List<string>();
            var nowList = ListProjectFiles().ToList();
            var hashNow = new Dictionary<string, string>(StringComparer.Ordinal);
            // A file another program holds is left out of the settling (null: not compared) instead of stopping the rollback (R-01).
            string HashNow(string rel)
            {
                if (hashNow.TryGetValue(rel, out var h)) return h;
                try { h = Retry(() => Hash(Abs(rel))); } catch (Exception) { h = null; }
                return hashNow[rel] = h;
            }
            for (int i = 0; i < list.Count; i++)
            {
                var snap = list[i];
                bool last = i == list.Count - 1;
                // The project at the end of the gap: the next step's backup, or the files as they are now.
                IEnumerable<string> after;
                Func<string, string> hashAfter; // null: the file exists but its content is not known (not copied)
                if (last)
                {
                    after = nowList;
                    hashAfter = HashNow;
                }
                else
                {
                    var next = list[i + 1];
                    var copies = next.Files.ToDictionary(f => f.Path, f => f.Hash, StringComparer.Ordinal);
                    after = next.Existing;
                    hashAfter = rel => copies.TryGetValue(rel, out var h) ? h : null;
                }
                var afterSet = new HashSet<string>(after, StringComparer.Ordinal);
                // The project at the start of the gap: what this step left behind.
                var left = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var e in snap.Existing) left[e] = null;
                foreach (var f in snap.Files) left[f.Path] = f.Hash;
                foreach (var d in snap.Deleted) left.Remove(d);
                foreach (var c in snap.Changed) left[c.Path] = c.After;
                foreach (var c in snap.Created) left[c.Path] = c.Hash;

                snap.Created.RemoveAll(c => !afterSet.Contains(c.Path));
                foreach (var rel in after)
                {
                    if (left.TryGetValue(rel, out var was))
                    {
                        if (was == null) continue; // content that is never copied (textures, models): not compared
                        var now = hashAfter(rel);
                        if (now == null || now == was) continue;
                        if (!touched.Contains(rel)) { between.Add(rel); continue; }
                        var changed = snap.Changed.FirstOrDefault(c => c.Path == rel);
                        var created = snap.Created.FirstOrDefault(c => c.Path == rel);
                        if (changed != null) changed.After = now;
                        else if (created != null) created.Hash = now;
                        else snap.Changed.Add(new FileChange { Path = rel, Before = was, After = now });
                    }
                    else
                    {
                        if (!touched.Contains(rel)) { between.Add(rel); continue; }
                        snap.Created.Add(new FileHash { Path = rel, Hash = hashAfter(rel) ?? HashNow(rel) });
                    }
                }
            }
            foreach (var snap in list) snap.Between.Clear();
            list[list.Count - 1].Between.AddRange(between.Distinct(StringComparer.Ordinal));
            foreach (var snap in list) Save(snap);
        }

        /// <summary>
        /// Undoes one step: restores changed and deleted files, removes created ones. A file whose current hash is not
        /// the hash the step left behind was changed by someone else afterwards and is left alone (listed in Skipped).
        /// </summary>
        public RollbackResult Rollback(Snapshot snap)
        {
            var r = new RollbackResult();
            r.Between.AddRange(snap.Between);
            var copied = snap.Files.ToDictionary(f => f.Path, f => f.Hash, StringComparer.Ordinal);
            // Every file is handled on its own: a file another program holds (OneDrive, an antivirus, an IDE) is an error for
            // that file and the rollback goes on (code review R-01, R-02, 18.10.2026). A file already back to its content
            // before the step (a rollback run again after one that did not finish) is not "changed later".
            foreach (var c in snap.Changed)
            {
                try
                {
                    var abs = Abs(c.Path);
                    if (File.Exists(abs))
                    {
                        var now = Retry(() => Hash(abs));
                        if (now == c.Before) continue;
                        if (now != c.After) { r.Skipped.Add(c.Path); continue; }
                    }
                    Restore(snap, c.Path, r);
                }
                catch (Exception e) { r.Errors.Add(c.Path + ": " + e.Message); }
            }
            foreach (var d in snap.Deleted)
            {
                try
                {
                    if (!copied.ContainsKey(d)) { r.Errors.Add(d + ": deleted and not in the backup"); continue; }
                    if (File.Exists(Abs(d)))
                    {
                        if (copied[d] != null && Retry(() => Hash(Abs(d))) == copied[d]) continue;
                        r.Skipped.Add(d);
                        continue;
                    }
                    Restore(snap, d, r);
                }
                catch (Exception e) { r.Errors.Add(d + ": " + e.Message); }
            }
            foreach (var c in snap.Created.OrderByDescending(x => x.Path.Length))
            {
                // Removed files are moved into the backup, never deleted outright.
                try
                {
                    var abs = Abs(c.Path);
                    if (!File.Exists(abs)) continue;
                    if (Retry(() => Hash(abs)) != c.Hash) { r.Skipped.Add(c.Path); continue; }
                    var kept = RemovedCopyPath(snap.Id, c.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(kept));
                    Retry(() => { File.Copy(abs, kept, true); return true; });
                    File.AppendAllText(Path.Combine(SnapshotDir(snap.Id), "removed", "index.txt"), Path.GetFileName(kept) + "\t" + c.Path + "\n");
                    Retry(() => { File.Delete(abs); return true; });
                    r.Removed.Add(c.Path);
                }
                catch (Exception e) { r.Errors.Add(c.Path + ": " + e.Message); }
            }
            try { RemoveEmptyCreatedFolders(snap); }
            catch (Exception e) { r.Errors.Add("An empty folder the step created: " + e.Message); }
            return r;
        }

        void Restore(Snapshot snap, string rel, RollbackResult r)
        {
            try
            {
                var abs = Abs(rel);
                Directory.CreateDirectory(Path.GetDirectoryName(abs));
                var copy = CopyPath(snap.Id, rel);
                // The version the rollback replaces is kept first (R-16): usually the converted file, but it may hold the
                // user's own later work, which a step's window took for the step's.
                if (File.Exists(abs))
                {
                    var kept = ReplacedCopyPath(snap.Id, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(kept));
                    Retry(() => { File.Copy(abs, kept, true); return true; });
                    File.AppendAllText(Path.Combine(SnapshotDir(snap.Id), "replaced", "index.txt"), Path.GetFileName(kept) + "\t" + rel + "\n");
                    r.Kept.Add(rel + " -> " + kept.Replace('\\', '/'));
                }
                Retry(() => { File.Copy(copy, abs, true); return true; });
                r.Restored.Add(rel);
            }
            catch (Exception e) { r.Errors.Add(rel + ": " + e.Message); }
        }

        /// <summary>
        /// Runs a file operation again a few times when another process holds the file for a moment (sharing violation,
        /// access denied while a sync client or an antivirus reads it), as <see cref="WriteAtomically"/> does.
        /// </summary>
        public static T Retry<T>(Func<T> op)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { return op(); }
                catch (Exception e) when ((e is IOException && !(e is FileNotFoundException) && !(e is DirectoryNotFoundException) || e is UnauthorizedAccessException) && attempt < 5)
                {
                    System.Threading.Thread.Sleep(100 * (attempt + 1));
                }
            }
        }

        /// <summary>Folders that exist only because the step created files in them (Unity keeps a .meta per folder).</summary>
        void RemoveEmptyCreatedFolders(Snapshot snap)
        {
            var existingDirs = new HashSet<string>(snap.Existing.Select(p => Path.GetDirectoryName(p)?.Replace('\\', '/') ?? ""), StringComparer.Ordinal);
            var dirs = snap.Created.Select(c => Path.GetDirectoryName(c.Path)?.Replace('\\', '/') ?? "").Distinct().OrderByDescending(d => d.Length);
            foreach (var d in dirs)
            {
                var cur = d;
                while (cur.Length > 0 && !existingDirs.Contains(cur) && !ProjectFolders.Contains(cur))
                {
                    var abs = Abs(cur);
                    if (!Directory.Exists(abs) || Directory.EnumerateFileSystemEntries(abs).Any()) break;
                    Directory.Delete(abs);
                    cur = Path.GetDirectoryName(cur)?.Replace('\\', '/') ?? "";
                }
            }
        }

        public void Save(Snapshot snap)
        {
            Directory.CreateDirectory(SnapshotDir(snap.Id));
            WriteAtomically(ManifestPath(snap.Id), JsonUtility.ToJson(snap, true));
        }

        /// <summary>
        /// Writes a file so that a process killed in the middle leaves the old content or the new one, never half of it:
        /// the text goes to a temporary file next to it, which then replaces the file.
        /// </summary>
        public static void WriteAtomically(string path, string text)
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (!File.Exists(path)) { File.Move(tmp, path); return; }
            // File.Replace fails now and then on Windows while another process (the indexer, an antivirus) holds the target
            // for a moment ("Unable to remove the file to be replaced", kode80SSR rollback, 2026-10-04): try again, then copy.
            for (int attempt = 0; ; attempt++)
            {
                try { File.Replace(tmp, path, null); return; }
                catch (IOException) when (attempt < 4) { System.Threading.Thread.Sleep(50 * (attempt + 1)); }
                catch (Exception) when (File.Exists(tmp))
                {
                    File.Copy(tmp, path, true);
                    File.Delete(tmp);
                    return;
                }
            }
        }

        public Snapshot Load(string id)
        {
            var p = ManifestPath(id);
            if (string.IsNullOrEmpty(id) || !File.Exists(p)) return null;
            try { return JsonUtility.FromJson<Snapshot>(File.ReadAllText(p)); }
            catch { return null; }
        }

        /// <summary>
        /// Closes a backup whose step did not finish (the Editor was killed, or the step failed): what changed since the
        /// backup began counts as the step's change, so a rollback undoes it (QA-001). Returns false when there is no
        /// such backup (the step was stopped before its backup was written: nothing of the step's own changed yet).
        /// </summary>
        public bool CompleteIfOpen(string id)
        {
            var snap = Load(id);
            if (snap == null) return false;
            if (!snap.Complete) Complete(snap);
            return true;
        }
    }
}
