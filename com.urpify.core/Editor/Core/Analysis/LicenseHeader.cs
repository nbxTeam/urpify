using System.Linq;
using System.Text.RegularExpressions;

namespace Urpify.Analysis
{
    /// <summary>
    /// The license a shader file names in its header comments (TZ M8 item 7): an SPDX identifier, a Creative Commons
    /// licence, MIT, Apache, BSD, GPL, the Unlicense or public domain. The shader panel shows it with a reminder that a
    /// converted copy stays under the same license.
    /// </summary>
    public static class LicenseHeader
    {
        static readonly Regex Spdx = new Regex(@"SPDX-License-Identifier:\s*([A-Za-z0-9.\-+ ]+)", RegexOptions.IgnoreCase);
        static readonly Regex Known = new Regex(
            @"\b(CC[ -]?BY(?:[ -](?:NC|SA|ND)){0,3}(?:[ -]\d\.\d)?|CC0(?:[ -]1\.0)?|Creative Commons[^\r\n.;]{0,60}|MIT License|MIT license|Apache License(?:,? Version \d\.\d)?|BSD[ -]\d-Clause|GNU (?:Lesser |Affero )?General Public License(?: v?\d)?|GPL-?v?\d|LGPL-?v?\d|The Unlicense|public domain)\b",
            RegexOptions.IgnoreCase);

        /// <summary>The license named in the comments of the first lines of the file, or null.</summary>
        public static string Find(string text, int lines = 40)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int end = 0;
            for (int n = 0; n < lines && end >= 0 && end < text.Length; n++) { end = text.IndexOf('\n', end + 1); if (end < 0) end = text.Length; }
            var head = text.Substring(0, System.Math.Min(text.Length, System.Math.Max(end, 0)));
            var comments = string.Join("\n", Regex.Matches(head, @"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline).Cast<Match>().Select(m => m.Value));
            var m1 = Spdx.Match(comments);
            if (m1.Success) return m1.Groups[1].Value.Trim();
            var m2 = Known.Match(comments);
            return m2.Success ? m2.Value.Trim() : null;
        }

        /// <summary>Non-commercial licences (NC): the converted copy may not be sold either.</summary>
        public static bool NonCommercial(string license) => license != null && Regex.IsMatch(license, @"\bNC\b|non-?commercial", RegexOptions.IgnoreCase);
    }
}
