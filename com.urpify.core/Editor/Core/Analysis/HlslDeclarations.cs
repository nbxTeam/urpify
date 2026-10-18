using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Urpify.Parsing;
using Urpify.Generation;

namespace Urpify.Analysis
{
    /// <summary>One top-level variable declaration split into its parts.</summary>
    public sealed class VariableDeclaration
    {
        public HlslDeclaration Source;
        public string Modifiers = "";            // uniform, static, const, ...
        public string Type = "";
        public List<string> Names = new List<string>();
        public bool HasInitializer;
        public bool InsideConditional;           // between #if and #endif
    }

    /// <summary>Reads top-level variable declarations of an HLSL/CG program.</summary>
    public static class HlslDeclarations
    {
        static readonly HashSet<string> ModifierWords = new HashSet<string>(StringComparer.Ordinal)
            { "uniform", "static", "const", "extern", "volatile", "shared", "groupshared", "row_major", "column_major", "precise", "nointerpolation" };

        static readonly Regex Word = new Regex(@"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);

        /// <summary>Texture and sampler types stay outside the material CBUFFER (SRP Batcher rule).</summary>
        public static bool IsResourceType(string type)
        {
            if (string.IsNullOrEmpty(type)) return false;
            return type.StartsWith("sampler", StringComparison.Ordinal) || type.StartsWith("Texture", StringComparison.Ordinal)
                || type.StartsWith("SamplerState", StringComparison.Ordinal) || type.StartsWith("SamplerComparisonState", StringComparison.Ordinal)
                || type.StartsWith("RWTexture", StringComparison.Ordinal) || type.StartsWith("StructuredBuffer", StringComparison.Ordinal)
                || type.StartsWith("Buffer", StringComparison.Ordinal) || type.StartsWith("UNITY_DECLARE", StringComparison.Ordinal)
                || type.StartsWith("TEXTURE", StringComparison.Ordinal) || type.StartsWith("SAMPLER", StringComparison.Ordinal);
        }

        /// <summary>Variable declarations of the unit (functions, structs and typedefs excluded).</summary>
        public static List<VariableDeclaration> Variables(HlslUnit unit)
        {
            var conditional = ConditionalRanges(unit);
            var result = new List<VariableDeclaration>();
            foreach (var d in unit.Declarations)
            {
                var text = d.Text.Trim().TrimEnd(';').Trim();
                if (text.Length == 0 || text.StartsWith("#", StringComparison.Ordinal)) continue;
                if (text.StartsWith("typedef", StringComparison.Ordinal) || text.StartsWith("struct", StringComparison.Ordinal)
                    || text.StartsWith("cbuffer", StringComparison.Ordinal) || text.StartsWith("CBUFFER", StringComparison.Ordinal)) continue;
                if (text.Contains("(") && !text.Contains("=")) continue; // a macro call such as UNITY_INSTANCING_BUFFER_START(Props)
                var v = new VariableDeclaration { Source = d };
                // Split "modifiers type name[, name]..."
                int pos = 0;
                var words = new List<(string w, int at)>();
                foreach (Match m in Word.Matches(text)) words.Add((m.Value, m.Index));
                int wi = 0;
                var mods = new List<string>();
                while (wi < words.Count && ModifierWords.Contains(words[wi].w)) { mods.Add(words[wi].w); wi++; }
                if (wi >= words.Count) continue;
                v.Modifiers = string.Join(" ", mods);
                v.Type = words[wi].w;
                pos = words[wi].at + words[wi].w.Length;
                // Template arguments: Texture2D<float4> name
                var rest = text.Substring(pos);
                if (rest.TrimStart().StartsWith("<", StringComparison.Ordinal))
                {
                    int close = rest.IndexOf('>');
                    if (close < 0) continue;
                    v.Type += rest.Substring(0, close + 1).Trim();
                    rest = rest.Substring(close + 1);
                }
                foreach (var part in SplitTopLevel(rest, ','))
                {
                    var p = part.Trim();
                    if (p.Length == 0) continue;
                    if (p.Contains("=")) v.HasInitializer = true;
                    var nm = Word.Match(p);
                    if (nm.Success) v.Names.Add(nm.Value);
                }
                if (v.Names.Count == 0) continue;
                v.InsideConditional = conditional.Any(r => d.Span.Start >= r.Start && d.Span.Start < r.End);
                result.Add(v);
            }
            return result;
        }

        static IEnumerable<string> SplitTopLevel(string s, char sep)
        {
            int depth = 0, start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '(' || c == '[' || c == '{' || c == '<') depth++;
                else if (c == ')' || c == ']' || c == '}' || c == '>') depth--;
                else if (c == sep && depth == 0) { yield return s.Substring(start, i - start); start = i + 1; }
            }
            yield return s.Substring(start);
        }

        /// <summary>Source ranges between an #if/#ifdef/#ifndef and its #endif (outermost level).</summary>
        public static List<Span> ConditionalRanges(HlslUnit unit)
        {
            var ranges = new List<Span>();
            var stack = new Stack<int>();
            foreach (var dir in unit.Directives.OrderBy(x => x.Span.Start))
            {
                if (dir.Kind == DirectiveKind.If || dir.Kind == DirectiveKind.Ifdef || dir.Kind == DirectiveKind.Ifndef) stack.Push(dir.Span.Start);
                else if (dir.Kind == DirectiveKind.Endif && stack.Count > 0)
                {
                    var s = stack.Pop();
                    if (stack.Count == 0) ranges.Add(new Span(s, dir.Span.End));
                }
            }
            return ranges;
        }
    }
}
