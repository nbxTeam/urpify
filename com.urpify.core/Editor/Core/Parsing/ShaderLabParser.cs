using System;
using System.Collections.Generic;
using System.Text;

namespace Urpify.Parsing
{
    /// <summary>
    /// Structural parser for .shader files (ShaderLab plus the CG/HLSL blocks inside it).
    /// Never throws: any problem is recorded in <see cref="ShaderDocument.Errors"/> and parsing continues.
    /// </summary>
    public static class ShaderLabParser
    {
        /// <summary>ShaderLab render-state commands, so several on one line read as separate statements.</summary>
        static readonly System.Collections.Generic.HashSet<string> StateCommands = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            { "Cull", "ZTest", "ZWrite", "ZClip", "Blend", "BlendOp", "ColorMask", "Offset", "AlphaToMask", "Conservative", "Lighting",
              "SeparateSpecular", "ColorMaterial", "AlphaTest", "Name" };

        enum K { Word, String, Punct, Program }

        struct T
        {
            public K Kind;
            public string Text;
            public int Start, End, Line;
            public ProgramBlock Program;
            public bool Is(string s) => Kind == K.Punct && Text == s;
            public bool IsWord(string s) => Kind == K.Word && string.Equals(Text, s, StringComparison.OrdinalIgnoreCase);
            public string Unquoted => Kind == K.String && Text.Length >= 2 ? Text.Substring(1, Text.Length - 2) : Text;
        }

        public static ShaderDocument Parse(string text, string path = "") => Parse(new SourceText(text, path));

        public static ShaderDocument Parse(SourceText src)
        {
            var doc = new ShaderDocument { Source = src };
            try
            {
                var toks = Lex(src, doc);
                var p = new Parser(src, toks, doc);
                p.ParseFile();
                foreach (var prog in doc.Programs)
                {
                    prog.Unit = HlslScanner.Scan(src, prog.Content.Start, prog.Content.End);
                    foreach (var e in prog.Unit.Errors) doc.Errors.Add(e);
                }
            }
            catch (Exception e)
            {
                doc.Errors.Add(new ParseError("Internal parser error: " + e.GetType().Name + ": " + e.Message, 1));
            }
            return doc;
        }

        // ---------------------------------------------------------------- lexer

        static readonly string[] ProgramKeywords = { "CGPROGRAM", "CGINCLUDE", "HLSLPROGRAM", "HLSLINCLUDE", "GLSLPROGRAM", "GLSLINCLUDE" };

