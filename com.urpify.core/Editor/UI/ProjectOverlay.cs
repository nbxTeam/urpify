using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Urpify.Pipeline;

namespace Urpify.UI
{
    /// <summary>
    /// The mark in the Project window (TZ 4.2): assets the last scan found broken get a small icon at the right of their
    /// row, the error icon for those that turn pink and the warning icon for silent breakage, with a tooltip. Off in
    /// Project Settings > Urpify (a personal setting). Reads the newest report the window or the migration wrote.
    /// </summary>
    [InitializeOnLoad]
    static class ProjectOverlay
    {
        static readonly Dictionary<string, (Severity severity, string title)> s_ByGuid = new Dictionary<string, (Severity, string)>(StringComparer.Ordinal);
        static DateTime s_Loaded = DateTime.MinValue;
        static double s_NextCheck;
        static GUIContent s_Pink, s_Silent;

        static ProjectOverlay()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            EditorApplication.projectWindowItemOnGUI += OnItem;
        }

        static string Newest()
        {
            var candidates = new List<string> { "Library/Urpify/last-report.json" };
            if (Directory.Exists(MigrationRunner.ReportsDir)) candidates.AddRange(Directory.GetFiles(MigrationRunner.ReportsDir, "*-latest.internal.json"));
            return candidates.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        }

        static void Refresh()
        {
            if (EditorApplication.timeSinceStartup < s_NextCheck) return;
            s_NextCheck = EditorApplication.timeSinceStartup + 2;
            var path = Newest();
            if (path == null) { s_ByGuid.Clear(); return; }
            var time = File.GetLastWriteTimeUtc(path);
            if (time == s_Loaded) return;
            s_Loaded = time;
            s_ByGuid.Clear();
            try
            {
                var r = JsonUtility.FromJson<MigrationReport>(File.ReadAllText(path));
                if (r == null) return;
                foreach (var a in r.Assets.Where(a => a.Severity >= Severity.Silent))
                {
                    var guid = AssetDatabase.AssetPathToGUID(a.Path);
                    if (string.IsNullOrEmpty(guid)) continue;
                    var title = r.Findings.Where(f => f.AssetPath == a.Path).OrderByDescending(f => f.Severity).Select(f => f.LocalTitle).FirstOrDefault() ?? "";
                    s_ByGuid[guid] = (a.Severity, title);
                }
            }
            catch (Exception) { }
        }

        static void OnItem(string guid, Rect rect)
        {
            if (!KitUserSettings.Current.ProjectOverlays) return;
            Refresh();
            if (!s_ByGuid.TryGetValue(guid, out var hit)) return;
            if (s_Pink == null)
            {
                s_Pink = new GUIContent(EditorGUIUtility.IconContent("console.erroricon.sml").image);
                s_Silent = new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml").image);
            }
            var icon = hit.severity == Severity.Pink ? s_Pink : s_Silent;
            var content = new GUIContent(icon.image, Loc.Get("ui.overlay.tip", Loc.Get("severity." + hit.severity), hit.title));
            float size = Mathf.Min(16, rect.height);
            // List rows: at the right end of the row; grid icons: in the top right corner of the thumbnail.
            var r = rect.height > 20 ? new Rect(rect.xMax - size, rect.y, size, size) : new Rect(rect.xMax - size - 2, rect.y + (rect.height - size) / 2, size, size);
            GUI.Label(r, content);
        }
    }
}
