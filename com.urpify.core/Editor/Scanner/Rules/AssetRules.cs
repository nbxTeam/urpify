using System.Collections.Generic;
using System;
using System.Linq;
using Urpify.Parsing;

namespace Urpify.Rules
{
    [Rule(RuleIds.MaterialUnityConverter)]
    sealed class MaterialRule : IMaterialRule
    {
        public void Check(MaterialContext ctx, FindingSink sink)
        {
            var o = ctx.Outcome;
            if (o == null || !o.Exists) return;
            string label = ctx.SubAssetName.Length > 0 ? ctx.SubAssetName : "";
            if (ctx.Material != null && ctx.Material.parent != null)
                // URP 14's converter skips variants (core 14.0.12 MaterialUpgrader.cs: "if (material.isVariant) continue;"), so
                // overrides of Built-in properties stop applying once the parent is on URP; URP 17.5's converter upgrades
                // them after the parent (core RenderPipelineConverterMaterialUpgrader.cs). The fix after Unity's converter
                // carries the overrides over in both (M8).
                sink.Add(RuleIds.MaterialVariant, o.BuiltIn && o.HasUnityUpgrader && Scanner.UrpFacts.UrpMajorForEditor < 17 ? Severity.Silent : Severity.Info,
                    o.BuiltIn && o.HasUnityUpgrader ? FixTier.Auto : FixTier.None, 0, o.ShaderName, ctx.Material.parent.name);
            if (o.BuiltIn)
            {
                if (o.HasUnityUpgrader)
                {
                    // Measured on the lab: Standard in Transparent mode and Legacy Self-Illumin lose their look after Unity's converter.
                    if ((o.ShaderName == "Standard" || o.ShaderName == "Standard (Specular setup)") && ctx.Material != null
                        && ctx.Material.HasProperty("_Mode") && Math.Abs(ctx.Material.GetFloat("_Mode") - 3f) < 0.01f)
                        sink.Add(RuleIds.MaterialStandardTransparent, Severity.Silent, FixTier.Auto, 0, o.ShaderName, o.ShaderName, label);
                    else if (o.ShaderName.StartsWith("Legacy Shaders/Self-Illumin/", StringComparison.Ordinal))
                        sink.Add(RuleIds.MaterialSelfIllumin, Severity.Silent, FixTier.Auto, 0, o.ShaderName, o.ShaderName, label);
                    else
                        sink.Add(RuleIds.MaterialUnityConverter, Severity.Info, FixTier.UnityConverter, 0, o.ShaderName, o.ShaderName, label);
                }
                // Tree prefabs use the Optimized variants, which Unity names Hidden/Nature/... (QA-014: the Free scan called a
                // Tree Creator tree "Not supported yet, assign a URP shader by hand" though Pro converts it).
                else if (o.ShaderName.StartsWith("Nature/Tree Soft Occlusion", StringComparison.Ordinal) || o.ShaderName.StartsWith("Nature/Tree Creator", StringComparison.Ordinal)
                         || o.ShaderName.StartsWith("Hidden/Nature/Tree Creator", StringComparison.Ordinal) || o.ShaderName.StartsWith("Hidden/Nature/Tree Soft Occlusion", StringComparison.Ordinal))
                {
                    // URP 14.0.12 and 17.5.0 ship no shader for these (no Tree Creator or soft-occlusion shader in the package
                    // sources); Pro converts Unity's own sources of them (M8).
                    sink.Add(RuleIds.MaterialBuiltInTree, Severity.Pink, FixTier.AutoApprox, 0, o.ShaderName, o.ShaderName);
                }
                else
                {
                    sink.Add(RuleIds.MaterialBuiltInNoUpgrader, o.Severity, o.Severity == Severity.Info ? FixTier.None
                        : BuiltInShaderTable.ForMaterials.ContainsKey(o.ShaderName) ? FixTier.Auto : FixTier.NotSupportedYet, 0,
                        o.ShaderName, o.ShaderName, o.Reason);
                }
                return;
            }
            if (ctx.ShaderSeverity > Severity.Info)
                sink.Add(RuleIds.MaterialCustomShader, ctx.ShaderSeverity, ctx.ShaderTier, 0, ctx.ShaderPath, o.ShaderName, ctx.ShaderPath);
        }
    }

    [Rule(RuleIds.ProjectorComponent)]
    sealed class ProjectorComponentRule : ISceneRule
    {
        public void Check(SceneContext ctx, FindingSink sink)
        {
            foreach (var d in ctx.Yaml.OfType("Projector"))
                sink.Add(RuleIds.ProjectorComponent, Severity.Silent, FixTier.AutoApprox, d.Line, ctx.Yaml.GameObjectPath(d), ctx.Yaml.GameObjectPath(d));
        }
    }

