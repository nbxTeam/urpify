using System;
using System.Collections.Generic;

namespace Urpify.Parsing
{
    /// <summary>
    /// Token-level view of a C# file for rules: identifiers, string literals and method declarations with lines.
    /// Comments are dropped, so commented-out code never triggers a rule. Not a C# parser.
    /// </summary>
    public sealed class CSharpFile
    {
        public enum TokKind { Ident, String, Number, Punct }

        public struct Tok
        {
            public TokKind Kind;
            public string Text;
            public int Line;
            /// <summary>Offset of the token in the source; <see cref="End"/> is exclusive.</summary>
            public int Start, End;
            public bool Is(string s) => Text == s && Kind != TokKind.String;
            public override string ToString() => Text;
        }

        public sealed class Method
        {
            public string Name = "";
            public int Line;
            /// <summary>Token range of the body, exclusive end; -1 when the method has no body.</summary>
            public int BodyStart = -1, BodyEnd = -1;
            /// <summary>Attributes directly before the declaration, e.g. "ImageEffectOpaque".</summary>
            public List<string> Attributes = new List<string>();
        }

        public readonly SourceText Source;
        public readonly List<Tok> Tokens = new List<Tok>();
        public readonly List<Method> Methods = new List<Method>();
        public readonly List<ParseError> Errors = new List<ParseError>();

        CSharpFile(SourceText src) { Source = src; }

        public static CSharpFile Parse(string text, string path = "")
        {
            var f = new CSharpFile(new SourceText(text, path));
            try
            {
                f.Tokenize();
                f.FindMethods();
            }
            catch (Exception e)
            {
                f.Errors.Add(new ParseError("Internal C# scanner error: " + e.Message, 1));
            }
            return f;
        }

        /// <summary>Index of the first token sequence equal to <paramref name="pattern"/> at or after <paramref name="from"/>, or -1.</summary>
        public int Find(int from, params string[] pattern)
        {
            for (int i = Math.Max(0, from); i + pattern.Length <= Tokens.Count; i++)
            {
                bool ok = true;
                for (int k = 0; k < pattern.Length && ok; k++)
                    ok = Tokens[i + k].Kind != TokKind.String && Tokens[i + k].Text == pattern[k];
                if (ok) return i;
            }
            return -1;
        }

        public IEnumerable<int> FindAll(params string[] pattern)
        {
            int i = -1;
            while ((i = Find(i + 1, pattern)) >= 0) yield return i;
        }

        public bool HasIdentifier(string name)
        {
            foreach (var t in Tokens) if (t.Kind == TokKind.Ident && t.Text == name) return true;
            return false;
        }

        public int FirstLineOf(string identifier)
        {
            foreach (var t in Tokens) if (t.Kind == TokKind.Ident && t.Text == identifier) return t.Line;
            return 0;
        }

        public Method FindMethod(string name) => Methods.Find(m => m.Name == name);

        /// <summary>The method whose body contains token <paramref name="index"/>, or null.</summary>
        public Method MethodAt(int index) => Methods.Find(m => m.BodyStart >= 0 && index >= m.BodyStart && index < m.BodyEnd);

        /// <summary>Unquoted value of a regular or verbatim string token.</summary>
        public static string StringValue(Tok t)
        {
            var s = t.Text;
            if (s.StartsWith("@\"", StringComparison.Ordinal)) return s.Substring(2, s.Length - 3).Replace("\"\"", "\"");
            if (s.StartsWith("$", StringComparison.Ordinal)) return "";
            if (s.Length >= 2 && s[0] == '"') return s.Substring(1, s.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
            return s;
        }

        void Tokenize()
        {
            var t = Source.Text;
            int n = t.Length;
            int i = 0;
            bool lineStart = true;
            while (i < n)
            {
                char c = t[i];
                if (c == '\n') { lineStart = true; i++; continue; }
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (c == '/' && i + 1 < n && t[i + 1] == '/') { while (i < n && t[i] != '\n') i++; continue; }
                if (c == '/' && i + 1 < n && t[i + 1] == '*')
                {
                    int close = t.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = close < 0 ? n : close + 2;
                    continue;
                }
                if (c == '#' && lineStart) { while (i < n && t[i] != '\n') i++; continue; }
                lineStart = false;
                int s = i;
                int line = Source.LineOf(s);
                // Strings: "..", @"..", $"..", $@"..", @$"..", '.'
                bool verbatim = false, interpolated = false;
                int q = i;
                while (q < n && (t[q] == '@' || t[q] == '$') && q - i < 3) { if (t[q] == '@') verbatim = true; else interpolated = true; q++; }
                if (q < n && t[q] == '"' && (q == i || verbatim || interpolated))
                {
                    i = q + 1;
                    int braces = 0;
                    while (i < n)
                    {
                        if (verbatim && t[i] == '"' && i + 1 < n && t[i + 1] == '"') { i += 2; continue; }
                        if (!verbatim && t[i] == '\\') { i += 2; continue; }
                        if (interpolated && t[i] == '{') { if (i + 1 < n && t[i + 1] == '{') { i += 2; continue; } braces++; }
                        if (interpolated && t[i] == '}' && braces > 0) braces--;
                        if (t[i] == '"' && braces == 0) { i++; break; }
                        if (!verbatim && t[i] == '\n') break;
                        i++;
                    }
                    Tokens.Add(new Tok { Kind = TokKind.String, Text = t.Substring(s, i - s), Line = line, Start = s, End = i });
                    continue;
                }
                if (c == '\'')
                {
                    i++;
                    while (i < n && t[i] != '\'' && t[i] != '\n') { if (t[i] == '\\') i++; i++; }
                    if (i < n && t[i] == '\'') i++;
                    Tokens.Add(new Tok { Kind = TokKind.String, Text = t.Substring(s, i - s), Line = line, Start = s, End = i });
                    continue;
                }
                if (char.IsLetter(c) || c == '_' || (c == '@' && i + 1 < n && (char.IsLetter(t[i + 1]) || t[i + 1] == '_')))
                {
                    if (c == '@') { i++; s = i; }
                    while (i < n && (char.IsLetterOrDigit(t[i]) || t[i] == '_')) i++;
                    Tokens.Add(new Tok { Kind = TokKind.Ident, Text = t.Substring(s, i - s), Line = line, Start = s, End = i });
                    continue;
                }
                if (char.IsDigit(c))
                {
                    while (i < n && (char.IsLetterOrDigit(t[i]) || t[i] == '.' || t[i] == '_')) i++;
                    Tokens.Add(new Tok { Kind = TokKind.Number, Text = t.Substring(s, i - s), Line = line, Start = s, End = i });
                    continue;
                }
                i++;
                Tokens.Add(new Tok { Kind = TokKind.Punct, Text = c.ToString(), Line = line, Start = s, End = i });
            }
        }

        static readonly HashSet<string> NotMethodNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "if", "for", "foreach", "while", "switch", "using", "lock", "catch", "return", "new", "nameof", "typeof",
            "sizeof", "default", "when", "fixed", "checked", "unchecked", "base", "this", "throw", "await", "get", "set",
        };

