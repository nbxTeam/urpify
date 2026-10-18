using System;
using System.Collections.Generic;
using System.Linq;

namespace Urpify.Parsing
{
    public enum ProgramLanguage { CG, HLSL, GLSL }

    /// <summary>A CGPROGRAM/CGINCLUDE/HLSLPROGRAM/HLSLINCLUDE block. <see cref="Content"/> excludes the keywords.</summary>
    public sealed class ProgramBlock
    {
        public ProgramLanguage Language;
        public bool IsInclude;
        /// <summary>From the opening keyword to the end of the closing keyword.</summary>
        public Span Span;
        public Span Content;
        public int Line;
        /// <summary>SubShader index, -1 at Shader level.</summary>
        public int SubShaderIndex = -1;
        /// <summary>Pass index inside its SubShader, -1 for SubShader-level programs (surface shaders).</summary>
        public int PassIndex = -1;
        public HlslUnit Unit = new HlslUnit();

        public string Keyword => (Language == ProgramLanguage.CG ? "CG" : Language == ProgramLanguage.HLSL ? "HLSL" : "GLSL") + (IsInclude ? "INCLUDE" : "PROGRAM");
    }

    public enum PassKind { Pass, GrabPass, UsePass }

    public sealed class PassInfo
    {
        public PassKind Kind;
        public string Name = "";
        public Dictionary<string, string> Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>LightMode tag value as written, "" when absent.</summary>
        public string LightMode => Tags.TryGetValue("LightMode", out var v) ? v : "";
        /// <summary>Where each tag of the pass's own Tags blocks is written ("Key" = "Value"), comments skipped.</summary>
        public Dictionary<string, Span> TagSpans = new Dictionary<string, Span>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Offset right after the '{' of the pass's first Tags block; -1 when it has none.</summary>
        public int TagsOpen = -1;
        public ProgramBlock Program;
        /// <summary>For GrabPass: the texture name, "" for the anonymous form (_GrabTexture).</summary>
        public string GrabTextureName = "";
        /// <summary>For UsePass: "Shader/PASSNAME".</summary>
        public string UsePassName = "";
        public int Line;
        public Span Span;
        /// <summary>Render state statements, e.g. "Blend SrcAlpha OneMinusSrcAlpha".</summary>
        public List<StateStatement> States = new List<StateStatement>();
    }

    public sealed class StateStatement
    {
        public string Name = "";
        public string Args = "";
        public int Line;
        public Span Span;
    }

    public sealed class SubShaderInfo
    {
        public int Index;
        public Dictionary<string, string> Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string RenderPipeline => Tags.TryGetValue("RenderPipeline", out var v) ? v : "";
        public int Lod = -1;
        public List<PassInfo> Passes = new List<PassInfo>();
        /// <summary>Programs directly inside the SubShader (surface shaders, CGINCLUDE).</summary>
        public List<ProgramBlock> Programs = new List<ProgramBlock>();
        public List<StateStatement> States = new List<StateStatement>();
        public bool HasPackageRequirements;
        /// <summary>Offset right after the '{' of the SubShader's own first Tags block (comments skipped); -1 when it has none.</summary>
        public int TagsOpen = -1;
        public int Line;
        public Span Span;

        /// <summary>Drawn with transparent objects: a Queue tag above 2500 (Built-in's Transparent is 3000, AlphaTest 2450).</summary>
        public bool IsTransparentQueue
        {
            get
            {
                if (!Tags.TryGetValue("Queue", out var q)) return false;
                var m = System.Text.RegularExpressions.Regex.Match(q.Trim(), @"^(\d+)$|^([A-Za-z]+)\s*([+-]\s*\d+)?$");
                if (!m.Success) return false;
                if (m.Groups[1].Success) return int.Parse(m.Groups[1].Value) > 2500;
                var name = m.Groups[2].Value.ToLowerInvariant();
                int value = name == "background" ? 1000 : name == "alphatest" ? 2450 : name == "transparent" ? 3000 : name == "overlay" ? 4000 : 2000;
                if (m.Groups[3].Success) value += int.Parse(m.Groups[3].Value.Replace(" ", ""));
                return value > 2500;
            }
        }

        /// <summary>
        /// A screen-effect SubShader: every pass with a program has ZTest Always (its own or the SubShader's). Such
        /// shaders are run by Graphics.Blit with an explicit pass index, never drawn on meshes, so URP's rule of one
        /// pass per LightMode does not apply to them. A SubShader with a GrabPass or a ForwardBase/ForwardAdd pass draws an
        /// object in the scene whatever its ZTest: Mochie's Underwater Visuals (ZTest Always on a mesh around the camera)
        /// was taken for a screen effect, kept its Built-in LightMode tags and rendered pink in URP.
        /// </summary>
        public bool IsBlitShader
        {
            get
            {
                if (Passes.Any(p => p.Kind == PassKind.GrabPass
                    || p.LightMode.Equals("ForwardBase", StringComparison.OrdinalIgnoreCase)
                    || p.LightMode.Equals("ForwardAdd", StringComparison.OrdinalIgnoreCase))) return false;
                var programPasses = Passes.Where(p => p.Kind == PassKind.Pass && p.Program != null).ToList();
                if (programPasses.Count == 0) return false;
                // ZTest Off is Always in ShaderLab; vert_img is UnityCG's full-screen vertex function.
                bool Always(IEnumerable<StateStatement> states) => states.Any(x => x.Name.Equals("ZTest", StringComparison.OrdinalIgnoreCase)
                    && (x.Args.Trim().Equals("Always", StringComparison.OrdinalIgnoreCase) || x.Args.Trim().Equals("Off", StringComparison.OrdinalIgnoreCase)));
                bool VertImg(PassInfo p) => p.Program.Unit.Pragma("vertex")?.Args.FirstOrDefault() == "vert_img";
                return programPasses.All(p => Always(p.States) || Always(States) || VertImg(p));
            }
        }

