using System;
using System.Collections.Generic;
using System.Text;

namespace Urpify.Parsing
{
    /// <summary>
    /// Structural scanner for CG/HLSL code: directives, structs, functions, top-level declarations and identifiers.
    /// Not a compiler. Conditional compilation is recorded, not evaluated, so code in every branch is scanned.
    /// Never throws: problems go to <see cref="HlslUnit.Errors"/>.
    /// </summary>
    public static class HlslScanner
    {
        internal enum TokKind { Ident, Number, String, Punct }

        internal struct Tok
        {
            public TokKind Kind;
            public string Text;
            public int Start, End, Line;
            public bool Is(string s) => Text == s;
            public override string ToString() => Text;
        }

        public static HlslUnit Scan(SourceText src) => Scan(src, 0, src.Length);

        public static HlslUnit Scan(SourceText src, int start, int end)
        {
            var unit = new HlslUnit();
            try
            {
                var toks = Tokenize(src, start, end, unit);
                Structure(src, toks, unit);
            }
            catch (Exception e)
            {
                unit.Errors.Add(new ParseError("Internal scanner error: " + e.Message, src.LineOf(start)));
            }
            if (!unit.Errors.Exists(e => e.Message.StartsWith("Unclosed struct", StringComparison.Ordinal))) return unit;
            // Legal code can open a block in each branch of an #if and close it after #endif (QA-011: "Unclosed struct v2f"
            // on a struct whose head is written in both branches). Read again with the first branch of every #if only, as a
            // reader would; used only when the whole-text reading does not balance, so other shaders read as before.
            try
            {
                var again = new HlslUnit();
                var toks = Tokenize(src, start, end, again);
                var skip = AlternateBranches(again.Directives);
                Structure(src, toks.FindAll(t => !skip.Exists(r => t.Start >= r.from && t.Start < r.to)), again);
                if (!again.Errors.Exists(e => e.Message.StartsWith("Unclosed", StringComparison.Ordinal))) return again;
            }
            catch { }
            return unit;
        }

        /// <summary>Text ranges of the #elif and #else branches (everything but the first branch of each #if).</summary>
        static List<(int from, int to)> AlternateBranches(List<HlslDirective> directives)
        {
            var ranges = new List<(int, int)>();
            var stack = new Stack<int>(); // start of the skipped part, or -1 while in the first branch
            foreach (var d in directives)
            {
                switch (d.Kind)
                {
                    case DirectiveKind.If: case DirectiveKind.Ifdef: case DirectiveKind.Ifndef: stack.Push(-1); break;
                    case DirectiveKind.Elif: case DirectiveKind.Else:
                        if (stack.Count > 0 && stack.Peek() < 0) { stack.Pop(); stack.Push(d.Span.End); }
                        break;
                    case DirectiveKind.Endif:
                        if (stack.Count == 0) break;
                        var from = stack.Pop();
                        if (from >= 0) ranges.Add((from, d.Span.Start));
                        break;
                }
            }
            return ranges;
        }

        // ---------------------------------------------------------------- tokenizer

