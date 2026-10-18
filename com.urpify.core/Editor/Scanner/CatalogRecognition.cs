using System.Linq;
using Urpify.Catalog;
using Urpify.Rules;

namespace Urpify.Scanner
{
    /// <summary>
    /// The catalog layer on top of the constructions (TZ 5.2, 11.1): a shader or script the catalog knows gets an Info
    /// finding that names it, where it comes from, how it matched, what was checked for it and, when its author has an
    /// official URP version, a pointer to it. The asset's other findings carry the entry's id.
    /// </summary>
    static class CatalogRecognition
    {
        public static void Shader(string text, string path, FindingSink sink) => Apply(Recognize(() => KnownSources.Shipped.RecognizeShader(text, path)), sink, "shader");

        public static void Script(string text, string path, FindingSink sink) =>
            Apply(Recognize(() => KnownSources.Shipped.RecognizeScript(text)), sink, "script");

        static CatalogMatch Recognize(System.Func<CatalogMatch> f)
        {
            try { return f(); }
            catch { return null; } // the catalog never breaks a scan
        }

        static void Apply(CatalogMatch m, FindingSink sink, string kind)
        {
            if (m == null) return;
            var e = m.Entry;
            // A similar shader is a hint only (QA-015): no recipe, no tier, not linked to the entry (architect decision 4 on
            // the QA report; MRTK Standard was "Similar 0.94" to a catalog entry and the tester read the result as its recipe).
            foreach (var f in sink.Findings.Where(_ => m.Kind != MatchKind.Similar))
            {
                f.CatalogId = e.Id;
                // A copy of Unity's own shader: Pro moves its materials to the URP shader Unity picks for the original
                // (CatalogMaterialRecipes), whatever blocks converting the copy itself.
                if (e.Recipe == "unity-urp-shader" && m.Kind != MatchKind.Similar && f.Severity > Severity.Info && f.Tier >= FixTier.NotSupportedYet) f.Tier = FixTier.Auto;
                // A known script template whose recipe was checked on its project (TZ 6.9: recognized templates are automatic).
                if (e.Verified && m.Kind != MatchKind.Similar && f.Tier == FixTier.AutoApprox) f.Tier = FixTier.Auto;
            }
            var how = m.Kind == MatchKind.Exact ? Loc.Get("catalog.match.exact")
                : m.Kind == MatchKind.Renamed ? Loc.Get("catalog.match.renamed")
                : Loc.Get("catalog.match.similar", (int)System.Math.Round(m.Score * 100));
            var official = string.IsNullOrEmpty(e.OfficialUrp) ? "" : " " + Loc.Get("catalog.official", e.OfficialUrp, e.OfficialUrpUrl);
            var status = string.IsNullOrEmpty(e.Status) ? "" : " " + e.Status.TrimEnd('.') + ".";
            var f2 = sink.Add(RuleIds.CatalogKnown, Severity.Info, FixTier.None, 0, e.Source, kind, e.Name, how, e.Source, e.License, status + official);
            if (m.Kind != MatchKind.Similar) f2.CatalogId = e.Id;
            f2.Evidence = m.Kind + (m.Kind == MatchKind.Similar ? " " + m.Score.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "");
        }
    }
}