        /// <summary>A skybox SubShader (Background queue, skybox preview or Background render type): URP draws all its passes.</summary>
        public bool IsSkybox
        {
            get
            {
                Tags.TryGetValue("Queue", out var q); Tags.TryGetValue("PreviewType", out var preview); Tags.TryGetValue("RenderType", out var type);
                return (q ?? "").StartsWith("Background", StringComparison.OrdinalIgnoreCase)
                    && ((preview ?? "").Equals("Skybox", StringComparison.OrdinalIgnoreCase) || (type ?? "").Equals("Background", StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>The surface-shader program of this SubShader, if any.</summary>
        public ProgramBlock SurfaceProgram => Programs.FirstOrDefault(p => !p.IsInclude && p.Unit.Pragma("surface") != null);
    }

    public sealed class ShaderProperty
    {
        public string Name = "";
        public string DisplayName = "";
        /// <summary>Type as written: Color, Vector, Float, Int, Integer, Range(0,1), 2D, 3D, Cube, 2DArray, CubeArray, Any.</summary>
        public string Type = "";
        public string Default = "";
        public string[] Attributes = Array.Empty<string>();
        public int Line;
        public Span Span;
    }

    /// <summary>Parsed .shader file. Positions refer to <see cref="Source"/>; untouched text is never rewritten.</summary>
    public sealed class ShaderDocument
    {
        public SourceText Source;
        public string ShaderName = "";
        public int ShaderLine;
        public List<ShaderProperty> Properties = new List<ShaderProperty>();
        /// <summary>The whole Properties { ... } block; empty when the shader has none.</summary>
        public Span PropertiesSpan;
        public List<SubShaderInfo> SubShaders = new List<SubShaderInfo>();
        /// <summary>Every program block in file order, including shader-level CGINCLUDE/HLSLINCLUDE.</summary>
        public List<ProgramBlock> Programs = new List<ProgramBlock>();
        public string Fallback = "";
        public int FallbackLine;
        public string CustomEditor = "";
        public List<ParseError> Errors = new List<ParseError>();

        /// <summary>
        /// Index of the SubShader URP picks: the first one whose RenderPipeline tag is empty or UniversalPipeline.
        /// -1 when none qualifies (URP then uses the Fallback).
        /// </summary>
        public int UrpSubShaderIndex
        {
            get
            {
                foreach (var s in SubShaders)
                {
                    var rp = s.RenderPipeline;
                    if (rp.Length == 0 || string.Equals(rp, "UniversalPipeline", StringComparison.OrdinalIgnoreCase)) return s.Index;
                }
                return -1;
            }
        }

        public SubShaderInfo UrpSubShader => UrpSubShaderIndex >= 0 ? SubShaders[UrpSubShaderIndex] : null;

        /// <summary>True when some SubShader is tagged for URP.</summary>
        public bool HasUrpSubShader => SubShaders.Any(s => string.Equals(s.RenderPipeline, "UniversalPipeline", StringComparison.OrdinalIgnoreCase));

        /// <summary>Shader-level and SubShader-level include blocks visible to programs of a SubShader.</summary>
        public IEnumerable<ProgramBlock> IncludeBlocksFor(int subShaderIndex) =>
            Programs.Where(p => p.IsInclude && (p.SubShaderIndex == -1 || p.SubShaderIndex == subShaderIndex));

        /// <summary>Rebuilds the text from spans. With no edits this is the original, byte for byte.</summary>
        public string Emit(IList<TextEdit> edits = null)
        {
            if (edits == null || edits.Count == 0) return Source.Text;
            // Insertions (empty spans) before a replacement that starts at the same place.
            var ordered = edits.OrderBy(e => e.Span.Start).ThenBy(e => e.Span.Length).ToList();
            var sb = new System.Text.StringBuilder(Source.Length + 256);
            int pos = 0;
            foreach (var e in ordered)
            {
                if (e.Span.Start < pos) throw new InvalidOperationException("Overlapping edits at " + e.Span);
                sb.Append(Source.Text, pos, e.Span.Start - pos);
                sb.Append(e.NewText);
                pos = e.Span.End;
            }
            sb.Append(Source.Text, pos, Source.Length - pos);
            return sb.ToString();
        }
    }

    public sealed class TextEdit
    {
        public Span Span;
        public string NewText = "";
        public TextEdit(Span span, string newText) { Span = span; NewText = newText ?? ""; }
    }
}
