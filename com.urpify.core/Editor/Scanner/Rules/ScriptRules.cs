using System;
using System.Linq;
using Urpify.Parsing;

namespace Urpify.Rules
{
    static class ScriptRuleUtil
    {
        /// <summary>Editor-only code does not run in the player; what it creates is scanned as assets instead.</summary>
        public static bool IsEditorScript(ScriptContext ctx) =>
            ctx.AssetPath.Replace('\\', '/').IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0;

        public static string Line(ScriptContext ctx, int line) => ctx.Code.Source.LineText(line);
    }

    /// <summary>
    /// OnRenderImage (TZ 6.7). Pro runs the method under URP through Urpify's renderer feature, its Blit calls recorded
    /// into the frame: one Blit is the effect as it was (Auto), a chain or command buffers AutoApprox, immediate drawing
    /// (GL, RenderTexture.active, DrawMeshNow, SetPass) cannot be recorded (Manual). The same test as the converter's.
    /// </summary>
    [Rule(RuleIds.OnRenderImage)]
    sealed class OnRenderImageRule : IScriptRule
    {
        public void Check(ScriptContext ctx, FindingSink sink)
        {
            if (ScriptRuleUtil.IsEditorScript(ctx)) return;
            // Converted by Urpify: the Built-in Camera Hooks renderer feature runs it under URP.
            if (ctx.Code.HasIdentifier("OnRenderImageInUrp") || ctx.Code.HasIdentifier("OnRenderImageInUrpAttribute")) return;
            foreach (var m in ctx.Code.Methods.Where(m => m.Name == "OnRenderImage" && m.BodyStart >= 0))
            {
                int blits = Analysis.CameraScriptFacts.CountCalls(ctx.Code, m, "Blit");
                bool buffers = Analysis.CameraScriptFacts.CountCalls(ctx.Code, m, "ExecuteCommandBuffer") > 0;
                var immediate = Analysis.CameraScriptFacts.ImmediateDrawing(ctx.Code, m);
                var rendersCamera = Analysis.CameraScriptFacts.RendersCamera(ctx.Code, m);
                bool opaque = m.Attributes.Contains("ImageEffectOpaque");
                bool recorded = immediate == null && Analysis.CameraScriptFacts.RecordedImmediate(ctx.Code, m) != null;
                // Drawing in other scripts the method calls is recorded too, as a chain (ImageEffectReach; the converter says the same).
                bool helpers = ctx.Reach != null && ctx.Reach.Helpers.Count > 0;
                var tier = immediate != null || rendersCamera != null ? FixTier.NotSupportedYet : blits > 1 || buffers || recorded || helpers ? FixTier.AutoApprox : FixTier.Auto;
                var f = sink.Add(RuleIds.OnRenderImage, Severity.Silent, tier, m.Line,
                    ScriptRuleUtil.Line(ctx, m.Line), blits, opaque ? "AfterRenderingSkybox" : "AfterRenderingPostProcessing", immediate ?? rendersCamera ?? "");
                if (immediate == null && rendersCamera != null)
                {
                    // The finding's own arguments hold rendersCamera at {2}, the only one this text uses.
                    f.ManualKey = "rule." + RuleIds.OnRenderImage + ".manualRender";
                    f.ManualSteps = Loc.Get(f.ManualKey, blits, "", rendersCamera);
                }
            }
        }
    }

    [Rule(RuleIds.CameraCallbacks)]
    sealed class CameraCallbacksRule : IScriptRule
    {
        public void Check(ScriptContext ctx, FindingSink sink)
        {
            if (ScriptRuleUtil.IsEditorScript(ctx)) return;
            // Converted by Urpify: CameraCallbacksInUrp calls them under URP.
            if (ctx.Code.HasIdentifier("CameraCallbacksInUrp") || ctx.Code.HasIdentifier("CameraCallbacksInUrpAttribute")) return;
            foreach (var name in Scanner.UrpFacts.DeadCameraCallbacks)
                foreach (var m in ctx.Code.Methods.Where(m => m.Name == name && m.BodyStart >= 0))
                {
                    // Pro calls the method from URP's begin/endCameraRendering (TZ 6.9).
                    sink.Add(RuleIds.CameraCallbacks, Severity.Silent, FixTier.AutoApprox, m.Line, ScriptRuleUtil.Line(ctx, m.Line), name);
                }
        }
    }