        internal static List<Tok> Tokenize(SourceText src, int start, int end, HlslUnit unit)
        {
            var t = src.Text;
            var toks = new List<Tok>();
            int i = start;
            int condDepth = 0;
            bool lineStart = true;
            while (i < end)
            {
                char c = t[i];
                if (c == '\n') { lineStart = true; i++; continue; }
                if (c == ' ' || c == '\t' || c == '\r' || c == '\f' || c == '\v') { i++; continue; }
                if (c == '/' && i + 1 < end && t[i + 1] == '/')
                {
                    while (i < end && t[i] != '\n') i++;
                    continue;
                }
                if (c == '/' && i + 1 < end && t[i + 1] == '*')
                {
                    int close = t.IndexOf("*/", i + 2, end - (i + 2), StringComparison.Ordinal);
                    if (close < 0) { unit.Errors.Add(new ParseError("Unclosed block comment", src.LineOf(i))); i = end; break; }
                    i = close + 2;
                    continue;
                }
                if (c == '#' && lineStart)
                {
                    i = ReadDirective(src, i, end, unit, ref condDepth);
                    lineStart = true;
                    continue;
                }
                lineStart = false;
                int s = i;
                if (c == '"')
                {
                    i++;
                    while (i < end && t[i] != '"' && t[i] != '\n') { if (t[i] == '\\') i++; i++; }
                    if (i < end && t[i] == '"') i++;
                    toks.Add(new Tok { Kind = TokKind.String, Text = t.Substring(s, i - s), Start = s, End = i, Line = src.LineOf(s) });
                    continue;
                }
                if (IsIdentStart(c))
                {
                    while (i < end && IsIdentPart(t[i])) i++;
                    var text = t.Substring(s, i - s);
                    int line = src.LineOf(s);
                    toks.Add(new Tok { Kind = TokKind.Ident, Text = text, Start = s, End = i, Line = line });
                    if (!unit.Identifiers.TryGetValue(text, out var lines)) unit.Identifiers[text] = lines = new List<int>();
                    if (lines.Count == 0 || lines[lines.Count - 1] != line) lines.Add(line);
                    continue;
                }
                if (char.IsDigit(c) || (c == '.' && i + 1 < end && char.IsDigit(t[i + 1])))
                {
                    while (i < end && (char.IsLetterOrDigit(t[i]) || t[i] == '.' ||
                                       ((t[i] == '+' || t[i] == '-') && (t[i - 1] == 'e' || t[i - 1] == 'E') && !IsHex(t, s)))) i++;
                    toks.Add(new Tok { Kind = TokKind.Number, Text = t.Substring(s, i - s), Start = s, End = i, Line = src.LineOf(s) });
                    continue;
                }
                i++;
                toks.Add(new Tok { Kind = TokKind.Punct, Text = c.ToString(), Start = s, End = i, Line = src.LineOf(s) });
            }
            return toks;
        }

        static bool IsHex(string t, int s) => s + 1 < t.Length && t[s] == '0' && (t[s + 1] == 'x' || t[s + 1] == 'X');
        internal static bool IsIdentStart(char c) => c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        internal static bool IsIdentPart(char c) => IsIdentStart(c) || (c >= '0' && c <= '9');

