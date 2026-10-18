using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Urpify.Parsing;

namespace Urpify.Catalog
{
    /// <summary>
    /// Fingerprint of a shader's normalized source (TZ 11.1): comments, whitespace and property default values do not
    /// count, so a copy with other formatting or other defaults has the same fingerprint. The catalog (M7) recognizes
    /// known shaders by it; the corpus statistics count each fingerprint once (architect decision 8 after M3).
    /// </summary>
    public static class ShaderFingerprint
    {
        public static string Of(string text, string path = "") => Hash(Normalize(text, path));

        /// <summary>The text that is hashed: properties without default values, then no comments and no whitespace.</summary>
        public static string Normalize(string text, string path = "") => StripCommentsAndWhitespace(WithoutPropertyDefaults(text, path));

        /// <summary>The source with each property written as [attributes]Name("Display",Type): no default values.</summary>
        public static string WithoutPropertyDefaults(string text, string path = "")
        {
            text = text ?? "";
            ShaderDocument doc = null;
            try { doc = ShaderLabParser.Parse(text, path); } catch { /* fingerprint the raw text */ }
            var sb = new StringBuilder(text.Length);
            int pos = 0;
            if (doc != null)
                foreach (var p in doc.Properties.Where(p => p.Span.Length > 0).OrderBy(p => p.Span.Start))
                {
                    if (p.Span.Start < pos || p.Span.End > text.Length) continue;
                    sb.Append(text, pos, p.Span.Start - pos);
                    foreach (var a in p.Attributes) sb.Append('[').Append(a).Append(']');
                    sb.Append(p.Name).Append("(\"").Append(p.DisplayName).Append("\",").Append(p.Type).Append(')');
                    pos = p.Span.End;
                }
            sb.Append(text, pos, text.Length - pos);
            return sb.ToString();
        }

        /// <summary>Fingerprint of a C# source: comments and whitespace do not count.</summary>
        public static string OfScript(string text) => Hash(StripCommentsAndWhitespace(text ?? ""));

        static string StripCommentsAndWhitespace(string s)
        {
            var sb = new StringBuilder(s.Length);
            int i = 0, n = s.Length;
            while (i < n)
            {
                char c = s[i];
                if (c == '/' && i + 1 < n && s[i + 1] == '/')
                {
                    while (i < n && s[i] != '\n') i++;
                    continue;
                }
                if (c == '/' && i + 1 < n && s[i + 1] == '*')
                {
                    int e = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = e < 0 ? n : e + 2;
                    continue;
                }
                if (c == '"')
                {
                    int e = i + 1;
                    while (e < n && s[e] != '"' && s[e] != '\n') e++;
                    e = Math.Min(n, e + 1);
                    sb.Append(s, i, e - i);
                    i = e;
                    continue;
                }
                if (!char.IsWhiteSpace(c)) sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        static string Hash(string s)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                var sb = new StringBuilder(32);
                for (int i = 0; i < 16; i++) sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
