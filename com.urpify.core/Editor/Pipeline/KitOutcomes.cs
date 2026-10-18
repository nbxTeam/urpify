using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Urpify.Pipeline
{
    /// <summary>
    /// One tier per finding for the report, the list and the panels (architect decision 11 on the QA report). Before the
    /// Urpify converters step the scan's estimate is all there is; after it, what the step actually did decides: a shader the
    /// scan expected to convert but that was restored is "Not supported yet", one that looks different is Review, one that
    /// the step stopped on keeps the step's reason (QA-021: the list said Auto for shaders the step had not converted, and
    /// "Not yet" for ones it had).
    /// </summary>
    public static class KitOutcomes
    {
        /// <summary>The tier an outcome of kit-converters.json stands for; null for informational lines.</summary>
        public static FixTier? Tier(string outcome)
        {
            switch (outcome)
            {
                case "Auto": return FixTier.Auto;
                case "AutoApprox": return FixTier.AutoApprox;
                case ShaderBatchConverter.ReviewOutcome: return FixTier.Review;
                // Urpify's own converted result did not compile: Urpify's limitation, not the project's.
                case "Restored": return FixTier.NotSupportedYet;
                case "NotSupportedYet": return FixTier.NotSupportedYet;
                case "Manual": return FixTier.Manual;
                default: return null;
            }
        }

        /// <summary>The Urpify converters step's report when the step is done; null otherwise.</summary>
        public static KitConverterReport Load(string root = ".")
        {
            if (MigrationState.Load(root).Get(StepId.KitConverters).Status != StepStatus.Done) return null;
            var p = Path.Combine(root, MigrationRunner.ReportsDir, "kit-converters.json");
            try { return File.Exists(p) ? JsonUtility.FromJson<KitConverterReport>(File.ReadAllText(p)) : null; }
            catch { return null; }
        }

        public const string FlatFrameNote = "Review: a scene that uses this script renders almost one flat colour after the migration and did not before. "
            + "Turn the component off on the camera to check whether it is the cause, or roll back the migration.";

        /// <summary>
        /// The scripts a went-flat scene runs (VerifyRunner.Apply) take the Review outcome in the step's report, so the
        /// report, the list and the panels all say Review (QA-030 safety net). Idempotent.
        /// </summary>
        public static void MarkReview(KitConverterReport kit, ICollection<string> paths, string root = ".")
        {
            if (kit == null || paths == null || paths.Count == 0) return;
            bool changed = false;
            foreach (var item in kit.Items.Where(i => paths.Contains(i.Path) && (i.Outcome == "Auto" || i.Outcome == "AutoApprox" || i.Outcome == ShaderBatchConverter.ReviewOutcome)))
            {
                if (item.Outcome != ShaderBatchConverter.ReviewOutcome) { item.Outcome = ShaderBatchConverter.ReviewOutcome; changed = true; }
                if (!item.Notes.Contains(FlatFrameNote)) { item.Notes.Add(FlatFrameNote); changed = true; }
            }
            if (!changed) return;
            try { File.WriteAllText(Path.Combine(root, MigrationRunner.ReportsDir, "kit-converters.json"), JsonUtility.ToJson(kit, true)); }
            catch (Exception e) { Debug.LogWarning("[Urpify] Cannot update kit-converters.json: " + e.Message); }
        }

        /// <summary>
        /// Puts what the step did over the scan's estimate: each breaking finding of a file the step handled, and of the
        /// materials that use a shader it handled, takes the tier of the step's outcome. Recomputes the summary.
        /// </summary>
        public static void Apply(MigrationReport report, KitConverterReport kit)
        {
            if (report == null || kit == null) return;
            var byPath = new Dictionary<string, KitConverterItem>(StringComparer.Ordinal);
            foreach (var item in kit.Items.Where(i => i.Path.Length > 0 && Tier(i.Outcome) != null)) byPath[item.Path] = item;
            if (byPath.Count == 0) return;
            foreach (var f in report.Findings.Where(f => f.Severity > Severity.Info))
            {
                if (!byPath.TryGetValue(f.AssetPath, out var item)
                    && !(f.RuleId == RuleIds.MaterialCustomShader && f.Evidence != null && byPath.TryGetValue(f.Evidence, out item)))
                    continue;
                var tier = Tier(item.Outcome).Value;
                // A finding the step does not own (a material fix of Unity's converter on a shader's material) keeps its tier.
                if (f.Tier == FixTier.UnityConverter || f.Tier == FixTier.None) continue;
                // Work by hand stays by hand: a scene whose material references the step fixed can still miss a material
                // (SCENE.MISSING_MATERIAL), and the step does not fix that.
                if (f.Tier == FixTier.Manual) continue;
                f.Tier = tier;
                if (f.ConverterId == null || f.ConverterId.Length == 0) f.ConverterId = item.Converter;
            }
            report.Summarize();
        }
    }
}