        static int ReadDirective(SourceText src, int i, int end, HlslUnit unit, ref int condDepth)
        {
            var t = src.Text;
            int start = i;
            var sb = new StringBuilder();
            i++; // '#'
            while (i < end)
            {
                char c = t[i];
                if (c == '\\' && i + 1 < end && (t[i + 1] == '\n' || (t[i + 1] == '\r' && i + 2 < end && t[i + 2] == '\n')))
                {
                    i += t[i + 1] == '\r' ? 3 : 2;
                    sb.Append(' ');
                    continue;
                }
                if (c == '\n') break;
                if (c == '/' && i + 1 < end && t[i + 1] == '/') { while (i < end && t[i] != '\n') i++; break; }
                if (c == '/' && i + 1 < end && t[i + 1] == '*')
                {
                    int close = t.IndexOf("*/", i + 2, end - (i + 2), StringComparison.Ordinal);
                    i = close < 0 ? end : close + 2;
                    sb.Append(' ');
                    continue;
                }
                sb.Append(c);
                i++;
            }
            var body = sb.ToString().Trim();
            int nameEnd = 0;
            while (nameEnd < body.Length && IsIdentPart(body[nameEnd])) nameEnd++;
            var name = body.Substring(0, nameEnd);
            var rest = body.Substring(nameEnd).Trim();
            var d = new HlslDirective { Name = name, Rest = rest, Line = src.LineOf(start), Span = new Span(start, i) };
            switch (name)
            {
                case "pragma": d.Kind = DirectiveKind.Pragma; break;
                case "include": d.Kind = DirectiveKind.Include; break;
                case "include_with_pragmas": d.Kind = DirectiveKind.Include; break;
                case "define": d.Kind = DirectiveKind.Define; break;
                case "undef": d.Kind = DirectiveKind.Undef; break;
                case "if": d.Kind = DirectiveKind.If; break;
                case "ifdef": d.Kind = DirectiveKind.Ifdef; break;
                case "ifndef": d.Kind = DirectiveKind.Ifndef; break;
                case "elif": d.Kind = DirectiveKind.Elif; break;
                case "else": d.Kind = DirectiveKind.Else; break;
                case "endif": d.Kind = DirectiveKind.Endif; break;
                default: d.Kind = DirectiveKind.Other; break;
            }
            if (d.Kind == DirectiveKind.Endif) condDepth = Math.Max(0, condDepth - 1);
            d.ConditionalDepth = d.Kind == DirectiveKind.If || d.Kind == DirectiveKind.Ifdef || d.Kind == DirectiveKind.Ifndef
                ? condDepth : Math.Max(0, d.Kind == DirectiveKind.Elif || d.Kind == DirectiveKind.Else ? condDepth - 1 : condDepth);
            if (d.Kind == DirectiveKind.If || d.Kind == DirectiveKind.Ifdef || d.Kind == DirectiveKind.Ifndef) condDepth++;
            unit.Directives.Add(d);

            switch (d.Kind)
            {
                case DirectiveKind.Pragma:
                {
                    var words = SplitWords(rest);
                    unit.Pragmas.Add(new HlslPragma
                    {
                        Name = words.Count > 0 ? words[0] : "",
                        Args = words.Count > 1 ? words.GetRange(1, words.Count - 1).ToArray() : Array.Empty<string>(),
                        Line = d.Line,
                        ConditionalDepth = d.ConditionalDepth,
                        Raw = rest,
                    });
                    break;
                }
                case DirectiveKind.Include:
                {
                    var p = rest.Trim();
                    if (p.Length >= 2 && (p[0] == '"' || p[0] == '<'))
                    {
                        char close = p[0] == '"' ? '"' : '>';
                        int e = p.IndexOf(close, 1);
                        p = e > 0 ? p.Substring(1, e - 1) : p.Substring(1);
                    }
                    unit.Includes.Add(new HlslInclude { Path = p, Line = d.Line, ConditionalDepth = d.ConditionalDepth });
                    break;
                }
                case DirectiveKind.Define:
                {
                    int k = 0;
                    while (k < rest.Length && IsIdentPart(rest[k])) k++;
                    var macro = rest.Substring(0, k);
                    if (macro.Length > 0) unit.Defines[macro] = rest.Substring(k).Trim();
                    // Identifiers used in macro bodies count as uses (e.g. #define DEPTH _CameraDepthTexture).
                    foreach (var w in IdentifiersIn(rest))
                    {
                        if (!unit.Identifiers.TryGetValue(w, out var lines)) unit.Identifiers[w] = lines = new List<int>();
                        if (lines.Count == 0 || lines[lines.Count - 1] != d.Line) lines.Add(d.Line);
                    }
                    break;
                }
            }
            return i;
        }

        static IEnumerable<string> IdentifiersIn(string s)
        {
            int i = 0;
            while (i < s.Length)
            {
                if (IsIdentStart(s[i]))
                {
                    int b = i;
                    while (i < s.Length && IsIdentPart(s[i])) i++;
                    yield return s.Substring(b, i - b);
                }
                else if (char.IsDigit(s[i])) { while (i < s.Length && IsIdentPart(s[i])) i++; }
                else i++;
            }
        }

        /// <summary>Splits a pragma line into words; "a:b" stays one word, quoted strings stay whole.</summary>
        internal static List<string> SplitWords(string s)
        {
            var words = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                if (char.IsWhiteSpace(s[i])) { i++; continue; }
                int b = i;
                if (s[i] == '"')
                {
                    i++;
                    while (i < s.Length && s[i] != '"') i++;
                    if (i < s.Length) i++;
                }
                else
                {
                    while (i < s.Length && !char.IsWhiteSpace(s[i])) i++;
                }
                words.Add(s.Substring(b, i - b));
            }
            return words;
        }

