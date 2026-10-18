using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Urpify.Generation;

namespace Urpify.Analysis
{
    /// <summary>One compiler message of a checked variant.</summary>
    [Serializable]
    public sealed class CompileMessage
    {
        public string Pass = "";
        /// <summary>Index of the SubShader as Unity serialized it; -1 for import messages.</summary>
        public int SubShader = -1;
        public string Stage = "";
        public string Keywords = "";
        public int Line;
        public string File = "";
        public string Message = "";
        public override string ToString() => $"{Pass} {Stage} [{Keywords}] {File}:{Line} {Message}";
    }

    /// <summary>
    /// Compiles the vertex and fragment programs of every pass for the given keyword sets (TZ 6.1: a converted shader
    /// must compile for the keywords its materials use). Importing a shader does not compile HLSL: a call with the wrong
    /// arguments imports without a message (measured in M3), so this is the check the converter relies on.
    /// </summary>
    public static class ShaderCompileCheck
    {
        public static ShaderCompilerPlatform CurrentPlatform()
        {
            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Vulkan: return ShaderCompilerPlatform.Vulkan;
                case GraphicsDeviceType.Metal: return ShaderCompilerPlatform.Metal;
                case GraphicsDeviceType.OpenGLCore: return ShaderCompilerPlatform.OpenGLCore;
                default: return ShaderCompilerPlatform.D3D;
            }
        }

        /// <summary>
        /// The same error regardless of its line (a file with an inserted SubShader has the old errors further down):
        /// pass, stage, keywords and message.
        /// </summary>
        public static string Key(CompileMessage m) => m.Pass + "|" + m.Stage + "|" + m.Keywords + "|" + m.Message;

        /// <summary>
        /// Errors of a two-pipeline file (<paramref name="now"/>) that the original shader (<paramref name="before"/>) did
        /// not have. The file's first <paramref name="urpSubShaders"/> SubShaders are the added URP ones: all their errors
        /// count. A Built-in SubShader's error counts when the original's SubShader at the same place did not have it
        /// (pass, stage, keywords, message; lines move). Import errors carry no SubShader: one counts when the file has
        /// more of that message than the original had (the URP part reporting the original's missing include again).
        /// </summary>
        public static List<CompileMessage> NewErrors(IEnumerable<CompileMessage> now, IEnumerable<CompileMessage> before, int urpSubShaders = 0)
        {
            var old = (before ?? Enumerable.Empty<CompileMessage>()).ToList();
            var oldKeys = new HashSet<string>(old.Where(e => e.SubShader >= 0).Select(e => e.SubShader + "|" + Key(e)));
            var oldImports = old.Where(e => e.SubShader < 0).GroupBy(e => e.Message).ToDictionary(g => g.Key, g => g.Count());
            var seenImports = new Dictionary<string, int>();
            var result = new List<CompileMessage>();
            foreach (var e in now)
            {
                if (e.SubShader < 0)
                {
                    seenImports[e.Message] = seenImports.TryGetValue(e.Message, out var n) ? n + 1 : 1;
                    if (seenImports[e.Message] > (oldImports.TryGetValue(e.Message, out var m) ? m : 0)) result.Add(e);
                }
                else if (e.SubShader < urpSubShaders || !oldKeys.Contains(e.SubShader - urpSubShaders + "|" + Key(e))) result.Add(e);
            }
            return result;
        }

        /// <summary>SubShaders tagged for URP at the start of a shader's text (the ones Keep Built-in support adds).</summary>
        public static int LeadingUrpSubShaders(string shaderText, string path = "")
        {
            try
            {
                var doc = Urpify.Parsing.ShaderLabParser.Parse(shaderText, path);
                return doc.SubShaders.TakeWhile(x => x.RenderPipeline.Equals("UniversalPipeline", StringComparison.OrdinalIgnoreCase)).Count();
            }
            catch { return 0; }
        }

        /// <summary>
        /// The stages compiled, each where the pass has it. The tessellation and geometry stages too: GarrettGunnell's FFTWater
        /// compiled its vertex and fragment programs after conversion while its hull program did not (URP has no
        /// UNITY_domain), so it was written as converted and rendered pink.
        /// </summary>
        static readonly ShaderType[] Stages = { ShaderType.Vertex, ShaderType.Fragment, ShaderType.Hull, ShaderType.Domain, ShaderType.Geometry };

        /// <summary>Errors of every pass that has a vertex/fragment program, for each keyword set (empty set = no keywords).</summary>
        public static List<CompileMessage> Errors(Shader shader, IEnumerable<string[]> keywordSets = null)
        {
            var sets = (keywordSets ?? new[] { Array.Empty<string>() }).ToList();
            if (sets.Count == 0) sets.Add(Array.Empty<string>());
            var result = new List<CompileMessage>();
            // Import errors first: a preprocessor error (an "#if" on an empty define) drops a pass's programs at import,
            // and the passes left then have nothing to compile (ShaderTutorials 033_River, M3).
            foreach (var m in ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == ShaderCompilerMessageSeverity.Error))
                result.Add(new CompileMessage { Pass = "import", Line = m.line, File = m.file ?? "", Message = m.message });
            var data = ShaderUtil.GetShaderData(shader);
            if (data == null) return result;
            var platform = CurrentPlatform();
            var target = EditorUserBuildSettings.activeBuildTarget;
            for (int s = 0; s < data.SerializedSubshaderCount; s++)
            {
                var sub = data.GetSerializedSubshader(s);
                for (int p = 0; p < sub.PassCount; p++)
                {
                    var pass = sub.GetPass(p);
                    if (!pass.HasShaderStage(ShaderType.Vertex) && !pass.HasShaderStage(ShaderType.Fragment)) continue;
                    foreach (var keywords in sets)
                        foreach (var stage in Stages)
                        {
                            if (!pass.HasShaderStage(stage)) continue;
                            var info = pass.CompileVariant(stage, keywords, platform, target);
                            foreach (var m in info.Messages.Where(m => m.severity == ShaderCompilerMessageSeverity.Error))
                                result.Add(new CompileMessage
                                {
                                    Pass = string.IsNullOrEmpty(pass.Name) ? $"#{p}" : pass.Name,
                                    SubShader = s,
                                    Stage = stage.ToString(),
                                    Keywords = string.Join(" ", keywords),
                                    Line = m.line,
                                    File = m.file ?? "",
                                    Message = m.message,
                                });
                            if (!info.Success && !info.Messages.Any(m => m.severity == ShaderCompilerMessageSeverity.Error))
                                result.Add(new CompileMessage { Pass = pass.Name, SubShader = s, Stage = stage.ToString(), Keywords = string.Join(" ", keywords), Message = "Compilation failed without a message." });
                        }
                }
            }
            return result;
        }
    }
}
