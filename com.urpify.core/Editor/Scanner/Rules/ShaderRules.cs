using System;
using System.Collections.Generic;
using System.Linq;
using Urpify.Parsing;

namespace Urpify.Rules
{
    static class ShaderRuleUtil
    {
        /// <summary>True when the SubShader URP picks is already written for URP: construct rules do not apply.</summary>
        public static bool UrpReady(ShaderContext ctx)
        {
            var sub = ctx.Doc?.UrpSubShader;
            return sub != null && string.Equals(sub.RenderPipeline, "UniversalPipeline", StringComparison.OrdinalIgnoreCase);
        }

        public static IEnumerable<PassInfo> UrpPasses(ShaderContext ctx) =>
            ctx.Doc?.UrpSubShader?.Passes ?? Enumerable.Empty<PassInfo>();

        public static bool IsDrawnLightMode(string lightMode) =>
            lightMode.Length == 0 || string.Equals(lightMode, "SRPDefaultUnlit", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(lightMode, "UniversalForward", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(lightMode, "UniversalForwardOnly", StringComparison.OrdinalIgnoreCase);

        /// <summary>Units a pass compiles: its program, include blocks visible to it, and local include files.</summary>
        public static IEnumerable<(HlslUnit unit, string file)> PassUnits(ShaderContext ctx, PassInfo pass)
        {
            if (pass.Program != null) yield return (pass.Program.Unit, ctx.AssetPath);
            var sub = ctx.Doc.UrpSubShader;
            if (sub != null)
                foreach (var inc in ctx.Doc.IncludeBlocksFor(sub.Index)) yield return (inc.Unit, ctx.AssetPath);
            foreach (var (inc, unit) in IncludeResolver.Flatten(ctx.Includes)) yield return (unit, inc.Path);
        }

        public static (string file, int line) Find(IEnumerable<(HlslUnit unit, string file)> units, string identifier)
        {
            foreach (var (unit, file) in units)
                if (unit.Uses(identifier)) return (file, unit.FirstLine(identifier));
            return ("", 0);
        }

        public static int LineIn(ShaderContext ctx, string file, int line) => file == ctx.AssetPath ? line : 0;
    }

    [Rule(RuleIds.ShaderAlreadyUrp, Order = -10)]
    sealed class ShaderAlreadyUrpRule : IShaderRule
    {
        public void Check(ShaderContext ctx, FindingSink sink)
        {
            if (!ShaderRuleUtil.UrpReady(ctx)) return;
            var sub = ctx.Doc.UrpSubShader;
            sink.Add(RuleIds.ShaderAlreadyUrp, Severity.Info, FixTier.None, sub.Line, ctx.Doc.Source.LineText(sub.Line));
        }
    }

    [Rule(RuleIds.Surface)]
    sealed class SurfaceShaderRule : IShaderRule
    {
        static readonly HashSet<string> BuiltInModels = new HashSet<string>(StringComparer.Ordinal) { "Standard", "StandardSpecular", "Lambert", "BlinnPhong" };

        /// <summary>#pragma surface options from ShaderTZ 6.3, plus harmless ones seen in real projects (noinstancing, softvegetation).</summary>
        static readonly HashSet<string> KnownFlags = new HashSet<string>(StringComparer.Ordinal)
        {
            "alpha", "addshadow", "fullforwardshadows", "keepalpha", "noshadow", "noambient", "novertexlights", "nolightmap",
            "nodynlightmap", "nodirlightmap", "nofog", "nometa", "noforwardadd", "nolppv", "noshadowmask", "halfasview",
            "interpolateview", "approxview", "dualforward", "dithercrossfade", "noinstancing", "softvegetation",
        };

        static readonly HashSet<string> KnownKeyed = new HashSet<string>(StringComparer.Ordinal)
        {
            "vertex", "finalcolor", "finalgbuffer", "alpha", "alphatest", "decal", "exclude_path", "tessellate", "tessphong",
        };

        public void Check(ShaderContext ctx, FindingSink sink)
        {
            if (ShaderRuleUtil.UrpReady(ctx)) return;
            var sub = ctx.Doc.UrpSubShader;
            var prog = sub?.SurfaceProgram;
            if (prog == null) return;
            var pragma = prog.Unit.Pragma("surface");
            var args = pragma.Args;
            var model = args.Length > 1 ? args[1] : "";
            var options = args.Skip(2).ToList();
            sink.Add(RuleIds.Surface, Severity.Pink, FixTier.Auto, pragma.Line, "#pragma " + pragma.Raw,
                model, options.Count == 0 ? "-" : string.Join(" ", options));

            if (model.Length > 0 && !BuiltInModels.Contains(model))
            {
                var fnName = "Lighting" + model;
                int line = 0;
                string file = "";
                foreach (var (unit, f, _) in ctx.UrpUnits())
                {
                    var fn = unit.Functions.FirstOrDefault(x => x.Name == fnName || x.Name.StartsWith(fnName + "_", StringComparison.Ordinal));
                    if (fn != null) { line = fn.Line; file = f; break; }
                }
                // Where the converter looks too: includes on any depth and functions a #define declares (QA-011: the scan said
                // "not found" for a macro-declared LightingToonMacro that Pro then converted).
                bool byMacro = false;
                if (file.Length == 0)
                {
                    try
                    {
                        var scope = Analysis.ProgramScope.Of(ctx.Doc, ctx.Doc.Programs.Where(p => p.SubShaderIndex == sub.Index || p.IsInclude));
                        byMacro = scope.FunctionPart(fnName).fn != null;
                    }
                    catch { }
                }
                if (byMacro)
                    sink.Add(RuleIds.SurfaceCustomLighting, Severity.Pink, FixTier.AutoApprox, pragma.Line, "#pragma " + pragma.Raw, model, fnName);
                else if (file.Length > 0)
                    sink.Add(RuleIds.SurfaceCustomLighting, Severity.Pink, FixTier.AutoApprox, ShaderRuleUtil.LineIn(ctx, file, line), ctx.Evidence(file, line), model, fnName);
                else
                    sink.Add(RuleIds.SurfaceCustomLighting, Severity.Pink, FixTier.NotSupportedYet, pragma.Line, "#pragma " + pragma.Raw, model, fnName);
            }

            var unknown = new List<string>();
            foreach (var o in options)
            {
                int colon = o.IndexOf(':');
                var key = colon > 0 ? o.Substring(0, colon) : o;
                if (colon > 0 ? KnownKeyed.Contains(key) : KnownFlags.Contains(o)) continue;
                unknown.Add(o);
            }
            if (options.Any(o => o.StartsWith("tessellate:", StringComparison.Ordinal)))
                sink.Add(RuleIds.SurfaceTessellation, Severity.Pink, FixTier.AutoApprox, pragma.Line, "#pragma " + pragma.Raw,
                    options.First(o => o.StartsWith("tessellate:", StringComparison.Ordinal)).Substring("tessellate:".Length));
            if (options.Any(o => o.StartsWith("finalgbuffer:", StringComparison.Ordinal)))
                sink.Add(RuleIds.SurfaceFinalGBuffer, Severity.Info, FixTier.Auto, pragma.Line, "#pragma " + pragma.Raw);
            if (unknown.Count > 0)
                sink.Add(RuleIds.SurfaceUnknownOption, Severity.Pink, FixTier.NotSupportedYet, pragma.Line, "#pragma " + pragma.Raw, string.Join(" ", unknown));

            var extra = sub.Passes.Where(p => p.Kind == PassKind.Pass).ToList();
            if (extra.Count > 0)
                sink.Add(RuleIds.SurfaceExtraPass, Severity.Pink, FixTier.AutoApprox, extra[0].Line, ctx.Doc.Source.LineText(extra[0].Line), extra.Count);
        }
    }

    [Rule(RuleIds.ForwardBase)]
    sealed class PassLightModeRule : IShaderRule
    {
        public void Check(ShaderContext ctx, FindingSink sink)
        {
            if (ShaderRuleUtil.UrpReady(ctx) || ctx.Doc.UrpSubShader?.SurfaceProgram != null) return;
            var passes = ShaderRuleUtil.UrpPasses(ctx).ToList();
            bool anyLegacy = passes.Any(p => p.Kind == PassKind.Pass && Scanner.UrpFacts.LegacyLightModes.Contains(p.LightMode));
            foreach (var p in passes.Where(p => p.Kind == PassKind.Pass))
            {
                var lm = p.LightMode;
                var ev = ctx.Doc.Source.LineText(p.Line);
                if (lm.Equals("ForwardBase", StringComparison.OrdinalIgnoreCase))
                    sink.Add(RuleIds.ForwardBase, Severity.Pink, FixTier.Auto, p.Line, ev);
                else if (lm.Equals("ForwardAdd", StringComparison.OrdinalIgnoreCase))
                    sink.Add(RuleIds.ForwardAdd, anyLegacy ? Severity.Pink : Severity.Silent, FixTier.AutoApprox, p.Line, ev);
                else if (lm.Equals("ShadowCaster", StringComparison.OrdinalIgnoreCase))
                    sink.Add(RuleIds.ShadowCasterPass, Severity.Info, FixTier.Auto, p.Line, ev);
                else if (lm.Equals("Always", StringComparison.OrdinalIgnoreCase))
                    sink.Add(RuleIds.LegacyLightMode, Severity.Pink, FixTier.Auto, p.Line, ev, lm);
                else if (Scanner.UrpFacts.LegacyLightModes.Contains(lm))
                    sink.Add(RuleIds.LegacyLightMode, Severity.Pink, FixTier.NotSupportedYet, p.Line, ev, lm);
                else if (lm.Equals("Deferred", StringComparison.OrdinalIgnoreCase) || lm.Equals("PrepassFinal", StringComparison.OrdinalIgnoreCase))
                    sink.Add(RuleIds.LegacyLightMode, Severity.Info, FixTier.None, p.Line, ev, lm);
            }
            foreach (var p in passes.Where(p => p.Kind == PassKind.UsePass))
            {
                if (p.UsePassName.StartsWith("Legacy Shaders/", StringComparison.Ordinal) || p.UsePassName.StartsWith("VertexLit/", StringComparison.Ordinal))
                    sink.Add(RuleIds.UsePassBuiltIn, Severity.Info, FixTier.Auto, p.Line, ctx.Doc.Source.LineText(p.Line), p.UsePassName);
                // A pass of a shader the project does not have: Built-in draws the object pink too (QA-011). Broken before the
                // migration, so it is the project's to fix, not Urpify's.
                var slash = p.UsePassName.LastIndexOf('/');
                var shaderName = slash > 0 ? p.UsePassName.Substring(0, slash) : "";
                if (shaderName.Length > 0 && UnityEngine.Shader.Find(shaderName) == null)
                    sink.Add(RuleIds.UsePassMissing, Severity.Pink, FixTier.Manual, p.Line, ctx.Doc.Source.LineText(p.Line), p.UsePassName, shaderName);
            }
        }
    }

    [Rule(RuleIds.MultipleUnlitPasses)]
    sealed class MultipleUntaggedPassesRule : IShaderRule
    {
        public void Check(ShaderContext ctx, FindingSink sink)
        {
            if (ShaderRuleUtil.UrpReady(ctx)) return;
            // Screen-effect shaders run each pass through Graphics.Blit with its index; URP's one-pass-per-LightMode rule is for meshes.
            if (ctx.Doc.UrpSubShader?.IsBlitShader == true || ctx.Doc.UrpSubShader?.IsSkybox == true) return;
            var untagged = ShaderRuleUtil.UrpPasses(ctx).Where(p => p.Kind == PassKind.Pass && p.LightMode.Length == 0).ToList();
            if (untagged.Count < 2) return;
            sink.Add(RuleIds.MultipleUnlitPasses, Severity.Silent, FixTier.Auto, untagged[1].Line, ctx.Doc.Source.LineText(untagged[1].Line), untagged.Count);
        }
    }

    [Rule(RuleIds.BuiltInLightingVariables)]
    sealed class BuiltInLightingVariablesRule : IShaderRule
    {
        public void Check(ShaderContext ctx, FindingSink sink)
        {
            if (ShaderRuleUtil.UrpReady(ctx)) return;
            foreach (var p in ShaderRuleUtil.UrpPasses(ctx).Where(p => p.Kind == PassKind.Pass && ShaderRuleUtil.IsDrawnLightMode(p.LightMode)))
            {
                foreach (var v in Scanner.UrpFacts.BuiltInOnlyLightVariables)
                {
                    var (file, line) = ShaderRuleUtil.Find(ShaderRuleUtil.PassUnits(ctx, p), v);
                    if (line == 0) continue;
                    sink.Add(RuleIds.BuiltInLightingVariables, Severity.Silent, FixTier.Auto, ShaderRuleUtil.LineIn(ctx, file, line), ctx.Evidence(file, line), v);
                    return;
                }
            }
        }
    }

    [Rule(RuleIds.GrabPass)]
    sealed class GrabPassRule : IShaderRule
    {
        public void Check(ShaderContext ctx, FindingSink sink)
        {
            if (ShaderRuleUtil.UrpReady(ctx)) return;
            foreach (var g in ShaderRuleUtil.UrpPasses(ctx).Where(p => p.Kind == PassKind.GrabPass))
                sink.Add(RuleIds.GrabPass, Severity.Silent, FixTier.AutoApprox, g.Line, ctx.Doc.Source.LineText(g.Line),
                    g.GrabTextureName.Length == 0 ? "_GrabTexture" : g.GrabTextureName);
        }
    }

    /// <summary>Identifier-based constructs: depth, depth-normals, projector matrices, image-effect helpers.</summary>
    abstract class IdentifierRule : IShaderRule
    {
        protected abstract string Id { get; }
        protected abstract string[] Identifiers { get; }
        protected abstract Severity Severity { get; }
        protected abstract FixTier Tier { get; }

        public void Check(ShaderContext ctx, FindingSink sink)
        {
            if (ShaderRuleUtil.UrpReady(ctx)) return;
            foreach (var id in Identifiers)
            {
                var (file, line) = ctx.FindIdentifier(id);
                if (line == 0) continue;
                sink.Add(Id, Severity, Tier, ShaderRuleUtil.LineIn(ctx, file, line), ctx.Evidence(file, line), id);
                return;
            }
        }
    }

    [Rule(RuleIds.DepthTexture)]
    sealed class DepthTextureRule : IShaderRule
    {
        static readonly string[] Identifiers = { "_CameraDepthTexture", "_LastCameraDepthTexture" };

        public void Check(ShaderContext ctx, FindingSink sink)
        {
            if (ShaderRuleUtil.UrpReady(ctx)) return;
            foreach (var id in Identifiers)
            {
                var (file, line) = ctx.FindIdentifier(id);
                if (line == 0) continue;
                // Drawn with transparent objects, it needs the depth before URP's default copy (after transparent objects):
                // on URP 17 it then reads no depth, on URP 14 the previous frame's (lab D01, M4). Same verdict on both
                // versions: a still frame on 2022.3 looks right, anything that moves does not.
                var sub = ctx.Doc?.UrpSubShader;
                var transparent = sub != null && sub.IsTransparentQueue;
                sink.Add(transparent ? RuleIds.DepthTextureTransparent : RuleIds.DepthTexture, Severity.Silent, FixTier.Auto,
                    ShaderRuleUtil.LineIn(ctx, file, line), ctx.Evidence(file, line), id);
                return;
            }
        }
    }

    [Rule(RuleIds.DepthNormalsTexture)]
    sealed class DepthNormalsTextureRule : IdentifierRule
    {
        protected override string Id => RuleIds.DepthNormalsTexture;
        protected override string[] Identifiers => new[] { "_CameraDepthNormalsTexture" };
        protected override Severity Severity => Severity.Silent;
        protected override FixTier Tier => FixTier.AutoApprox;
    }

    /// <summary>
    /// Built-in's deferred G-buffer (_CameraGBufferTexture0-3). URP's Forward renderer has no G-buffer, and its Deferred
    /// path names and packs its own differently (_GBuffer0-6, UnityGBuffer.hlsl; URP 14.0.12 and 17.5.0), so the shader
    /// reads empty textures and the effect loses its input without an error. Not converted yet.
    /// </summary>
    [Rule(RuleIds.GBufferTexture)]
    sealed class GBufferTextureRule : IdentifierRule
    {
        protected override string Id => RuleIds.GBufferTexture;
        protected override string[] Identifiers => new[] { "_CameraGBufferTexture0", "_CameraGBufferTexture1", "_CameraGBufferTexture2", "_CameraGBufferTexture3" };
        protected override Severity Severity => Severity.Silent;
        protected override FixTier Tier => FixTier.NotSupportedYet;
    }

    [Rule(RuleIds.ProjectorShader)]
    sealed class ProjectorShaderRule : IdentifierRule
    {
        protected override string Id => RuleIds.ProjectorShader;
        protected override string[] Identifiers => new[] { "unity_Projector", "unity_ProjectorClip" };
        protected override Severity Severity => Severity.Silent;
        protected override FixTier Tier => FixTier.AutoApprox;
    }

    [Rule(RuleIds.ImageEffectShader)]
    sealed class ImageEffectShaderRule : IdentifierRule
    {
        protected override string Id => RuleIds.ImageEffectShader;
        protected override string[] Identifiers => new[] { "vert_img", "v2f_img" };
        protected override Severity Severity => Severity.Info;
        protected override FixTier Tier => FixTier.Auto;
    }

    [Rule(RuleIds.MissingInclude)]
    sealed class MissingIncludeRule : IShaderRule
    {
        public void Check(ShaderContext ctx, FindingSink sink)
        {
            // Includes from Unity's render-pipeline packages are expected to be missing in a Built-in project
            // (dual-mode and SRP shaders); they are not a problem of this project.
            foreach (var inc in ctx.Includes.Where(i => i.Kind == IncludeKind.Missing
                                                         && !i.Written.StartsWith("Packages/com.unity.render-pipelines.", StringComparison.OrdinalIgnoreCase)))
                sink.Add(RuleIds.MissingInclude, Severity.Info, FixTier.Manual, inc.Line, "#include \"" + inc.Written + "\"", inc.Written);
        }
    }

    /// <summary>
    /// Cross-check with Unity's compiled data. Adds a finding only when URP's behaviour is worse than every
    /// construct found above, so a shader never looks fine while Unity says it breaks.
    /// </summary>
    [Rule(RuleIds.ShaderLegacyPass, Order = 100)]
    sealed class CompiledPassesRule : IShaderRule
    {
        public void Check(ShaderContext ctx, FindingSink sink)
        {
            if (ctx.Compiled == null || ctx.Compiled.HasErrors) return;
            var sub = Scanner.ShaderUrpEvaluator.UrpSubShader(ctx.Compiled);
            var worst = SeverityUtil.Worst(sink.Findings);
            if (sub == null)
            {
                if (worst < Severity.Pink && ctx.Doc?.Fallback.Length == 0)
                    sink.Add(RuleIds.ShaderNoUrpPass, Severity.Pink, FixTier.NotSupportedYet, 0, "", "-");
                return;
            }
            var legacy = sub.LightModes.Where(m => Scanner.UrpFacts.LegacyLightModes.Contains(m)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (legacy.Count > 0 && worst < Severity.Pink)
            {
                sink.Add(RuleIds.ShaderLegacyPass, Severity.Pink, FixTier.NotSupportedYet, 0, "", string.Join(", ", legacy));
                return;
            }
            if (legacy.Count == 0 && sub.LightModes.Count > 0 && !sub.LightModes.Any(Scanner.UrpFacts.DrawnLightModes.Contains) && worst < Severity.Silent)
                sink.Add(RuleIds.ShaderNoUrpPass, Severity.Silent, FixTier.NotSupportedYet, 0, "",
                    string.Join(", ", sub.LightModes.Select(m => m.Length == 0 ? "(none)" : m)));
        }
    }

    [Rule(RuleIds.ShaderCompileErrors, Order = 200)]
    sealed class CompileErrorsRule : IShaderRule
    {
        public void Check(ShaderContext ctx, FindingSink sink)
        {
            if (ctx.Compiled == null || !ctx.Compiled.HasErrors) return;
            var first = ctx.Compiled.Errors.FirstOrDefault() ?? "";
            sink.Add(RuleIds.ShaderCompileErrors, Severity.Info, FixTier.Manual, 0, first, ctx.Compiled.Errors.Count);
        }
    }
}
