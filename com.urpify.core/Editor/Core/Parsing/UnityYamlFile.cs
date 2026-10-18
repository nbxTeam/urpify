using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Urpify.Parsing
{
    /// <summary>
    /// Minimal reader for text-serialized scenes and prefabs: one entry per "--- !u!classId &amp;fileId" document,
    /// with top-level fields as raw strings. Enough to find components, script GUIDs and object paths
    /// without opening the scene in the Editor.
    /// </summary>
    public sealed class UnityYamlFile
    {
        public sealed class Doc
        {
            public int ClassId;
            public long FileId;
            /// <summary>Type name from the first line, e.g. "Projector", "MonoBehaviour".</summary>
            public string TypeName = "";
            public bool Stripped;
            public int Line;
            /// <summary>Top-level fields (two-space indent) of the document body.</summary>
            public Dictionary<string, string> Fields = new Dictionary<string, string>(StringComparer.Ordinal);
            /// <summary>Items of top-level lists ("  - value" lines), by field name, e.g. m_Materials.</summary>
            public Dictionary<string, List<string>> Lists = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            internal string LastKey = "";

            public IReadOnlyList<string> List(string key) => Lists.TryGetValue(key, out var l) ? (IReadOnlyList<string>)l : Array.Empty<string>();

            /// <summary>(fileID, guid) of every reference in a list field.</summary>
            public IEnumerable<(long fileId, string guid)> ListRefs(string key)
            {
                foreach (var item in List(key))
                {
                    var f = FileIdRx.Match(item);
                    var g = GuidRx.Match(item);
                    if (f.Success) yield return (long.Parse(f.Groups[1].Value), g.Success ? g.Groups[1].Value : "");
                }
            }

            public string Get(string key) => Fields.TryGetValue(key, out var v) ? v : "";

            /// <summary>fileID from a reference field like "{fileID: 123}", 0 when absent.</summary>
            public long RefFileId(string key)
            {
                var m = FileIdRx.Match(Get(key));
                return m.Success ? long.Parse(m.Groups[1].Value) : 0;
            }

            /// <summary>guid from a reference field like "{fileID: 11500000, guid: abc, type: 3}", "" when absent.</summary>
            public string RefGuid(string key)
            {
                var m = GuidRx.Match(Get(key));
                return m.Success ? m.Groups[1].Value : "";
            }
        }

        static readonly Regex HeaderRx = new Regex(@"^--- !u!(\d+) &(-?\d+)( stripped)?", RegexOptions.Compiled);
        static readonly Regex FileIdRx = new Regex(@"fileID:\s*(-?\d+)", RegexOptions.Compiled);
        static readonly Regex GuidRx = new Regex(@"guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);

        public bool IsText;
        public readonly List<Doc> Docs = new List<Doc>();
        readonly Dictionary<long, Doc> byId = new Dictionary<long, Doc>();

        public static UnityYamlFile Parse(string text)
        {
            var f = new UnityYamlFile();
            if (text == null || !text.StartsWith("%YAML", StringComparison.Ordinal)) return f;
            f.IsText = true;
            Doc cur = null;
            int lineNo = 0;
            int pos = 0;
            while (pos < text.Length)
            {
                int nl = text.IndexOf('\n', pos);
                if (nl < 0) nl = text.Length;
                var line = text.Substring(pos, nl - pos).TrimEnd('\r');
                pos = nl + 1;
                lineNo++;
                if (line.StartsWith("--- ", StringComparison.Ordinal))
                {
                    var m = HeaderRx.Match(line);
                    if (m.Success)
                    {
                        cur = new Doc { ClassId = int.Parse(m.Groups[1].Value), FileId = long.Parse(m.Groups[2].Value), Stripped = m.Groups[3].Success, Line = lineNo };
                        f.Docs.Add(cur);
                        f.byId[cur.FileId] = cur;
                    }
                    continue;
                }
                if (cur == null) continue;
                if (cur.TypeName.Length == 0 && line.Length > 1 && line[line.Length - 1] == ':' && line[0] != ' ')
                {
                    cur.TypeName = line.Substring(0, line.Length - 1);
                    continue;
                }
                if (line.Length > 2 && line[0] == ' ' && line[1] == ' ' && line[2] != ' ' && line[2] != '-')
                {
                    int colon = line.IndexOf(':', 2);
                    if (colon > 2)
                    {
                        var key = line.Substring(2, colon - 2);
                        var val = colon + 1 < line.Length ? line.Substring(colon + 1).Trim() : "";
                        if (!cur.Fields.ContainsKey(key)) cur.Fields[key] = val;
                        cur.LastKey = key;
                    }
                }
                else if (line.Length > 4 && line.StartsWith("  - ", StringComparison.Ordinal) && cur.LastKey.Length > 0)
                {
                    if (!cur.Lists.TryGetValue(cur.LastKey, out var items)) cur.Lists[cur.LastKey] = items = new List<string>();
                    items.Add(line.Substring(4).Trim());
                }
            }
            return f;
        }

        public Doc Get(long fileId) => byId.TryGetValue(fileId, out var d) ? d : null;

        public IEnumerable<Doc> OfType(string typeName)
        {
            foreach (var d in Docs) if (d.TypeName == typeName) yield return d;
        }

        /// <summary>Name of the GameObject a component belongs to, "" when unknown.</summary>
        public string GameObjectName(Doc component)
        {
            var go = Get(component.RefFileId("m_GameObject"));
            return go?.Get("m_Name") ?? "";
        }

        /// <summary>Hierarchy path "Root/Child/Object" of the GameObject a component belongs to.</summary>
        public string GameObjectPath(Doc component)
        {
            var go = component.TypeName == "GameObject" ? component : Get(component.RefFileId("m_GameObject"));
            if (go == null) return "";
            var parts = new List<string>();
            var guard = 0;
            while (go != null && guard++ < 128)
            {
                parts.Add(go.Get("m_Name"));
                Doc transform = null;
                foreach (var d in Docs)
                    if ((d.TypeName == "Transform" || d.TypeName == "RectTransform") && d.RefFileId("m_GameObject") == go.FileId) { transform = d; break; }
                if (transform == null) break;
                var parent = Get(transform.RefFileId("m_Father"));
                go = parent == null ? null : Get(parent.RefFileId("m_GameObject"));
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