    [Rule(RuleIds.PostProcessingV2)]
    sealed class PostProcessingV2Rule : ISceneRule
    {
        public void Check(SceneContext ctx, FindingSink sink)
        {
            if (ctx.ScriptPathForGuid == null) return;
            foreach (var d in ctx.Yaml.OfType("MonoBehaviour"))
            {
                var path = ctx.ScriptPathForGuid(d.RefGuid("m_Script"));
                if (path == null || !path.EndsWith("/PostProcessLayer.cs", StringComparison.Ordinal)) continue;
                if (path.IndexOf("com.unity.postprocessing", StringComparison.Ordinal) < 0 && path.IndexOf("PostProcessing", StringComparison.Ordinal) < 0) continue;
                sink.Add(RuleIds.PostProcessingV2, Severity.Silent, FixTier.UnityConverter, d.Line, ctx.Yaml.GameObjectPath(d), ctx.Yaml.GameObjectPath(d));
                return;
            }
        }
    }

    [Rule(RuleIds.TerrainBuiltIn)]
    sealed class TerrainRule : ISceneRule
    {
        public void Check(SceneContext ctx, FindingSink sink)
        {
            foreach (var d in ctx.Yaml.OfType("Terrain"))
            {
                // Default material: empty reference, or a material from the Editor's built-in resources
                // (guid 0000000000000000f000000000000000, as serialized in the lab scene by 6000.5).
                bool builtInMaterial = d.RefFileId("m_MaterialTemplate") == 0 || d.RefGuid("m_MaterialTemplate") == "0000000000000000f000000000000000";
                if (!builtInMaterial) continue;
                sink.Add(RuleIds.TerrainBuiltIn, Severity.Info, FixTier.UnityConverter, d.Line, ctx.Yaml.GameObjectPath(d), ctx.Yaml.GameObjectPath(d));
            }
        }
    }

    /// <summary>
    /// Renderers that reference a material from the Editor's built-in resources (Default-Material and friends).
    /// Unity's converter does not rewrite these references: in ShaderTutorials 008, 009 and 010 the ground plane with
    /// Default-Material stayed pink after the official converter (birp-to-urp-tool-plan.md 2.4a captures).
    /// </summary>
    [Rule(RuleIds.BuiltInMaterialReference)]
    sealed class BuiltInMaterialReferenceRule : ISceneRule
    {
        public void Check(SceneContext ctx, FindingSink sink)
        {
            if (ctx.BuiltInMaterial == null) return;
            var seen = new Dictionary<long, (string name, ShaderUrpOutcome o, int count, UnityYamlFile.Doc first)>();
            foreach (var d in ctx.Yaml.Docs)
            {
                if (!d.Lists.ContainsKey("m_Materials")) continue;
                foreach (var (fileId, guid) in d.ListRefs("m_Materials"))
                {
                    if (guid.Length == 0 || !guid.StartsWith("0000000000000000", StringComparison.Ordinal)) continue;
                    var m = ctx.BuiltInMaterial(guid, fileId);
                    if (m == null) continue;
                    if (seen.TryGetValue(fileId, out var e)) seen[fileId] = (e.name, e.o, e.count + 1, e.first);
                    else seen[fileId] = (m.Value.name, m.Value.outcome, 1, d);
                }
            }
            foreach (var kv in seen)
            {
                var (name, o, count, first) = kv.Value;
                if (o == null || o.Severity == Severity.Info) continue;
                var path = ctx.Yaml.GameObjectPath(first);
                sink.Add(RuleIds.BuiltInMaterialReference, o.Severity, FixTier.Auto, first.Line, path, name, o.ShaderName, count, path);
            }
        }
    }

