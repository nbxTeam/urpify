using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Urpify.Analysis;
using Urpify.Generation;
using Urpify.Pipeline;

namespace Urpify.UI
{
    /// <summary>
    /// Code tab of the shader panel (TZ 4.2): original and result side by side with changed lines marked, a tooltip on
    /// each change saying what changed and why, and an editable result whose compiler errors land on their lines.
    /// </summary>
    public sealed class ShaderCodeView : VisualElement
    {
        static readonly string[][] BaseKeywords = { new string[0], new[] { "_MAIN_LIGHT_SHADOWS", "_ADDITIONAL_LIGHTS", "FOG_LINEAR" } };

        readonly ShaderPanelModel model;
        readonly VisualElement columns = new VisualElement();
        readonly VisualElement messages = new VisualElement();
        readonly TextField editor = new TextField { multiline = true };
        readonly Dictionary<int, string> errorLines = new Dictionary<int, string>();

        public ShaderCodeView(ShaderPanelModel model)
        {
            this.model = model;
            columns.AddToClassList("urpify-code-columns");
            Add(columns);
            if (model.HasResult)
            {
                var buttons = new VisualElement();
                buttons.AddToClassList("urpify-buttons");
                var edit = new Button(ToggleEditor) { text = Loc.Get("ui.shader.edit"), tooltip = Loc.Get("ui.shader.editTip") };
                buttons.Add(edit);
                buttons.Add(new Button(Compile) { text = Loc.Get("ui.shader.compile"), tooltip = Loc.Get("ui.shader.compileTip") });
                var reset = new Button(() => { editor.value = model.Conversion.Output; Compile(); }) { text = Loc.Get("ui.shader.reset") };
                buttons.Add(reset);
                Add(buttons);
                editor.value = model.ConvertedText;
                editor.AddToClassList("urpify-code-editor");
                UiKit.UseMonospace(editor);
                editor.Query<VisualElement>().ForEach(UiKit.UseMonospace);
                editor.style.display = DisplayStyle.None;
                Add(editor);
            }
            else if (model.HasDraft) Add(UiKit.Note(Loc.Get("ui.shader.draftNote")));
            else if (model.BackupOriginalText != null) Insert(0, UiKit.Note(Loc.Get("ui.shader.codeFromBackup", model.BackupId)));
            Add(messages);
            Rebuild();
        }

        void ToggleEditor() => editor.style.display = editor.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;

        void Rebuild()
        {
            columns.Clear();
            if (model.BackupOriginalText != null && !model.HasResult) { MigratedColumns(); return; }
            var a = Lines(model.OriginalText);
            var b = model.HasResult ? Lines(model.ConvertedText) : new string[0];
            var (removed, added) = model.HasResult ? LineDiff.Compare(a, b) : (new bool[a.Length], new bool[0]);
            var notes = model.Conversion?.Notes.Where(n => n.Line > 0).GroupBy(n => n.Line)
                .ToDictionary(g => g.Key, g => string.Join("\n", g.Select(n => n.Text))) ?? new Dictionary<int, string>();
            columns.Add(Column(Loc.Get("ui.shader.original"), a, i => removed[i] ? "urpify-codeline-removed" : null,
                i => notes.TryGetValue(i + 1, out var t) ? t : removed[i] ? Loc.Get("ui.shader.why.replaced") : null));
            if (model.HasResult)
            {
                var reasons = Reasons(b);
                columns.Add(Column(Loc.Get(model.Edited ? "ui.shader.convertedEdited" : "ui.shader.converted"), b,
                    i => errorLines.ContainsKey(i + 1) ? "urpify-codeline-error" : added[i] ? "urpify-codeline-added" : null,
                    i => errorLines.TryGetValue(i + 1, out var e) ? e : added[i] ? reasons[i] : null));
            }
            else if (model.HasDraft)
            {
                var d = Lines(model.DraftText);
                var (_, draftAdded) = LineDiff.Compare(a, d);
                columns.Add(Column(Loc.Get("ui.shader.draft"), d,
                    i => d[i].Contains(ShaderConversion.DraftMarker) ? "urpify-codeline-error" : draftAdded[i] ? "urpify-codeline-added" : null,
                    i => d[i].Contains(ShaderConversion.DraftMarker) ? Loc.Get("ui.shader.draftStop") : null));
            }
            else if (!model.ProInstalled)
            {
                // The Free package: the converted column is there but closed, with what Pro would do (TZ section 2).
                var col = new VisualElement();
                col.AddToClassList("urpify-code-column");
                col.AddToClassList("urpify-code-locked");
                var head = new VisualElement();
                head.AddToClassList("urpify-pro-row");
                head.Add(UiKit.H2(Loc.Get("ui.shader.converted")));
                head.Add(UiKit.ProBadge(Loc.Get("ui.shader.proColumnTip")));
                col.Add(head);
                col.Add(UiKit.Note(NoResultText()));
                columns.Add(col);
            }
            else
            {
                // No result: the same column as the original, so the two sides line up, with why it is empty.
                var col = new VisualElement();
                col.AddToClassList("urpify-code-column");
                col.Add(UiKit.H2(Loc.Get("ui.shader.converted")));
                col.Add(UiKit.Note(NoResultText()));
                columns.Add(col);
            }
        }

