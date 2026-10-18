using System;
using System.Collections.Generic;
using System.Linq;

namespace Urpify.Parsing
{
    public enum DirectiveKind { Pragma, Include, Define, Undef, If, Ifdef, Ifndef, Elif, Else, Endif, Other }

    /// <summary>A preprocessor line. Conditionals are recorded, never evaluated.</summary>
    public sealed class HlslDirective
    {
        public DirectiveKind Kind;
        /// <summary>Directive name as written, e.g. "pragma".</summary>
        public string Name = "";
        /// <summary>Everything after the directive name, continuation lines joined.</summary>
        public string Rest = "";
        public int Line;
        public Span Span;
        /// <summary>How many #if levels enclose this directive.</summary>
        public int ConditionalDepth;
    }

    public sealed class HlslPragma
    {
        /// <summary>First word after #pragma, e.g. "surface", "vertex", "multi_compile".</summary>
        public string Name = "";
        public string[] Args = Array.Empty<string>();
        public int Line;
        public int ConditionalDepth;
        public string Raw = "";
    }

    public sealed class HlslInclude
    {
        public string Path = "";
        public int Line;
        public int ConditionalDepth;
    }

    public sealed class HlslField
    {
        public string Type = "";
        public string Name = "";
        public string Semantic = "";
        public int Line;
    }

    public sealed class HlslStruct
    {
        public string Name = "";
        public List<HlslField> Fields = new List<HlslField>();
        public int Line;
        public Span Span;
    }

    public sealed class HlslParam
    {
        public string Modifiers = "";
        public string Type = "";
        public string Name = "";
        public string Semantic = "";
    }

    public sealed class HlslFunction
    {
        public string ReturnType = "";
        public string Name = "";
        public List<HlslParam> Params = new List<HlslParam>();
        public string Semantic = "";
        public int Line;
        /// <summary>From the return type to the closing brace.</summary>
        public Span Span;
        /// <summary>Inside the braces.</summary>
        public Span Body;
        /// <summary>Identifiers used in the body.</summary>
        public HashSet<string> BodyIdentifiers = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>A top-level statement that is not a function or struct, e.g. "sampler2D _MainTex;" or "CBUFFER_START(UnityPerMaterial)".</summary>
    public sealed class HlslDeclaration
    {
        public string Text = "";
        public int Line;
        public Span Span;
        /// <summary>Type-like first word, e.g. "sampler2D", "float4", "uniform", "CBUFFER_START".</summary>
        public string Head = "";
        /// <summary>Declared name when recognizable, else "".</summary>
        public string Name = "";
    }

    /// <summary>Structure of one CG/HLSL program or include file.</summary>
    public sealed class HlslUnit
    {
        public List<HlslDirective> Directives = new List<HlslDirective>();
        public List<HlslPragma> Pragmas = new List<HlslPragma>();
        public List<HlslInclude> Includes = new List<HlslInclude>();
        public Dictionary<string, string> Defines = new Dictionary<string, string>(StringComparer.Ordinal);
        public List<HlslStruct> Structs = new List<HlslStruct>();
        public List<HlslFunction> Functions = new List<HlslFunction>();
        public List<HlslDeclaration> Declarations = new List<HlslDeclaration>();
        /// <summary>Every identifier outside comments and strings, with the 1-based lines where it appears.</summary>
        public Dictionary<string, List<int>> Identifiers = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        public List<ParseError> Errors = new List<ParseError>();

        public bool Uses(string identifier) => Identifiers.ContainsKey(identifier);

        public int FirstLine(string identifier) =>
            Identifiers.TryGetValue(identifier, out var lines) && lines.Count > 0 ? lines[0] : 0;

        public HlslFunction Function(string name) => Functions.FirstOrDefault(f => f.Name == name);

        public HlslPragma Pragma(string name) => Pragmas.FirstOrDefault(p => p.Name == name);
    }
}
