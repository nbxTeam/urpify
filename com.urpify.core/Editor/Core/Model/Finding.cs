using System;
using System.Collections.Generic;

namespace Urpify
{
    /// <summary>
    /// What happens to the asset once the project runs on URP and Unity's own Render Pipeline Converter has run.
    /// Ordered from harmless to worst so the maximum is the verdict of a group.
    /// </summary>
    public enum Severity
    {
        /// <summary>No visible change.</summary>
        Info = 0,
        /// <summary>Renders, but with a noticeable difference.</summary>
        Degraded = 1,
        /// <summary>Not pink, but the effect disappears or is distorted without any error.</summary>
        Silent = 2,
        /// <summary>Renders with the error shader (pink) in the Editor; invisible in release builds.</summary>
        Pink = 3,
    }

    /// <summary>Who fixes the finding. Ordered from least to most manual work.</summary>
    public enum FixTier
    {
        /// <summary>Nothing to fix.</summary>
        None = 0,
        /// <summary>Unity's Render Pipeline Converter handles it.</summary>
        UnityConverter = 1,
        /// <summary>Urpify converts it with a practically identical result.</summary>
        Auto = 2,
        /// <summary>Urpify converts it; the result is close but not pixel-identical.</summary>
        AutoApprox = 3,
        /// <summary>
        /// Urpify does not convert this yet; it is planned (decision before M8 No. 2). Until then a person does it, with
        /// Urpify's explanation.
        /// </summary>
        NotSupportedYet = 4,
        /// <summary>
        /// A person has to act, and only in two cases (decision before M8 No. 2): the project was broken before the
        /// migration (a shader that does not compile in Built-in, scripts that do not compile, a missing include file), or
        /// the construct is proven to have no URP counterpart or no need for one.
        /// </summary>
        Manual = 5,
        /// <summary>
        /// Converted, but the self-check render looks different from the original (QA-004): someone should look at it.
        /// Ranks between AutoApprox and NotSupportedYet (<see cref="SeverityUtil.Rank"/>); the value is new, so it is last.
        /// </summary>
        Review = 6,
    }

    /// <summary>Top-level group in the findings list.</summary>
    public enum AssetKind
    {
        Shader = 0,
        Script = 1,
        Material = 2,
        Scene = 3,
        Prefab = 4,
        Settings = 5,
        Project = 6,
        Animation = 7,
    }

    /// <summary>One construct of the old pipeline found in one asset. Serialized into the JSON report.</summary>
    [Serializable]
    public sealed class Finding
    {
        public string RuleId;
        public Severity Severity;
        public string AssetPath;
        /// <summary>1-based line in the asset, 0 when the finding is about the whole asset.</summary>
        public int Line;
        public AssetKind Kind;
        /// <summary>Materials, scenes and prefabs that use the asset.</summary>
        public string[] UsedBy = Array.Empty<string>();
        public FixTier Tier;
        public string ConverterId = "";
        public string CatalogId = "";
        /// <summary>Short name of the construct, e.g. "Custom lighting function LightingToon".</summary>
        public string Title = "";
        /// <summary>Plain-language: what breaks and why.</summary>
        public string Explanation = "";
        /// <summary>For Manual findings: what to do.</summary>
        public string ManualSteps = "";
        /// <summary>The source text that triggered the rule, trimmed to one line.</summary>
        public string Evidence = "";
        /// <summary>
        /// The arguments the texts were formatted with. Title, Explanation and ManualSteps hold the text in the language of
        /// the scan (the report files keep it); the window formats the rule's strings again in its own language from these
        /// (design review 13.10.2026, item 2: a scan run from the command line showed English titles in a Russian window).
        /// </summary>
        public string[] Args = Array.Empty<string>();
        /// <summary>The key of ManualSteps when a rule chose another one than "rule.&lt;id&gt;.manual".</summary>
        public string ManualKey = "";
        /// <summary>The texts come from the rule's localization keys with <see cref="Args"/>; false in reports made before.</summary>
        public bool FromKeys;

        /// <summary>The title in the current language (as stored when the report predates <see cref="Args"/>).</summary>
        public string LocalTitle => Localized("rule." + RuleId + ".title", Title);
        public string LocalExplanation => Localized("rule." + RuleId + ".explanation", Explanation);
        public string LocalManualSteps => string.IsNullOrEmpty(ManualSteps) ? "" : Localized(ManualKey.Length > 0 ? ManualKey : "rule." + RuleId + ".manual", ManualSteps);

        string Localized(string key, string stored) =>
            FromKeys && Loc.Has(key) ? Loc.Get(key, Array.ConvertAll(Args ?? Array.Empty<string>(), a => (object)a)) : stored;

        public Finding Clone()
        {
            var f = (Finding)MemberwiseClone();
            f.UsedBy = (string[])UsedBy.Clone();
            f.Args = (string[])(Args ?? Array.Empty<string>()).Clone();
            return f;
        }

        public override string ToString() => $"{Severity} {Tier} {RuleId} {AssetPath}:{Line} {Title}";
    }

    public static class SeverityUtil
    {
        public static Severity Max(Severity a, Severity b) => a > b ? a : b;
        public static FixTier Max(FixTier a, FixTier b) => Rank(a) >= Rank(b) ? a : b;

        /// <summary>How much a tier asks of a person, for "the worst tier": Review sits between AutoApprox and NotSupportedYet.</summary>
        public static int Rank(FixTier t) => t == FixTier.Review ? 35 : (int)t * 10;

        public static Severity Worst(IEnumerable<Finding> findings)
        {
            var s = Severity.Info;
            foreach (var f in findings) s = Max(s, f.Severity);
            return s;
        }
    }
}