        /// <summary>
        /// A file the migration already converted: the Built-in original from its backup on the left, the file as it is in the
        /// project on the right, changed lines marked (design review 13.10.2026, item 3: the left column showed the URP file as
        /// "Original" and the right one said no converter handles it).
        /// </summary>
        void MigratedColumns()
        {
            var a = Lines(model.BackupOriginalText);
            var b = Lines(model.OriginalText);
            var (removed, added) = LineDiff.Compare(a, b);
            var reasons = Reasons(b);
            columns.Add(Column(Loc.Get("ui.shader.originalFromBackup"), a, i => removed[i] ? "urpify-codeline-removed" : null,
                i => removed[i] ? Loc.Get("ui.shader.why.replaced") : null));
            columns.Add(Column(Loc.Get("ui.shader.inProjectNow"), b, i => added[i] ? "urpify-codeline-added" : null, i => added[i] ? reasons[i] : null));
        }

        string NoResultText()
        {
            if (!model.ProInstalled) return Loc.Get("ui.shader.proNeeded");
            if (model.Converter == null) return Loc.Get(model.BreaksInUrp == false ? "ui.shader.rendersAsIs" : "ui.shader.noConverter");
            return Loc.Get("ui.shader.notConverted");
        }

        static string[] Lines(string text) => text.Replace("\r\n", "\n").Split('\n');

        static VisualElement Column(string title, string[] lines, System.Func<int, string> mark, System.Func<int, string> tooltip)
        {
            var col = new VisualElement();
            col.AddToClassList("urpify-code-column");
            col.Add(UiKit.H2(title));
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.AddToClassList("urpify-code-scroll");
            UiKit.UseMonospace(scroll);
            for (int i = 0; i < lines.Length; i++)
            {
                var cls = mark(i);
                // Not colour alone: the gutter sign says what the line is.
                var sign = cls == null ? " " : cls == "urpify-codeline-removed" ? "-" : cls == "urpify-codeline-error" ? "!" : "+";
                var l = new Label(UiKit.CodeText(sign + (i + 1).ToString().PadLeft(5) + "  " + lines[i]));
                l.AddToClassList("urpify-codeline");
                // On each line: the Editor's stylesheet sets the font per text element, so it is not inherited (M4 screenshots).
                UiKit.UseMonospace(l);
                if (cls != null) l.AddToClassList(cls);
                var tip = tooltip(i);
                if (!string.IsNullOrEmpty(tip)) l.tooltip = tip;
                scroll.Add(l);
            }
            col.Add(scroll);
            return col;
        }

        /// <summary>Compiles the edited result in memory; errors go on their lines and into the list below.</summary>
        void Compile()
        {
            model.SetConverted(editor.value);
            errorLines.Clear();
            messages.Clear();
            var shader = ShaderUtil.CreateShaderAsset(model.PreviewText, false);
            List<CompileMessage> errors;
            if (shader == null) errors = new List<CompileMessage> { new CompileMessage { Message = Loc.Get("ui.shader.notImported") } };
            else
            {
                shader.hideFlags = HideFlags.HideAndDontSave;
                var original = model.InProject ? AssetDatabase.LoadAssetAtPath<Shader>(model.Path) : null;
                var sets = original != null ? MigrationRunner.KeywordSets(original) : BaseKeywords.ToList();
                try { errors = ShaderCompileCheck.Errors(shader, sets); }
                finally { Object.DestroyImmediate(shader); }
            }
            if (errors.Count == 0) messages.Add(UiKit.Note(Loc.Get("ui.shader.compiles")));
            foreach (var e in errors.Take(30))
            {
                var inFile = string.IsNullOrEmpty(e.File) || e.File.EndsWith(".shader", System.StringComparison.OrdinalIgnoreCase);
                if (inFile && e.Line > 0 && !errorLines.ContainsKey(e.Line)) errorLines[e.Line] = e.Message;
                var where = e.Line > 0 ? Loc.Get("ui.shader.line", e.Line) + (inFile ? "" : " " + e.File) + ": " : "";
                var l = new Label(where + e.Message + (e.Pass.Length > 0 ? "  (" + e.Pass + " " + e.Stage + ")" : ""));
                l.AddToClassList("urpify-manual");
                messages.Add(l);
            }
            if (errors.Count > 30) messages.Add(UiKit.Note(Loc.Get("report.andMore", errors.Count - 30).Trim()));
            Rebuild();
        }

        /// <summary>Why each line of the result is there, for the tooltips on changed lines.</summary>
        static string[] Reasons(string[] lines)
        {
            var r = new string[lines.Length];
            string block = "";
            for (int i = 0; i < lines.Length; i++)
            {
                var s = lines[i];
                if (s.Contains("HLSLINCLUDE")) block = "include";
                else if (s.Contains("Name \"ForwardLit\"")) block = "forward";
                else if (s.Contains("Name \"ShadowCaster\"")) block = "shadow";
                else if (s.Contains("Name \"DepthOnly\"")) block = "depth";
                else if (s.Contains("Name \"DepthNormals\"")) block = "depthNormals";
                else if (s.Contains("Name \"Meta\"")) block = "meta";
                string key;
                if (s.Contains("moved to UnityPerMaterial") || s.Contains("CBUFFER_")) key = "cbuffer";
                else if (s.Contains("(BirpCompat)") || s.Contains("BirpCompat")) key = "compat";
                else if (s.Contains("(set by the converter)")) key = "pragma";
                else if (s.Contains("(Built-in only)")) key = "builtinPragma";
                else if (s.Contains("\"RenderPipeline\"")) key = "tag";
                else if (s.Contains("HLSLPROGRAM") || s.Contains("ENDHLSL")) key = "hlsl";
                else if (s.Contains("FallbackError")) key = "fallback";
                else if (s.Contains("CGINCLUDE")) key = "cginclude";
                else key = block.Length > 0 ? block : "other";
                r[i] = Loc.Get("ui.shader.why." + key);
            }
            return r;
        }
    }
}