        // ---------------------------------------------------------------- structure

        static void Structure(SourceText src, List<Tok> toks, HlslUnit unit)
        {
            int i = 0;
            int n = toks.Count;
            while (i < n)
            {
                var tk = toks[i];
                // Stray closing brace at top level (e.g. the end of a cbuffer block).
                if (tk.Is("}") || tk.Is(";")) { i++; continue; }

                if (tk.Kind == TokKind.Ident && (tk.Text == "struct") && i + 2 < n && toks[i + 1].Kind == TokKind.Ident && toks[i + 2].Is("{"))
                {
                    i = ReadStruct(src, toks, i, unit);
                    continue;
                }
                if (tk.Kind == TokKind.Ident && (tk.Text == "cbuffer" || tk.Text == "tbuffer") && i + 1 < n)
                {
                    // "cbuffer Name : register(b0) {" -> skip the header; members are scanned as declarations.
                    int j = i + 1;
                    while (j < n && !toks[j].Is("{") && !toks[j].Is(";")) j++;
                    i = j + 1;
                    continue;
                }

                // Collect one statement.
                int s = i;
                int paren = 0, bracket = 0;
                bool isFunction = false;
                int j2 = i;
                for (; j2 < n; j2++)
                {
                    var x = toks[j2];
                    if (x.Is("(")) paren++;
                    else if (x.Is(")")) paren = Math.Max(0, paren - 1);
                    else if (x.Is("[")) bracket++;
                    else if (x.Is("]")) bracket = Math.Max(0, bracket - 1);
                    else if (paren == 0 && bracket == 0 && x.Is(";")) break;
                    else if (paren == 0 && bracket == 0 && x.Is("{")) { isFunction = LooksLikeFunction(toks, s, j2); break; }
                    else if (paren == 0 && bracket == 0 && x.Is("}")) break;

                    // A macro statement with no semicolon ends at the line break: CBUFFER_START(X), CBUFFER_END.
                    if (paren == 0 && bracket == 0 && j2 + 1 < n && toks[j2 + 1].Line > x.Line && IsMacroStatement(toks, s, j2))
                    {
                        j2++;
                        AddDeclaration(src, toks, s, j2, unit);
                        s = -1;
                        break;
                    }
                }
                if (s < 0) { i = j2; continue; }
                if (j2 >= n)
                {
                    if (j2 > s) AddDeclaration(src, toks, s, j2, unit);
                    break;
                }
                if (toks[j2].Is("{"))
                {
                    int close = MatchBrace(toks, j2);
                    if (close < 0)
                    {
                        unit.Errors.Add(new ParseError("Unbalanced '{'", toks[j2].Line));
                        close = n - 1;
                    }
                    if (isFunction) AddFunction(src, toks, s, j2, close, unit);
                    else AddDeclaration(src, toks, s, close + 1, unit); // e.g. an initializer list or unknown block
                    i = close + 1;
                    continue;
                }
                if (toks[j2].Is("}")) { if (j2 > s) AddDeclaration(src, toks, s, j2, unit); i = j2 + 1; continue; }
                AddDeclaration(src, toks, s, j2 + 1, unit);
                i = j2 + 1;
            }
        }

        static bool IsMacroStatement(List<Tok> toks, int s, int j)
        {
            var head = toks[s];
            if (head.Kind != TokKind.Ident || !IsAllCaps(head.Text)) return false;
            if (j == s) return true; // bare macro, e.g. CBUFFER_END
            // HEAD(...) with the closing paren at j
            return s + 1 < toks.Count && toks[s + 1].Is("(") && toks[j].Is(")");
        }

