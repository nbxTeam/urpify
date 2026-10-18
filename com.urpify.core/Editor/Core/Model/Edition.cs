using System;
using System.Collections.Generic;
using System.Linq;

namespace Urpify
{
    /// <summary>Who fixes a finding (TZ section 2): Unity's converter, the Free package, Urpify Pro, or a person.</summary>
    public enum FixOwner
    {
        None = 0,
        Unity = 1,
        Free = 2,
        Pro = 3,
        Person = 4,
    }

    /// <summary>
    /// Free and Pro (TZ section 2, decided 01.10.2026). The Free package runs Unity's converter and the fixes after it; Pro
    /// converts shaders, scripts and components. Pro is installed when its Editor assembly is loaded (as source or DLL).
    /// </summary>
    public static class Edition
    {
        /// <summary>Rules whose Auto findings the Free package's fixes after Unity's converter handle.</summary>
        public static readonly HashSet<string> FreeRules = new HashSet<string>(StringComparer.Ordinal)
        {
            RuleIds.MaterialStandardTransparent, RuleIds.MaterialSelfIllumin, RuleIds.BuiltInMaterialReference, RuleIds.PackageMaterialReference,
            RuleIds.ShaderFindBuiltIn, RuleIds.MaterialVariant, RuleIds.MaterialBuiltInNoUpgrader,
            RuleIds.UrpAssetTextures, RuleIds.EnvironmentNotBaked,
        };

        static bool? s_Pro;

        public static bool ProInstalled =>
            s_Pro ?? (s_Pro = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Urpify.Editor.Pro")).Value;

        public static FixOwner Owner(FixTier tier, string ruleId)
        {
            switch (tier)
            {
                case FixTier.None: return FixOwner.None;
                case FixTier.UnityConverter: return FixOwner.Unity;
                case FixTier.Auto:
                case FixTier.AutoApprox: return FreeRules.Contains(ruleId ?? "") ? FixOwner.Free : FixOwner.Pro;
                case FixTier.Review: return FixOwner.Pro;
                default: return FixOwner.Person;
            }
        }

        public static FixOwner Owner(Finding f) => Owner(f.Tier, f.RuleId);

        /// <summary>
        /// Who fixes an asset: the owner of the findings that decide its fix tier (<see cref="MigrationReport.BreakingTier"/>);
        /// Pro when any of them needs Pro, so a row never promises the Free package more than it does.
        /// </summary>
        public static FixOwner Owner(MigrationReport report, AssetSummary asset)
        {
            var findings = report.Findings.Where(f => f.AssetPath == asset.Path).ToList();
            var breaking = findings.Where(f => f.Severity > Severity.Info).ToList();
            var source = (breaking.Count > 0 ? breaking : findings).Where(f => f.Tier == asset.Tier).ToList();
            if (source.Count == 0) return Owner(asset.Tier, "");
            return source.Select(Owner).Max();
        }

        /// <summary>True when the asset's fix needs Pro and Pro is not installed: the row shows a lock.</summary>
        public static bool Locked(MigrationReport report, AssetSummary asset) => !ProInstalled && Owner(report, asset) == FixOwner.Pro;
    }
}