        static List<T> Lex(SourceText src, ShaderDocument doc)
        {
            var t = src.Text;
            int n = t.Length;
            var toks = new List<T>();
            int i = 0;
            while (i < n)
            {
                char c = t[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (c == '/' && i + 1 < n && t[i + 1] == '/') { while (i < n && t[i] != '\n') i++; continue; }
                if (c == '/' && i + 1 < n && t[i + 1] == '*')
                {
                    int close = t.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (close < 0) { doc.Errors.Add(new ParseError("Unclosed block comment", src.LineOf(i))); break; }
                    i = close + 2;
                    continue;
                }
                int s = i;
                if (c == '"')
                {
                    // ShaderLab strings may span lines: Unity compiles Poiyomi (display names holding multi-line JSON)
                    // and a shader whose name contains a line break (corpus: SpiralColors).
                    i++;
                    while (i < n && t[i] != '"') { if (t[i] == '\\') i++; i++; }
                    if (i >= n) doc.Errors.Add(new ParseError("Unclosed string", src.LineOf(s)));
                    if (i < n && t[i] == '"') i++;
                    toks.Add(new T { Kind = K.String, Text = t.Substring(s, Math.Min(i, n) - s), Start = s, End = Math.Min(i, n), Line = src.LineOf(s) });
                    continue;
                }
                if (IsWordChar(c))
                {
                    while (i < n && IsWordChar(t[i])) i++;
                    var word = t.Substring(s, i - s);
                    int kw = Array.FindIndex(ProgramKeywords, k => k == word);
                    if (kw >= 0)
                    {
                        var endKw = word.StartsWith("CG", StringComparison.Ordinal) ? "ENDCG" : word.StartsWith("HLSL", StringComparison.Ordinal) ? "ENDHLSL" : "ENDGLSL";
                        int endAt = FindWord(t, endKw, i);
                        var prog = new ProgramBlock
                        {
                            Language = word.StartsWith("CG", StringComparison.Ordinal) ? ProgramLanguage.CG : word.StartsWith("HLSL", StringComparison.Ordinal) ? ProgramLanguage.HLSL : ProgramLanguage.GLSL,
                            IsInclude = word.EndsWith("INCLUDE", StringComparison.Ordinal),
                            Line = src.LineOf(s),
                        };
                        if (endAt < 0)
                        {
                            doc.Errors.Add(new ParseError($"{word} without {endKw}", prog.Line));
                            prog.Content = new Span(i, n);
                            prog.Span = new Span(s, n);
                            i = n;
                        }
                        else
                        {
                            prog.Content = new Span(i, endAt);
                            prog.Span = new Span(s, endAt + endKw.Length);
                            i = endAt + endKw.Length;
                        }
                        toks.Add(new T { Kind = K.Program, Text = word, Start = s, End = i, Line = prog.Line, Program = prog });
                        continue;
                    }
                    toks.Add(new T { Kind = K.Word, Text = word, Start = s, End = i, Line = src.LineOf(s) });
                    continue;
                }
                i++;
                toks.Add(new T { Kind = K.Punct, Text = c.ToString(), Start = s, End = i, Line = src.LineOf(s) });
            }
            return toks;
        }

        static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '.';

        static int FindWord(string t, string word, int from)
        {
            int i = from;
            while (true)
            {
                i = t.IndexOf(word, i, StringComparison.Ordinal);
                if (i < 0) return -1;
                bool before = i == 0 || !IsWordChar(t[i - 1]);
                bool after = i + word.Length >= t.Length || !IsWordChar(t[i + word.Length]);
                if (before && after) return i;
                i += word.Length;
            }
        }

        // ---------------------------------------------------------------- parser

        sealed class Parser
        {
            readonly SourceText src;
            readonly List<T> toks;
            readonly ShaderDocument doc;
            int i;

            public Parser(SourceText src, List<T> toks, ShaderDocument doc) { this.src = src; this.toks = toks; this.doc = doc; }

            bool AtEnd => i >= toks.Count;
            T Cur => toks[i];
            T Peek(int k) => i + k < toks.Count ? toks[i + k] : default;
            void Error(string msg) => doc.Errors.Add(new ParseError(msg, AtEnd ? src.LineCount : Cur.Line));

            public void ParseFile()
            {
                while (!AtEnd && !Cur.IsWord("Shader")) i++;
                if (AtEnd) { doc.Errors.Add(new ParseError("No 'Shader' block", 1)); return; }
                doc.ShaderLine = Cur.Line;
                i++;
                if (!AtEnd && Cur.Kind == K.String) { doc.ShaderName = Cur.Unquoted; i++; }
                else Error("Shader name missing");
                if (AtEnd || !Cur.Is("{")) { Error("Expected '{' after Shader name"); return; }
                i++;
                ParseShaderBody(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                if (!AtEnd && Cur.Is("}")) i++;
                else Error("Shader block is not closed");
            }

            /// <summary>Contents of Shader { } or Category { } up to (not including) the closing brace.</summary>
            void ParseShaderBody(Dictionary<string, string> inheritedTags)
            {
                var categoryTags = new Dictionary<string, string>(inheritedTags, StringComparer.OrdinalIgnoreCase);
                while (!AtEnd && !Cur.Is("}"))
                {
                    var tk = Cur;
                    if (tk.Kind == K.Program) { AddProgram(tk.Program, -1, -1); i++; continue; }
                    if (tk.IsWord("Properties") && Peek(1).Is("{")) { var ps = tk.Start; i += 2; ParseProperties(); Expect("}"); doc.PropertiesSpan = new Span(ps, i > 0 ? toks[i - 1].End : ps); continue; }
                    if (tk.IsWord("SubShader") && Peek(1).Is("{")) { i += 2; ParseSubShader(categoryTags, tk); Expect("}"); continue; }
                    if (tk.IsWord("Category") && Peek(1).Is("{")) { i += 2; ParseShaderBody(categoryTags); Expect("}"); continue; }
                    if (tk.IsWord("Tags") && Peek(1).Is("{")) { i += 2; ParseTags(categoryTags); Expect("}"); continue; }
                    if (tk.IsWord("Fallback") || tk.IsWord("FallBack"))
                    {
                        doc.FallbackLine = tk.Line;
                        i++;
                        if (!AtEnd && (Cur.Kind == K.String || Cur.Kind == K.Word)) { doc.Fallback = Cur.Kind == K.String ? Cur.Unquoted : Cur.Text; i++; }
                        // Optional LOD argument: Fallback "X" 100 -- skip to end of line
                        SkipRestOfLine(tk.Line);
                        continue;
                    }
                    if (tk.IsWord("CustomEditor") || tk.IsWord("CustomEditorForRenderPipeline"))
                    {
                        i++;
                        if (!AtEnd && Cur.Kind == K.String) { if (doc.CustomEditor.Length == 0) doc.CustomEditor = Cur.Unquoted; i++; }
                        SkipRestOfLine(tk.Line);
                        continue;
                    }
                    SkipStatementOrBlock();
                }
            }

            void ParseSubShader(Dictionary<string, string> inheritedTags, T head)
            {
                var sub = new SubShaderInfo { Index = doc.SubShaders.Count, Line = head.Line };
                foreach (var kv in inheritedTags) sub.Tags[kv.Key] = kv.Value;
                doc.SubShaders.Add(sub);
                int start = head.Start;
                while (!AtEnd && !Cur.Is("}"))
                {
                    var tk = Cur;
                    if (tk.Kind == K.Program) { AddProgram(tk.Program, sub.Index, -1); sub.Programs.Add(tk.Program); i++; continue; }
                    if (tk.IsWord("Tags") && Peek(1).Is("{")) { if (sub.TagsOpen < 0) sub.TagsOpen = Peek(1).End; i += 2; ParseTags(sub.Tags); Expect("}"); continue; }
                    if (tk.IsWord("LOD")) { i++; if (!AtEnd && int.TryParse(Cur.Text, out var lod)) { sub.Lod = lod; i++; } continue; }
                    if (tk.IsWord("Pass") && Peek(1).Is("{")) { i += 2; ParsePass(sub, tk); Expect("}"); continue; }
                    if (tk.IsWord("GrabPass") && Peek(1).Is("{"))
                    {
                        var gp = new PassInfo { Kind = PassKind.GrabPass, Line = tk.Line };
                        i += 2;
                        while (!AtEnd && !Cur.Is("}"))
                        {
                            if (Cur.Kind == K.String) { gp.GrabTextureName = Cur.Unquoted; i++; continue; }
                            if (Cur.IsWord("Tags") && Peek(1).Is("{")) { i += 2; ParseTags(gp.Tags); Expect("}"); continue; }
                            if (Cur.IsWord("Name")) { i++; if (!AtEnd && Cur.Kind == K.String) { gp.Name = Cur.Unquoted; i++; } continue; }
                            SkipStatementOrBlock();
                        }
                        gp.Span = new Span(tk.Start, AtEnd ? src.Length : Cur.End);
                        Expect("}");
                        sub.Passes.Add(gp);
                        continue;
                    }
                    if (tk.IsWord("UsePass"))
                    {
                        var up = new PassInfo { Kind = PassKind.UsePass, Line = tk.Line };
                        i++;
                        if (!AtEnd && Cur.Kind == K.String) { up.UsePassName = Cur.Unquoted; up.Span = new Span(tk.Start, Cur.End); i++; }
                        else Error("UsePass without a name");
                        sub.Passes.Add(up);
                        continue;
                    }
                    if (tk.IsWord("PackageRequirements") && Peek(1).Is("{")) { sub.HasPackageRequirements = true; SkipStatementOrBlock(); continue; }
                    if (tk.Kind == K.Word && !Peek(1).Is("{")) { sub.States.Add(ReadState()); continue; }
                    SkipStatementOrBlock();
                }
                sub.Span = new Span(start, AtEnd ? src.Length : Cur.End);
                // Unity applies a LightMode set in the SubShader's Tags to passes without their own
                // (ToonWaterShader ToonLit.shader compiles to FORWARDBASE passes this way).
                if (sub.Tags.TryGetValue("LightMode", out var subLightMode))
                    foreach (var p in sub.Passes)
                        if (p.Kind == PassKind.Pass && !p.Tags.ContainsKey("LightMode")) p.Tags["LightMode"] = subLightMode;
            }

            void ParsePass(SubShaderInfo sub, T head)
            {
                var pass = new PassInfo { Kind = PassKind.Pass, Line = head.Line };
                int passIndex = sub.Passes.Count;
                while (!AtEnd && !Cur.Is("}"))
                {
                    var tk = Cur;
                    if (tk.Kind == K.Program)
                    {
                        AddProgram(tk.Program, sub.Index, tk.Program.IsInclude ? -1 : passIndex);
                        if (!tk.Program.IsInclude)
                        {
                            if (pass.Program != null) Error("Pass has more than one program block");
                            pass.Program = tk.Program;
                        }
                        i++;
                        continue;
                    }
                    if (tk.IsWord("Name")) { i++; if (!AtEnd && Cur.Kind == K.String) { pass.Name = Cur.Unquoted; i++; } continue; }
                    if (tk.IsWord("Tags") && Peek(1).Is("{")) { if (pass.TagsOpen < 0) pass.TagsOpen = Peek(1).End; i += 2; ParseTags(pass.Tags, pass.TagSpans); Expect("}"); continue; }
                    if (tk.Kind == K.Word && !Peek(1).Is("{")) { pass.States.Add(ReadState()); continue; }
                    SkipStatementOrBlock();
                }
                pass.Span = new Span(head.Start, AtEnd ? src.Length : Cur.End);
                sub.Passes.Add(pass);
            }

            void ParseTags(Dictionary<string, string> into, Dictionary<string, Span> spans = null)
            {
                while (!AtEnd && !Cur.Is("}"))
                {
                    if (Cur.Kind == K.String && Peek(1).Is("=") && (Peek(2).Kind == K.String || Peek(2).Kind == K.Word))
                    {
                        into[Cur.Unquoted] = Peek(2).Kind == K.String ? Peek(2).Unquoted : Peek(2).Text;
                        if (spans != null) spans[Cur.Unquoted] = new Span(Cur.Start, Peek(2).End);
                        i += 3;
                        continue;
                    }
                    if (Cur.Kind == K.Program) return; // malformed: let the caller see the program
                    i++;
                }
            }

            void ParseProperties()
            {
                while (!AtEnd && !Cur.Is("}"))
                {
                    int startTok = i;
                    var attrs = new List<string>();
                    while (!AtEnd && Cur.Is("["))
                    {
                        int s = Cur.Start;
                        int depth = 0;
                        for (; !AtEnd; i++)
                        {
                            if (Cur.Is("[")) depth++;
                            else if (Cur.Is("]")) { depth--; if (depth == 0) { attrs.Add(src.Slice(s + 1, Cur.Start).Trim()); i++; break; } }
                        }
                    }
                    if (AtEnd || Cur.Is("}")) break;
                    if (Cur.Kind != K.Word || !Peek(1).Is("("))
                    {
                        Error("Unrecognized property declaration");
                        SkipRestOfLine(Cur.Line);
                        continue;
                    }
                    var prop = new ShaderProperty { Name = Cur.Text, Line = Cur.Line, Attributes = attrs.ToArray() };
                    int propStart = toks[startTok].Start;
                    i += 2; // name (
                    if (!AtEnd && Cur.Kind == K.String) { prop.DisplayName = Cur.Unquoted; i++; }
                    if (!AtEnd && Cur.Is(",")) i++;
                    // Type up to the matching ')'.
                    int typeStart = AtEnd ? src.Length : Cur.Start;
                    int depth2 = 1;
                    while (!AtEnd)
                    {
                        if (Cur.Is("(")) depth2++;
                        else if (Cur.Is(")")) { depth2--; if (depth2 == 0) break; }
                        i++;
                    }
                    if (AtEnd) { Error($"Property {prop.Name} is not closed"); break; }
                    prop.Type = src.Slice(typeStart, Cur.Start).Trim();
                    i++; // )
                    if (!AtEnd && Cur.Is("="))
                    {
                        i++;
                        int defStart = AtEnd ? src.Length : Cur.Start;
                        int defEnd = defStart;
                        if (!AtEnd && Cur.Is("("))
                        {
                            int d = 0;
                            for (; !AtEnd; i++) { if (Cur.Is("(")) d++; else if (Cur.Is(")")) { d--; if (d == 0) { defEnd = Cur.End; i++; break; } } }
                        }
                        else if (!AtEnd && Cur.Kind == K.String)
                        {
                            defEnd = Cur.End;
                            i++;
                            if (!AtEnd && Cur.Is("{"))
                            {
                                int d = 0;
                                for (; !AtEnd; i++) { if (Cur.Is("{")) d++; else if (Cur.Is("}")) { d--; if (d == 0) { defEnd = Cur.End; i++; break; } } }
                            }
                        }
                        else
                        {
                            if (!AtEnd && Cur.Is("-")) i++;
                            if (!AtEnd && Cur.Kind == K.Word) { defEnd = Cur.End; i++; }
                        }
                        prop.Default = src.Slice(defStart, defEnd).Trim();
                    }
                    prop.Span = new Span(propStart, i > 0 ? toks[i - 1].End : propStart);
                    doc.Properties.Add(prop);
                }
            }

            StateStatement ReadState()
            {
                var head = Cur;
                int line = head.Line;
                i++;
                int end = head.End;
                // Several commands may share a line ("ZTest Always Cull Off ZWrite Off"): a known command word starts the next one.
                while (!AtEnd && Cur.Line == line && !Cur.Is("}") && !Cur.Is("{") && Cur.Kind != K.Program
                       && !(Cur.Kind == K.Word && StateCommands.Contains(Cur.Text))) { end = Cur.End; i++; }
                return new StateStatement { Name = head.Text, Args = src.Slice(head.End, end).Trim(), Line = line, Span = new Span(head.Start, end) };
            }

            void AddProgram(ProgramBlock p, int sub, int pass)
            {
                p.SubShaderIndex = sub;
                p.PassIndex = pass;
                doc.Programs.Add(p);
            }

            void Expect(string punct)
            {
                if (!AtEnd && Cur.Is(punct)) { i++; return; }
                Error($"Expected '{punct}'");
            }

            void SkipRestOfLine(int line)
            {
                while (!AtEnd && Cur.Line == line && !Cur.Is("}") && !Cur.Is("{") && Cur.Kind != K.Program) i++;
            }

            /// <summary>Skips "Word args... { ... }" or "Word args..." to the end of its line.</summary>
            void SkipStatementOrBlock()
            {
                if (AtEnd) return;
                if (Cur.Is("{")) { SkipBlock(); return; }
                if (Cur.Kind == K.Program) { i++; return; }
                int line = Cur.Line;
                i++;
                while (!AtEnd && !Cur.Is("}") && Cur.Kind != K.Program)
                {
                    if (Cur.Is("{")) { SkipBlock(); return; }
                    if (Cur.Line != line) return;
                    i++;
                }
            }

            void SkipBlock()
            {
                int depth = 0;
                for (; !AtEnd; i++)
                {
                    if (Cur.Is("{")) depth++;
                    else if (Cur.Is("}")) { depth--; if (depth == 0) { i++; return; } }
                    else if (Cur.Kind == K.Program) { AddProgram(Cur.Program, -1, -1); }
                }
                Error("Unclosed block");
            }
        }

        /// <summary>Debug view of the parsed structure; used by tests and the Code tab.</summary>
        public static string Describe(ShaderDocument d)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Shader \"{d.ShaderName}\" props={d.Properties.Count} fallback=\"{d.Fallback}\" errors={d.Errors.Count}");
            foreach (var s in d.SubShaders)
            {
                sb.AppendLine($"  SubShader[{s.Index}] rp=\"{s.RenderPipeline}\" lod={s.Lod} programs={s.Programs.Count}");
                foreach (var p in s.Passes)
                    sb.AppendLine($"    {p.Kind} \"{p.Name}\" LightMode=\"{p.LightMode}\" program={(p.Program != null ? p.Program.Keyword : "-")} grab=\"{p.GrabTextureName}\" use=\"{p.UsePassName}\"");
            }
            return sb.ToString();
        }
    }
}