    /// <summary>
    /// Renderers that reference a material inside a package whose shader URP cannot draw. Unity's converter never changes
    /// package files, so these stay pink after it: on smo-shaders in 2022.3 every ProBuilder wall used ProBuilder 5.2.4's
    /// ProBuilderDefault.mat ("ProBuilder/Standard Vertex Color", a surface shader) and 74 % of the frame turned pink. The
    /// free fix points them to a URP copy in the project: Unity's upgrader for a built-in shader (Auto), else URP Lit with
    /// the material's color, texture, smoothness and metallic (AutoApprox: the shader's own effects are not kept).
    /// </summary>
    [Rule(RuleIds.PackageMaterialReference)]
    sealed class PackageMaterialReferenceRule : ISceneRule
    {
        public void Check(SceneContext ctx, FindingSink sink)
        {
            if (ctx.PackageMaterial == null) return;
            var seen = new Dictionary<string, (string name, string path, ShaderUrpOutcome o, int count, UnityYamlFile.Doc first)>(StringComparer.Ordinal);
            foreach (var d in ctx.Yaml.Docs)
            {
                if (!d.Lists.ContainsKey("m_Materials")) continue;
                foreach (var (_, guid) in d.ListRefs("m_Materials"))
                {
                    if (guid.Length == 0 || guid.StartsWith("0000000000000000", StringComparison.Ordinal)) continue;
                    if (seen.TryGetValue(guid, out var e)) { seen[guid] = (e.name, e.path, e.o, e.count + 1, e.first); continue; }
                    var m = ctx.PackageMaterial(guid);
                    if (m == null) continue;
                    seen[guid] = (m.Value.name, m.Value.path, m.Value.outcome, 1, d);
                }
            }
            foreach (var kv in seen)
            {
                var (name, path, o, count, first) = kv.Value;
                if (o == null || o.Severity == Severity.Info) continue;
                var go = ctx.Yaml.GameObjectPath(first);
                var tier = o.BuiltIn && o.HasUnityUpgrader ? FixTier.Auto : FixTier.AutoApprox;
                sink.Add(RuleIds.PackageMaterialReference, o.Severity, tier, first.Line, go, name, path, o.ShaderName, count, go);
            }
        }
    }

    /// <summary>
    /// Renderers that reference a material file that is not in the project (group A of Manual: broken before the
    /// migration). They render pink in Built-in already. Seen on daniel-ilett/smo-shaders (2026-10-12): Unity 6 upgraded
    /// ProBuilder 4.0.5 to 6.1.2, whose package no longer has the old default material the walls used, so the scene was
    /// pink before and after the migration and nothing said why.
    /// </summary>
    [Rule(RuleIds.MissingMaterial)]
    sealed class MissingMaterialRule : ISceneRule
    {
        public void Check(SceneContext ctx, FindingSink sink)
        {
            if (ctx.ScriptPathForGuid == null) return;
            var missing = new Dictionary<string, (int count, UnityYamlFile.Doc first)>(StringComparer.Ordinal);
            foreach (var d in ctx.Yaml.Docs)
            {
                if (!d.Lists.ContainsKey("m_Materials")) continue;
                foreach (var (_, guid) in d.ListRefs("m_Materials"))
                {
                    // Empty: no material set; 0000000000000000...: the Editor's built-in resources (SCENE.BUILTIN_MATERIAL).
                    if (guid.Length == 0 || guid.StartsWith("0000000000000000", StringComparison.Ordinal)) continue;
                    if (!string.IsNullOrEmpty(ctx.ScriptPathForGuid(guid))) continue;
                    missing[guid] = missing.TryGetValue(guid, out var e) ? (e.count + 1, e.first) : (1, d);
                }
            }
            foreach (var kv in missing)
            {
                var path = ctx.Yaml.GameObjectPath(kv.Value.first);
                sink.Add(RuleIds.MissingMaterial, Severity.Pink, FixTier.Manual, kv.Value.first.Line, path, kv.Value.count, path, kv.Key);
            }
        }
    }

    /// <summary>Animation clips that animate Built-in material property names (YAML "attribute: material._Color.r").</summary>
    [Rule(RuleIds.AnimatedMaterialProperties)]
    sealed class AnimatedMaterialPropertiesRule : ISceneRule
    {
        static readonly string[] BuiltInNames = { "material._Color", "material._MainTex", "material._Glossiness" };

