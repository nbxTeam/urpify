using System;
using System.Collections.Generic;
using System.Linq;
using Urpify.Parsing;

namespace Urpify.Rules
{
    /// <summary>
    /// Marks a rule class. A new construct is a new class with this attribute; the core needs no change.
    /// The id must be one of <see cref="RuleIds"/> or a new stable id.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class RuleAttribute : Attribute
    {
        public readonly string Id;
        /// <summary>Lower runs first. Rules that depend on others' findings use a higher order.</summary>
        public int Order;
        public RuleAttribute(string id) { Id = id; }
    }

    public interface IShaderRule { void Check(ShaderContext ctx, FindingSink sink); }
    public interface IScriptRule { void Check(ScriptContext ctx, FindingSink sink); }
    public interface IMaterialRule { void Check(MaterialContext ctx, FindingSink sink); }
    public interface ISceneRule { void Check(SceneContext ctx, FindingSink sink); }
    public interface IProjectRule { void Check(ProjectContext ctx, FindingSink sink); }

    /// <summary>Collects findings for one asset and fills the text fields from the localization table.</summary>
    public sealed class FindingSink
    {
        public readonly List<Finding> Findings = new List<Finding>();
        readonly string assetPath;
        readonly AssetKind kind;
        public string CurrentRule = "";

        public FindingSink(string assetPath, AssetKind kind) { this.assetPath = assetPath; this.kind = kind; }

        /// <summary>
        /// Adds a finding. Title and explanation come from "rule.&lt;id&gt;.title" / ".explanation" / ".manual",
        /// formatted with <paramref name="args"/>.
        /// </summary>
        public Finding Add(string ruleId, Severity severity, FixTier tier, int line, string evidence, params object[] args)
        {
            var f = new Finding
            {
                RuleId = ruleId,
                Severity = severity,
                Tier = tier,
                AssetPath = assetPath,
                Kind = kind,
                Line = line,
                Evidence = Trim(evidence),
                Title = Loc.Get("rule." + ruleId + ".title", args),
                Explanation = Loc.Get("rule." + ruleId + ".explanation", args),
                Args = (args ?? new object[0]).Select(a => Convert.ToString(a, System.Globalization.CultureInfo.InvariantCulture) ?? "").ToArray(),
                FromKeys = true,
            };
            // What to do by hand: for Manual findings, and for what Urpify does not convert yet (meanwhile).
            if (tier >= FixTier.NotSupportedYet)
            {
                var m = Loc.Get("rule." + ruleId + ".manual", args);
                if (m != "rule." + ruleId + ".manual") f.ManualSteps = m;
            }
            Findings.Add(f);
            return f;
        }

        public bool Has(string ruleId) => Findings.Any(f => f.RuleId == ruleId);

        static string Trim(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace('\t', ' ').Trim();
            int nl = s.IndexOfAny(new[] { '\r', '\n' });
            if (nl >= 0) s = s.Substring(0, nl).TrimEnd() + " ...";
            return s.Length > 160 ? s.Substring(0, 157) + "..." : s;
        }
    }

    // ------------------------------------------------------------------ contexts

    /// <summary>What Unity itself reports for a compiled shader. Filled by the scanner; null for files Unity did not import.</summary>
    public sealed class CompiledShaderInfo
    {
        public string Name = "";
        public bool HasErrors;
        public List<string> Errors = new List<string>();
        public List<CompiledSubShader> SubShaders = new List<CompiledSubShader>();
        /// <summary>Shader Unity falls back to when no SubShader works, "" when none.</summary>
        public string Fallback = "";
    }

    public sealed class CompiledSubShader
    {
        public string RenderPipeline = "";
        public List<string> LightModes = new List<string>();
    }

    public sealed class ShaderContext
    {
        public string AssetPath = "";
        public ShaderDocument Doc;
        /// <summary>Includes of every program, resolved and scanned (shared across shaders).</summary>
        public List<ResolvedInclude> Includes = new List<ResolvedInclude>();
        public CompiledShaderInfo Compiled;
        public string[] UsedBy = Array.Empty<string>();

        /// <summary>
        /// Every HLSL unit that the URP-selected SubShader compiles: its programs, visible include blocks and local
        /// include files. Empty when no SubShader qualifies for URP.
        /// </summary>
        public IEnumerable<(HlslUnit unit, string file, int lineOffset)> UrpUnits()
        {
            var sub = Doc?.UrpSubShader;
            if (sub == null) yield break;
            var programs = Doc.Programs.Where(p => p.SubShaderIndex == sub.Index || (p.IsInclude && p.SubShaderIndex == -1)).ToList();
            foreach (var p in programs) yield return (p.Unit, AssetPath, 0);
            foreach (var (inc, unit) in IncludeResolver.Flatten(Includes)) yield return (unit, inc.Path, 0);
        }

        /// <summary>First place an identifier is used in the URP SubShader's code; ("", 0) when absent.</summary>
        public (string file, int line) FindIdentifier(string identifier)
        {
            foreach (var (unit, file, _) in UrpUnits())
                if (unit.Uses(identifier)) return (file, unit.FirstLine(identifier));
            return ("", 0);
        }

        public string Evidence(string file, int line)
        {
            if (line <= 0) return "";
            if (file == AssetPath) return Doc.Source.LineText(line);
            return file + ":" + line;
        }
    }