    /// <summary>
    /// Camera.onPreCull, onPreRender, onPostRender: static delegates Built-in calls for every camera; URP never calls them,
    /// so the code subscribed to them stops running without an error (QA-011). Urpify does not convert them yet.
    /// </summary>
    [Rule(RuleIds.CameraDelegates)]
    sealed class CameraDelegatesRule : IScriptRule
    {
        static readonly string[] Names = { "onPreCull", "onPreRender", "onPostRender" };

        public void Check(ScriptContext ctx, FindingSink sink)
        {
            if (ScriptRuleUtil.IsEditorScript(ctx)) return;
            foreach (var name in Names)
                foreach (var i in ctx.Code.FindAll("Camera", ".", name))
                {
                    var line = ctx.Code.Tokens[i].Line;
                    sink.Add(RuleIds.CameraDelegates, Severity.Silent, FixTier.NotSupportedYet, line, ScriptRuleUtil.Line(ctx, line), name);
                }
        }
    }

    /// <summary>
    /// A mirror or water reflection (MirrorReflection, Standard Assets Water): OnWillRenderObject renders a camera, or hands
    /// Camera.current to code that does. URP 17 sends OnWillRenderObject from ScriptableRenderContext.Cull, and rendering
    /// another camera there throws (UniversalCameraData already created: lab M01, M7). Pro: [OnWillRenderObjectInUrp].
    /// </summary>
    [Rule(RuleIds.WillRenderCamera)]
    sealed class WillRenderCameraRule : IScriptRule
    {
        public void Check(ScriptContext ctx, FindingSink sink)
        {
            if (ScriptRuleUtil.IsEditorScript(ctx)) return;
            var code = ctx.Code;
            if (code.HasIdentifier("OnWillRenderObjectInUrp") || code.HasIdentifier("OnWillRenderObjectInUrpAttribute")) return;
            var renders = Analysis.CameraScriptFacts.CameraRenderCalls(code);
            bool current = code.Find(0, "Camera", ".", "current") >= 0;
            if (renders.Count == 0 && !current) return;
            foreach (var m in code.Methods.Where(m => m.Name == "OnWillRenderObject" && m.BodyStart >= 0))
                sink.Add(RuleIds.WillRenderCamera, Severity.Silent, FixTier.AutoApprox, m.Line, ScriptRuleUtil.Line(ctx, m.Line));
        }
    }

    [Rule(RuleIds.CameraCommandBuffer)]
    sealed class CommandBufferRule : IScriptRule
    {
        public void Check(ScriptContext ctx, FindingSink sink)
        {
            if (ScriptRuleUtil.IsEditorScript(ctx)) return;
            var code = ctx.Code;
            foreach (var name in new[] { "AddCommandBuffer", "AddCommandBufferAsync" })
                foreach (var i in code.FindAll(name, "("))
                {
                    string evt = "";
                    bool light = false;
                    for (int k = i + 2; k < Math.Min(code.Tokens.Count, i + 16); k++)
                    {
                        var t = code.Tokens[k].Text;
                        if (t == ")" ) break;
                        if ((t == "CameraEvent" || t == "LightEvent") && k + 2 < code.Tokens.Count && code.Tokens[k + 1].Is("."))
                        {
                            evt = t + "." + code.Tokens[k + 2].Text;
                            light = t == "LightEvent";
                            break;
                        }
                    }
                    int line = code.Tokens[i].Line;
                    var eventName = evt.Length > 0 ? evt.Substring(evt.IndexOf('.') + 1) : "";
                    if (light)
                        sink.Add(RuleIds.LightCommandBuffer, Severity.Silent, FixTier.NotSupportedYet, line, ScriptRuleUtil.Line(ctx, line), evt);
                    else
                    {
                        // Pro runs the camera's buffer at the matching URP event (TZ 6.8); Built-in's deferred events have none.
                        var tier = Analysis.CameraScriptFacts.DeferredEvents.Contains(eventName) ? FixTier.AutoApprox
                            : eventName.Length == 0 || Analysis.CameraScriptFacts.DepthEvents.Contains(eventName) ? FixTier.AutoApprox : FixTier.Auto;
                        sink.Add(RuleIds.CameraCommandBuffer, Severity.Silent, tier, line, ScriptRuleUtil.Line(ctx, line), evt.Length > 0 ? evt : "?");
                    }
                }
        }
    }

    [Rule(RuleIds.ReplacementShader)]
    sealed class ReplacementShaderRule : IScriptRule
    {
        public void Check(ScriptContext ctx, FindingSink sink)
        {
            if (ScriptRuleUtil.IsEditorScript(ctx)) return;
            foreach (var name in new[] { "SetReplacementShader", "RenderWithShader" })
            {
                int i = ctx.Code.Find(0, name, "(");
                if (i < 0) continue;
                int line = ctx.Code.Tokens[i].Line;
                // Pro draws the camera's opaque objects with the replacement shader in Urpify's renderer feature (M7).
                sink.Add(RuleIds.ReplacementShader, Severity.Silent, FixTier.AutoApprox, line, ScriptRuleUtil.Line(ctx, line), name);
            }
        }
    }