        public void Check(SceneContext ctx, FindingSink sink)
        {
            if (!ctx.AssetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) || ctx.RawText == null) return;
            int line = 0, n = 0;
            string first = "";
            var lines = ctx.RawText.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var l = lines[i].Trim();
                if (!l.StartsWith("attribute: material.", StringComparison.Ordinal)) continue;
                var attr = l.Substring("attribute: ".Length);
                if (!BuiltInNames.Any(b => attr.StartsWith(b, StringComparison.Ordinal))) continue;
                n++;
                if (line == 0) { line = i + 1; first = attr; }
            }
            if (n > 0) sink.Add(RuleIds.AnimatedMaterialProperties, Severity.Info, FixTier.UnityConverter, line, first, n, first);
        }
    }

    [Rule(RuleIds.BinaryScene)]
    sealed class BinarySceneRule : ISceneRule
    {
        public void Check(SceneContext ctx, FindingSink sink)
        {
            if (ctx.AssetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) return;
            if (!ctx.Yaml.IsText) sink.Add(RuleIds.BinaryScene, Severity.Info, FixTier.None, 0, "");
        }
    }

    /// <summary>
    /// Scenes that take ambient light from the skybox and have no baked lighting data (architect decision 2 after M8).
    /// Measured on Portals (M9a): in a Unity 6 player such a scene gets no ambient light in either pipeline (ambient probe 0
    /// in the Built-in and the URP player; with the environment baked, 0.168 in both, mean brightness 124.8 -> 145.4 in
    /// Built-in, 115.9 -> 134.6 in URP), while the Editor shows it. So it is not a break of the migration: Info, with the
    /// scene panel's optional Bake environment to make the player match the Editor. (M8 read it as URP-only: wrong.)
    /// </summary>
    [Rule(RuleIds.EnvironmentNotBaked)]
    sealed class EnvironmentNotBakedRule : ISceneRule
    {
        public void Check(SceneContext ctx, FindingSink sink)
        {
            // Only where it is measured: Unity 6 / URP 17. In 2022.3 / URP 14 a player gets an ambient probe without baked
            // data in both pipelines (Portals, M9a: L0 0.168 / 0.211 / 0.290 in Built-in, in URP, and in URP after a bake),
            // and baking the environment changes nothing.
            if (ctx.IsPrefab || Scanner.UrpFacts.UrpMajorForEditor < 17) return;
            var render = ctx.Yaml.OfType("RenderSettings").FirstOrDefault();
            var lightmap = ctx.Yaml.OfType("LightmapSettings").FirstOrDefault();
            if (render == null || lightmap == null) return;
            if (render.Get("m_AmbientMode").Trim() != "0" || render.RefFileId("m_SkyboxMaterial") == 0) return;
            // Older scenes (Unity 5) have no m_LightingDataAsset field: no lighting data either.
            if (lightmap.RefFileId("m_LightingDataAsset") != 0) return;
            sink.Add(RuleIds.EnvironmentNotBaked, Severity.Info, FixTier.None, render.Line, "RenderSettings: m_AmbientMode 0 (Skybox), LightmapSettings: m_LightingDataAsset none");
        }
    }

    /// <summary>URP Assets without their texture resources (Scanner.UrpAssetTextures; architect decision 6 after M8).</summary>
    [Rule(RuleIds.UrpAssetTextures)]
    sealed class UrpAssetTexturesRule : IProjectRule
    {
        public void Check(ProjectContext ctx, FindingSink sink)
        {
            if (string.IsNullOrEmpty(ctx.UrpVersion)) return;
            foreach (var path in Scanner.UrpAssetTextures.EmptyAssets())
            {
                var asset = UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);
                var missing = string.Join(", ", Scanner.UrpAssetTextures.MissingTextures(asset));
                var f = sink.Add(RuleIds.UrpAssetTextures, Severity.Silent, FixTier.Auto, 0, "m_Textures: " + missing + " empty", missing);
                f.AssetPath = path;
            }
        }
    }

    [Rule(RuleIds.UrpNotInstalled)]
    sealed class ProjectRules : IProjectRule
    {
        public void Check(ProjectContext ctx, FindingSink sink)
        {
            if (string.IsNullOrEmpty(ctx.UrpVersion))
                sink.Add(RuleIds.UrpNotInstalled, Severity.Info, FixTier.UnityConverter, 0, "Packages/manifest.json", Scanner.UrpFacts.UrpMajorForEditor);
            // Nothing uses the package any more (Unity's converter turned its layers into URP Volumes, or there were none): it is
            // not something a converter will fix, and it can go (owner's check 19.10.2026, item 10).
            if (ctx.ManifestDependencies.TryGetValue("com.unity.postprocessing", out var ppv))
            {
                if (ctx.PostProcessingV2InUse == false)
                    sink.Add(RuleIds.PostProcessingPackageUnused, Severity.Info, FixTier.None, 0, "\"com.unity.postprocessing\": \"" + ppv + "\"", ppv);
                else
                    sink.Add(RuleIds.PostProcessingPackage, Severity.Info, FixTier.UnityConverter, 0, "\"com.unity.postprocessing\": \"" + ppv + "\"", ppv);
            }
            if (ctx.ScriptCompilationFailed)
                sink.Add(RuleIds.ScriptCompilationFailed, Severity.Info, FixTier.Manual, 0, "");
        }
    }
}