        static bool IsAllCaps(string s)
        {
            bool letter = false;
            foreach (var c in s)
            {
                if (c >= 'a' && c <= 'z') return false;
                if (c >= 'A' && c <= 'Z') letter = true;
            }
            return letter;
        }

        static bool LooksLikeFunction(List<Tok> toks, int s, int brace)
        {
            // [attributes] modifiers type name ( params ) [: semantic] {
            int k = brace - 1;
            if (k >= s + 1 && toks[k].Kind == TokKind.Ident && toks[k - 1].Is(":")) k -= 2;
            if (k < s || !toks[k].Is(")")) return false;
            int depth = 0;
            for (; k >= s; k--)
            {
                if (toks[k].Is(")")) depth++;
                else if (toks[k].Is("(")) { depth--; if (depth == 0) break; }
            }
            if (k <= s) return false;
            if (toks[k - 1].Kind != TokKind.Ident) return false;
            // Needs a return type before the name.
            int typeTok = k - 2;
            while (typeTok >= s && toks[typeTok].Is("]"))
            {
                int d = 0;
                for (; typeTok >= s; typeTok--) { if (toks[typeTok].Is("]")) d++; else if (toks[typeTok].Is("[")) { d--; if (d == 0) break; } }
                typeTok--;
            }
            return typeTok >= s && toks[typeTok].Kind == TokKind.Ident;
        }

        static int MatchBrace(List<Tok> toks, int open)
        {
            int depth = 0;
            for (int k = open; k < toks.Count; k++)
            {
                if (toks[k].Is("{")) depth++;
                else if (toks[k].Is("}")) { depth--; if (depth == 0) return k; }
            }
            return -1;
        }

        static void AddDeclaration(SourceText src, List<Tok> toks, int s, int e, HlslUnit unit)
        {
            if (e <= s) return;
            var first = toks[s];
            var last = toks[Math.Min(e, toks.Count) - 1];
            var d = new HlslDeclaration
            {
                Line = first.Line,
                Span = new Span(first.Start, last.End),
                Text = src.Slice(first.Start, last.End),
                Head = first.Kind == TokKind.Ident ? first.Text : "",
            };
            // Name: the last identifier before '=', ':', '[', '(' or ';'.
            for (int k = s; k < e; k++)
            {
                var x = toks[k];
                if (x.Is("=") || x.Is(":") || x.Is("[") || x.Is(";") || x.Is("(") || x.Is("{")) break;
                if (x.Kind == TokKind.Ident && k > s) d.Name = x.Text;
            }
            unit.Declarations.Add(d);
        }

        static int ReadStruct(SourceText src, List<Tok> toks, int i, HlslUnit unit)
        {
            var st = new HlslStruct { Name = toks[i + 1].Text, Line = toks[i].Line };
            int open = i + 2;
            int close = MatchBrace(toks, open);
            if (close < 0) { unit.Errors.Add(new ParseError($"Unclosed struct {st.Name}", st.Line)); close = toks.Count - 1; }
            int k = open + 1;
            while (k < close)
            {
                int e = k;
                bool macroField = false;
                while (e < close && !toks[e].Is(";"))
                {
                    // A macro field without a semicolon ends at its line break: SHADOW_COORDS(2), UNITY_FOG_COORDS(1).
                    if (e + 1 < close && toks[e + 1].Line > toks[e].Line && IsMacroStatement(toks, k, e)) { e++; macroField = true; break; }
                    e++;
                }
                // [modifiers] type name [array] [: SEMANTIC] ;
                var parts = new List<Tok>();
                for (int m = k; m < e; m++) parts.Add(toks[m]);
                int colon = parts.FindIndex(p => p.Is(":"));
                var decl = colon >= 0 ? parts.GetRange(0, colon) : parts;
                var idents = decl.FindAll(p => p.Kind == TokKind.Ident);
                if (idents.Count >= 2)
                {
                    st.Fields.Add(new HlslField
                    {
                        Type = idents[idents.Count - 2].Text,
                        Name = idents[idents.Count - 1].Text,
                        Semantic = colon >= 0 && colon + 1 < parts.Count ? parts[colon + 1].Text : "",
                        Line = parts[0].Line,
                    });
                }
                else if (idents.Count == 1 && parts.Count > 0)
                {
                    // Macro field, e.g. UNITY_FOG_COORDS(1) or INTERNAL_DATA
                    st.Fields.Add(new HlslField { Type = "", Name = idents[0].Text, Line = parts[0].Line });
                }
                k = macroField ? e : e + 1;
            }
            st.Span = new Span(toks[i].Start, toks[close].End);
            unit.Structs.Add(st);
            int next = close + 1;
            if (next < toks.Count && toks[next].Is(";")) next++;
            return next;
        }