    [Rule(RuleIds.ShaderFindBuiltIn)]
    sealed class ShaderFindRule : IScriptRule
    {
        public void Check(ScriptContext ctx, FindingSink sink)
        {
            if (ctx.ShaderOutcome == null) return;
            bool editor = ScriptRuleUtil.IsEditorScript(ctx);
            var code = ctx.Code;
            foreach (var i in code.FindAll("Shader", ".", "Find", "("))
            {
                if (i + 4 >= code.Tokens.Count || code.Tokens[i + 4].Kind != CSharpFile.TokKind.String) continue;
                var name = CSharpFile.StringValue(code.Tokens[i + 4]);
                if (name.Length == 0) continue;
                var o = ctx.ShaderOutcome(name);
                if (o == null || !o.Exists || !o.BuiltIn || o.Severity == Severity.Info) continue;
                int line = code.Tokens[i].Line;
                sink.Add(RuleIds.ShaderFindBuiltIn, editor ? Severity.Info : o.Severity, o.HasUnityUpgrader ? FixTier.Auto : FixTier.NotSupportedYet,
                    line, ScriptRuleUtil.Line(ctx, line), name);
            }
        }
    }

    /// <summary>
    /// Code that sets Built-in property names. URP Lit keeps _MainTex, _Color and _Glossiness only as hidden
    /// "ObsoleteProperties" that the shader never reads (URP 17.5.0 Shaders/Lit.shader lines 66-70, LitInput.hlsl),
    /// so after Unity's converter these calls stop changing anything on converted materials.
    /// </summary>
    [Rule(RuleIds.BuiltInPropertyNames)]
    sealed class BuiltInPropertyNamesRule : IScriptRule
    {
        static readonly System.Collections.Generic.Dictionary<string, string> Renames = new System.Collections.Generic.Dictionary<string, string>
            { { "_Color", "_BaseColor" }, { "_MainTex", "_BaseMap" }, { "_Glossiness", "_Smoothness" } };

        static readonly System.Collections.Generic.HashSet<string> Calls = new System.Collections.Generic.HashSet<string>
        {
            "SetColor", "SetTexture", "SetFloat", "SetVector", "SetTextureOffset", "SetTextureScale", "GetColor", "GetTexture",
            "GetFloat", "GetVector", "HasProperty", "PropertyToID",
        };

        public void Check(ScriptContext ctx, FindingSink sink)
        {
            var code = ctx.Code;
            var names = new System.Collections.Generic.List<string>();
            int firstLine = 0;
            for (int i = 0; i + 2 < code.Tokens.Count; i++)
            {
                var t = code.Tokens[i];
                if (t.Kind != CSharpFile.TokKind.Ident || !Calls.Contains(t.Text) || !code.Tokens[i + 1].Is("(")) continue;
                var arg = code.Tokens[i + 2];
                if (arg.Kind != CSharpFile.TokKind.String) continue;
                var v = CSharpFile.StringValue(arg);
                if (!Renames.ContainsKey(v)) continue;
                if (!names.Contains(v)) names.Add(v);
                if (firstLine == 0) firstLine = t.Line;
            }
            if (names.Count == 0) return;
            var pairs = string.Join(", ", names.Select(n => n + " -> " + Renames[n]));
            // Info: the rule cannot tell which material the code touches, and custom shaders keep these names
            // (ShaderTutorials scripts set _Color on their own shaders and render the same after conversion).
            sink.Add(RuleIds.BuiltInPropertyNames, Severity.Info, FixTier.Auto,
                firstLine, ScriptRuleUtil.Line(ctx, firstLine), pairs);
        }
    }

    [Rule(RuleIds.DepthTextureMode)]
    sealed class DepthTextureModeRule : IScriptRule
    {
        public void Check(ScriptContext ctx, FindingSink sink)
        {
            if (ScriptRuleUtil.IsEditorScript(ctx)) return;
            int i = ctx.Code.Find(0, "depthTextureMode");
            if (i < 0) return;
            int line = ctx.Code.Tokens[i].Line;
            sink.Add(RuleIds.DepthTextureMode, Severity.Info, FixTier.Auto, line, ScriptRuleUtil.Line(ctx, line));
        }
    }
}