    public sealed class ScriptContext
    {
        public string AssetPath = "";
        public CSharpFile Code;
        public string[] UsedBy = Array.Empty<string>();
        /// <summary>Resolves a shader name to what URP will do with it (null when unknown).</summary>
        public Func<string, ShaderUrpOutcome> ShaderOutcome;
        /// <summary>The script that feeds this effect through command buffers on lights, or "" (<see cref="Analysis.LightBufferFeeds"/>).</summary>
        public string FedByLightBuffers = "";
        /// <summary>What the script's OnRenderImage reaches in other scripts (<see cref="Analysis.ImageEffectReach"/>), or null.</summary>
        public Analysis.EffectReach Reach;
    }

    /// <summary>What happens to a shader under URP, decided from Unity's compiled data and the upgrader list.</summary>
    public sealed class ShaderUrpOutcome
    {
        public string ShaderName = "";
        public bool Exists;
        public bool BuiltIn;
        public Severity Severity;
        /// <summary>URP's own converter has an upgrader for this built-in shader.</summary>
        public bool HasUnityUpgrader;
        public string Reason = "";
    }

    public sealed class MaterialContext
    {
        public string AssetPath = "";
        /// <summary>For materials embedded in a model file: the material name inside it, else "".</summary>
        public string SubAssetName = "";
        public UnityEngine.Material Material;
        public string ShaderName = "";
        public string ShaderPath = "";
        public ShaderUrpOutcome Outcome;
        /// <summary>Worst severity and tier of the shader's own findings when the shader is a project asset.</summary>
        public Severity ShaderSeverity;
        public FixTier ShaderTier;
        public string[] UsedBy = Array.Empty<string>();
    }

    public sealed class SceneContext
    {
        public string AssetPath = "";
        public bool IsPrefab;
        public UnityYamlFile Yaml;
        /// <summary>The file text, for rules that read lines directly (animation clips).</summary>
        public string RawText;
        /// <summary>Script asset path by GUID, for MonoBehaviour documents.</summary>
        public Func<string, string> ScriptPathForGuid;
        /// <summary>
        /// A material from the Editor's built-in resources by (guid, fileID), with what URP does with its shader;
        /// null when the reference is not a built-in material.
        /// </summary>
        public Func<string, long, (string name, ShaderUrpOutcome outcome)?> BuiltInMaterial;
        /// <summary>
        /// A material file inside a package (Packages/...) by GUID, with what URP does with its shader; null when the
        /// reference is not a package material.
        /// </summary>
        public Func<string, (string name, string path, ShaderUrpOutcome outcome)?> PackageMaterial;
    }

    public sealed class ProjectContext
    {
        public string ProjectRoot = "";
        public Dictionary<string, string> ManifestDependencies = new Dictionary<string, string>(StringComparer.Ordinal);
        public string UrpVersion = "";
        public bool ScriptCompilationFailed;
        /// <summary>
        /// A scene or prefab has a Post-processing v2 layer (SCENE.PPV2); null when the scan does not cover the whole project
        /// and so cannot tell.
        /// </summary>
        public bool? PostProcessingV2InUse;
    }

    // ------------------------------------------------------------------ registry

    public static class RuleRegistry
    {
        static List<(RuleAttribute attr, IShaderRule rule)> shaderRules;
        static List<(RuleAttribute attr, IScriptRule rule)> scriptRules;
        static List<(RuleAttribute attr, IMaterialRule rule)> materialRules;
        static List<(RuleAttribute attr, ISceneRule rule)> sceneRules;
        static List<(RuleAttribute attr, IProjectRule rule)> projectRules;

        public static IReadOnlyList<(RuleAttribute attr, IShaderRule rule)> ShaderRules => shaderRules ??= Collect<IShaderRule>();
        public static IReadOnlyList<(RuleAttribute attr, IScriptRule rule)> ScriptRules => scriptRules ??= Collect<IScriptRule>();
        public static IReadOnlyList<(RuleAttribute attr, IMaterialRule rule)> MaterialRules => materialRules ??= Collect<IMaterialRule>();
        public static IReadOnlyList<(RuleAttribute attr, ISceneRule rule)> SceneRules => sceneRules ??= Collect<ISceneRule>();
        public static IReadOnlyList<(RuleAttribute attr, IProjectRule rule)> ProjectRules => projectRules ??= Collect<IProjectRule>();

        /// <summary>A string that changes whenever the set of rules changes; part of the cache key.</summary>
        public static string Fingerprint =>
            string.Join(",", ShaderRules.Select(r => r.attr.Id).Concat(ScriptRules.Select(r => r.attr.Id))
                .Concat(MaterialRules.Select(r => r.attr.Id)).Concat(SceneRules.Select(r => r.attr.Id)).OrderBy(x => x, StringComparer.Ordinal));

        static List<(RuleAttribute, T)> Collect<T>() where T : class
        {
            var list = new List<(RuleAttribute, T)>();
            foreach (var type in UnityEditor.TypeCache.GetTypesDerivedFrom<T>())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                var attr = (RuleAttribute)Attribute.GetCustomAttribute(type, typeof(RuleAttribute));
                if (attr == null) continue;
                try { list.Add((attr, (T)Activator.CreateInstance(type))); }
                catch (Exception e) { UnityEngine.Debug.LogError($"[Urpify] Cannot create rule {type.FullName}: {e.Message}"); }
            }
            return list.OrderBy(r => r.Item1.Order).ThenBy(r => r.Item1.Id, StringComparer.Ordinal).ToList();
        }
    }
}
