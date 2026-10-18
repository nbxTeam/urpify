using System.Collections.Generic;
using System.Linq;
using Urpify.Parsing;

namespace Urpify.Analysis
{
    /// <summary>
    /// The preprocessor condition under which a line of a program is compiled, as a C preprocessor expression built from the
    /// #if / #ifdef / #ifndef / #elif / #else directives above it ("" when it always is). Urpify does not evaluate
    /// conditions; it carries them along, so what a file declares only under a condition is renamed under the same condition
    /// (QA-007: whiteflare declares DecodeHDR only in its URP branch, included under #ifdef _WF_PLATFORM_LWRP).
    /// </summary>
    public static class HlslGuards
    {
        public static string At(HlslUnit unit, int line)
        {
            // Each open #if level: the condition of the current branch, and what the earlier branches of the level tested.
            var stack = new List<(string current, string earlier)>();
            foreach (var d in unit.Directives.Where(d => d.Line < line).OrderBy(d => d.Line))
            {
                switch (d.Kind)
                {
                    case DirectiveKind.If: stack.Add(("(" + d.Rest.Trim() + ")", "")); break;
                    case DirectiveKind.Ifdef: stack.Add(("defined(" + d.Rest.Trim() + ")", "")); break;
                    case DirectiveKind.Ifndef: stack.Add(("!defined(" + d.Rest.Trim() + ")", "")); break;
                    case DirectiveKind.Elif:
                        if (stack.Count == 0) break;
                        var (c, e) = stack[stack.Count - 1];
                        var tested = e.Length == 0 ? c : e + " || " + c;
                        stack[stack.Count - 1] = ("(!(" + tested + ") && (" + d.Rest.Trim() + "))", tested);
                        break;
                    case DirectiveKind.Else:
                        if (stack.Count == 0) break;
                        var (c2, e2) = stack[stack.Count - 1];
                        var all = e2.Length == 0 ? c2 : e2 + " || " + c2;
                        stack[stack.Count - 1] = ("!(" + all + ")", all);
                        break;
                    case DirectiveKind.Endif:
                        if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                        break;
                }
            }
            // An include guard (#ifndef X_INCLUDED ... #define X_INCLUDED) is not a real condition.
            var parts = stack.Select(s => s.current).Where(c => !IsIncludeGuard(unit, c)).ToList();
            return string.Join(" && ", parts);
        }

        /// <summary>Both conditions: "" stands for "always".</summary>
        public static string And(string a, string b) => a.Length == 0 ? b : b.Length == 0 ? a : a + " && " + b;

        static bool IsIncludeGuard(HlslUnit unit, string condition)
        {
            if (!condition.StartsWith("!defined(")) return false;
            var name = condition.Substring("!defined(".Length).TrimEnd(')').Trim();
            return unit.Directives.Any(d => d.Kind == DirectiveKind.Define && d.Rest.Trim().Split(' ', '\t')[0] == name);
        }
    }
}