        void FindMethods()
        {
            for (int i = 1; i + 1 < Tokens.Count; i++)
            {
                var name = Tokens[i];
                if (name.Kind != TokKind.Ident || !Tokens[i + 1].Is("(") || NotMethodNames.Contains(name.Text)) continue;
                var prev = Tokens[i - 1];
                // A declaration has a return type (identifier, '>' of a generic, ']' of an array) before the name.
                bool typeBefore = (prev.Kind == TokKind.Ident && !NotMethodNames.Contains(prev.Text) && prev.Text != "else")
                                  || prev.Is(">") || prev.Is("]") || prev.Is("?");
                if (!typeBefore) continue;
                // Skip "new Foo(" and "return Foo(" and member access "a.Foo(".
                if (i >= 2 && Tokens[i - 2].Is(".")) continue;
                int close = MatchParen(i + 1);
                if (close < 0 || close + 1 >= Tokens.Count) continue;
                int after = close + 1;
                // Skip generic constraints: where T : class
                while (after < Tokens.Count && Tokens[after].Is("where"))
                {
                    while (after < Tokens.Count && !Tokens[after].Is("{") && !Tokens[after].Is(";")) after++;
                }
                if (after >= Tokens.Count) continue;
                var m = new Method { Name = name.Text, Line = name.Line };
                if (Tokens[after].Is("{"))
                {
                    int end = MatchBrace(after);
                    if (end < 0) continue;
                    m.BodyStart = after + 1;
                    m.BodyEnd = end;
                }
                else if (Tokens[after].Is("=") && after + 1 < Tokens.Count && Tokens[after + 1].Is(">"))
                {
                    int end = after + 2;
                    while (end < Tokens.Count && !Tokens[end].Is(";")) end++;
                    m.BodyStart = after + 2;
                    m.BodyEnd = end;
                }
                else if (!Tokens[after].Is(";")) continue;
                // Attributes: [ ... ] sequences right before the modifiers/return type.
                int k = i - 1;
                while (k >= 0 && (Tokens[k].Kind == TokKind.Ident || Tokens[k].Is(">") || Tokens[k].Is("<") || Tokens[k].Is(",") || Tokens[k].Is("?") || Tokens[k].Is("."))) k--;
                while (k >= 0 && Tokens[k].Is("]"))
                {
                    int depth = 0;
                    int j = k;
                    for (; j >= 0; j--) { if (Tokens[j].Is("]")) depth++; else if (Tokens[j].Is("[")) { depth--; if (depth == 0) break; } }
                    if (j + 1 < k && Tokens[j + 1].Kind == TokKind.Ident) m.Attributes.Add(Tokens[j + 1].Text);
                    k = j - 1;
                }
                Methods.Add(m);
            }
        }

        int MatchParen(int open)
        {
            int d = 0;
            for (int k = open; k < Tokens.Count; k++)
            {
                if (Tokens[k].Is("(")) d++;
                else if (Tokens[k].Is(")")) { d--; if (d == 0) return k; }
            }
            return -1;
        }

        int MatchBrace(int open)
        {
            int d = 0;
            for (int k = open; k < Tokens.Count; k++)
            {
                if (Tokens[k].Is("{")) d++;
                else if (Tokens[k].Is("}")) { d--; if (d == 0) return k; }
            }
            return -1;
        }
    }
}
