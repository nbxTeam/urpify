using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Urpify.Rules;

namespace Urpify.Scanner
{
    /// <summary>
    /// Decides what URP does with a shader using Unity's own compiled data (SubShader tags and pass LightModes),
    /// so built-in shaders without source are covered too.
    /// </summary>
    public sealed class ShaderUrpEvaluator
    {
        readonly Dictionary<string, ShaderUrpOutcome> byName = new Dictionary<string, ShaderUrpOutcome>(StringComparer.Ordinal);
        /// <summary>Fallback shader name by shader name, from parsed project sources.</summary>
        public readonly Dictionary<string, string> Fallbacks = new Dictionary<string, string>(StringComparer.Ordinal);

        public static CompiledShaderInfo Read(Shader shader)
        {
            var info = new CompiledShaderInfo { Name = shader.name };
            try
            {
                foreach (var m in ShaderUtil.GetShaderMessages(shader))
                    if (m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                        info.Errors.Add(m.message);
                info.HasErrors = info.Errors.Count > 0 || ShaderUtil.ShaderHasError(shader);
                var rpTag = new ShaderTagId("RenderPipeline");
                var lightMode = new ShaderTagId("LightMode");
                for (int s = 0; s < shader.subshaderCount; s++)
                {
                    var sub = new CompiledSubShader { RenderPipeline = shader.FindSubshaderTagValue(s, rpTag).name ?? "" };
                    int passes = shader.GetPassCountInSubshader(s);
                    for (int p = 0; p < passes; p++) sub.LightModes.Add(shader.FindPassTagValue(s, p, lightMode).name ?? "");
                    info.SubShaders.Add(sub);
                }
            }
            catch (Exception e)
            {
                info.Errors.Add("Cannot read compiled shader data: " + e.Message);
            }
            return info;
        }

        /// <summary>The SubShader URP selects: first with no RenderPipeline tag or UniversalPipeline; null when none.</summary>
        public static CompiledSubShader UrpSubShader(CompiledShaderInfo info) =>
            info.SubShaders.FirstOrDefault(s => s.RenderPipeline.Length == 0 || string.Equals(s.RenderPipeline, "UniversalPipeline", StringComparison.OrdinalIgnoreCase));

        public ShaderUrpOutcome Evaluate(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return new ShaderUrpOutcome { ShaderName = "", Exists = false };
            if (byName.TryGetValue(shaderName, out var cached)) return cached;
            var shader = Shader.Find(shaderName);
            var o = shader == null ? new ShaderUrpOutcome { ShaderName = shaderName, Exists = false, Reason = "not found" } : Evaluate(shader, 0);
            byName[shaderName] = o;
            return o;
        }

        public ShaderUrpOutcome Evaluate(Shader shader) => Evaluate(shader, 0);

        ShaderUrpOutcome Evaluate(Shader shader, int depth)
        {
            var path = AssetDatabase.GetAssetPath(shader);
            var o = new ShaderUrpOutcome
            {
                ShaderName = shader.name,
                Exists = true,
                BuiltIn = string.IsNullOrEmpty(path) || path.StartsWith("Resources/", StringComparison.Ordinal) || path == "Resources/unity_builtin_extra",
                HasUnityUpgrader = UrpFacts.HasUnityUpgrader(shader.name),
            };
            var info = Read(shader);
            var sub = UrpSubShader(info);
            if (sub == null)
            {
                if (depth < 4 && Fallbacks.TryGetValue(shader.name, out var fb) && !string.Equals(fb, "Off", StringComparison.OrdinalIgnoreCase))
                {
                    var fbShader = Shader.Find(fb);
                    if (fbShader != null)
                    {
                        var f = Evaluate(fbShader, depth + 1);
                        o.Severity = f.Severity;
                        o.Reason = $"no SubShader for URP; Fallback \"{fb}\": {f.Reason}";
                        return o;
                    }
                }
                o.Severity = Severity.Pink;
                o.Reason = "no SubShader for URP";
                return o;
            }
            var legacy = sub.LightModes.Where(m => UrpFacts.LegacyLightModes.Contains(m)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (legacy.Count > 0)
            {
                o.Severity = Severity.Pink;
                o.Reason = "passes with LightMode " + string.Join(", ", legacy);
                return o;
            }
            var drawn = sub.LightModes.Count(m => UrpFacts.DrawnLightModes.Contains(m));
            if (drawn == 0)
            {
                o.Severity = Severity.Silent;
                o.Reason = "no pass URP draws (" + string.Join(", ", sub.LightModes.Select(m => m.Length == 0 ? "(none)" : m)) + ")";
                return o;
            }
            o.Severity = Severity.Info;
            o.Reason = "URP draws " + drawn + " pass(es)";
            return o;
        }
    }
}