        static void AddFunction(SourceText src, List<Tok> toks, int s, int brace, int close, HlslUnit unit)
        {
            int k = brace - 1;
            string semantic = "";
            if (toks[k].Kind == TokKind.Ident && toks[k - 1].Is(":")) { semantic = toks[k].Text; k -= 2; }
            int closeParen = k;
            int depth = 0;
            for (; k >= s; k--)
            {
                if (toks[k].Is(")")) depth++;
                else if (toks[k].Is("(")) { depth--; if (depth == 0) break; }
            }
            int openParen = k;
            var nameTok = toks[openParen - 1];
            var ret = new StringBuilder();
            for (int m = s; m < openParen - 1; m++)
            {
                if (toks[m].Is("[")) { int d = 0; for (; m < openParen - 1; m++) { if (toks[m].Is("[")) d++; else if (toks[m].Is("]")) { d--; if (d == 0) break; } } continue; }
                if (ret.Length > 0) ret.Append(' ');
                ret.Append(toks[m].Text);
            }
            var fn = new HlslFunction
            {
                Name = nameTok.Text,
                ReturnType = ret.ToString(),
                Semantic = semantic,
                Line = nameTok.Line,
                Span = new Span(toks[s].Start, toks[close].End),
                Body = new Span(toks[brace].End, toks[close].Start),
            };
            // Parameters split on top-level commas.
            int ps = openParen + 1;
            int pd = 0;
            for (int m = openParen + 1; m <= closeParen; m++)
            {
                if (toks[m].Is("(")) pd++;
                else if (toks[m].Is(")") && m != closeParen) pd--;
                if ((toks[m].Is(",") && pd == 0) || m == closeParen)
                {
                    if (m > ps) fn.Params.Add(ParseParam(toks, ps, m));
                    ps = m + 1;
                }
            }
            for (int m = brace + 1; m < close; m++)
                if (toks[m].Kind == TokKind.Ident) fn.BodyIdentifiers.Add(toks[m].Text);
            unit.Functions.Add(fn);
        }

        static readonly HashSet<string> ParamModifiers = new HashSet<string>(StringComparer.Ordinal)
            { "in", "out", "inout", "uniform", "const", "point", "line", "triangle", "lineadj", "triangleadj", "precise", "nointerpolation", "linear", "centroid", "noperspective", "sample" };

        static HlslParam ParseParam(List<Tok> toks, int s, int e)
        {
            var p = new HlslParam();
            var mods = new StringBuilder();
            var idents = new List<string>();
            for (int m = s; m < e; m++)
            {
                var x = toks[m];
                if (x.Is(":")) { if (m + 1 < e) p.Semantic = toks[m + 1].Text; break; }
                if (x.Is("=")) break;
                if (x.Kind != TokKind.Ident) continue;
                if (ParamModifiers.Contains(x.Text) && idents.Count == 0) { if (mods.Length > 0) mods.Append(' '); mods.Append(x.Text); continue; }
                idents.Add(x.Text);
            }
            p.Modifiers = mods.ToString();
            if (idents.Count >= 2) { p.Type = idents[idents.Count - 2]; p.Name = idents[idents.Count - 1]; }
            else if (idents.Count == 1) p.Type = idents[0];
            return p;
        }
    }
}
