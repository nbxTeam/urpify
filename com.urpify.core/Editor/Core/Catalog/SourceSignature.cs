using System;
using System.Collections.Generic;
using System.Text;

namespace Urpify.Catalog
{
    /// <summary>
    /// Structure of a shader or C# source for the catalog (TZ 11.1), beyond the exact fingerprint:
    /// - <see cref="Structure"/>: a hash of the token stream in which the author's names are replaced by their order of
    ///   first appearance ($1, $2, ...), string literals are blanked, and comments, whitespace and property default values
    ///   do not count. A copy with renamed variables, properties, functions or shader name has the same structure hash.
    ///   Language keywords, HLSL types, intrinsics and semantics, ShaderLab keywords and Unity's own names (unity_*, UNITY_*,
    ///   Unity*, the UnityCG / camera API names below) keep their text: two sources that differ in them are different.
    /// - <see cref="MinHash"/>: 32 min-hashes of the 4-token shingles of that stream, to estimate how much of the code two
    ///   sources share (Jaccard similarity) when they are not the same: "looks like X".
    /// </summary>
    public sealed class SourceSignature
    {
        public string Structure;
        public uint[] MinHash;
        public int Tokens;
        /// <summary>The token stream the structure hash is made of (for diagnostics).</summary>
        public string Canonical;

        public const int HashCount = 32;
        const int Shingle = 4;

        public static SourceSignature OfShader(string text, string path = "") => Of(Tokenize(ShaderFingerprint.WithoutPropertyDefaults(text, path), shader: true));

        public static SourceSignature OfScript(string text) => Of(Tokenize(text ?? "", shader: false));

        /// <summary>Estimated share of shingles two sources have in common, 0..1.</summary>
        public static double Similarity(uint[] a, uint[] b)
        {
            if (a == null || b == null || a.Length != b.Length || a.Length == 0) return 0;
            int same = 0;
            for (int i = 0; i < a.Length; i++) if (a[i] == b[i]) same++;
            return (double)same / a.Length;
        }

        public static string Encode(uint[] hashes)
        {
            var bytes = new byte[hashes.Length * 4];
            Buffer.BlockCopy(hashes, 0, bytes, 0, bytes.Length);
            return Convert.ToBase64String(bytes);
        }

        public static uint[] Decode(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var bytes = Convert.FromBase64String(s);
            var h = new uint[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, h, 0, h.Length * 4);
            return h;
        }

        static SourceSignature Of(List<string> tokens)
        {
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            var canon = new List<string>(tokens.Count);
            // Shingles see every name of the author as "$": a rename (or a name that happens to equal an intrinsic, like
            // "lit") shifts the numbering of the structure but not the shingles.
            var shingled = new List<string>(tokens.Count);
            foreach (var t in tokens)
            {
                if (t.Length > 0 && (char.IsLetter(t[0]) || t[0] == '_') && !Kept(t))
                {
                    if (!names.TryGetValue(t, out var c)) names[t] = c = "$" + (names.Count + 1);
                    canon.Add(c);
                    shingled.Add("$");
                }
                else { canon.Add(t); shingled.Add(t); }
            }
            var sig = new SourceSignature { Tokens = canon.Count, MinHash = new uint[HashCount] };
            sig.Canonical = string.Join(" ", canon);
            sig.Structure = Hash(sig.Canonical);
            for (int i = 0; i < HashCount; i++) sig.MinHash[i] = uint.MaxValue;
            var sb = new StringBuilder();
            for (int s = 0; s + Shingle <= Math.Max(shingled.Count, Shingle); s++)
            {
                sb.Clear();
                for (int k = s; k < s + Shingle && k < shingled.Count; k++) sb.Append(shingled[k]).Append('\u0001');
                ulong h = Fnv(sb.ToString());
                for (int i = 0; i < HashCount; i++)
                {
                    uint v = Mix(h, (ulong)i);
                    if (v < sig.MinHash[i]) sig.MinHash[i] = v;
                }
                if (shingled.Count < Shingle) break;
            }
            return sig;
        }

        static ulong Fnv(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in s) { h ^= c; h *= 1099511628211UL; }
            return h;
        }

        /// <summary>One of the independent hash functions: the shingle hash mixed with the function's index (splitmix64).</summary>
        static uint Mix(ulong h, ulong i)
        {
            ulong z = h + (i + 1) * 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return (uint)((z ^ (z >> 31)) >> 16);
        }

        static string Hash(string s)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                var sb = new StringBuilder(32);
                for (int i = 0; i < 16; i++) sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Tokens: identifiers, numbers, one-character punctuation; strings blanked; comments and preprocessor spacing gone.</summary>
        static List<string> Tokenize(string s, bool shader)
        {
            var list = new List<string>();
            int i = 0, n = s.Length;
            while (i < n)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (c == '/' && i + 1 < n && s[i + 1] == '/') { while (i < n && s[i] != '\n') i++; continue; }
                if (c == '/' && i + 1 < n && s[i + 1] == '*') { int e = s.IndexOf("*/", i + 2, StringComparison.Ordinal); i = e < 0 ? n : e + 2; continue; }
                if (c == '"' || (!shader && c == '\''))
                {
                    char q = c; i++;
                    while (i < n && s[i] != q && s[i] != '\n') { if (s[i] == '\\') i++; i++; }
                    i++;
                    list.Add(q == '"' ? "\"\"" : "''");
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int st = i;
                    while (i < n && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                    list.Add(s.Substring(st, i - st));
                    continue;
                }
                if (char.IsDigit(c))
                {
                    int st = i;
                    while (i < n && (char.IsLetterOrDigit(s[i]) || s[i] == '.')) i++;
                    list.Add(s.Substring(st, i - st));
                    continue;
                }
                list.Add(c.ToString());
                i++;
            }
            return list;
        }

        static bool Kept(string t)
        {
            if (KeptNames.Contains(t)) return true;
            if (t.StartsWith("unity_", StringComparison.Ordinal) || t.StartsWith("UNITY_", StringComparison.Ordinal) || t.StartsWith("Unity", StringComparison.Ordinal)
                || t.StartsWith("SV_", StringComparison.Ordinal) || t.StartsWith("TEXCOORD", StringComparison.Ordinal) || t.StartsWith("COLOR", StringComparison.Ordinal)
                || t.StartsWith("SHADOW_", StringComparison.Ordinal) || t.StartsWith("TRANSFER_", StringComparison.Ordinal) || t.StartsWith("LIGHTING_", StringComparison.Ordinal)
                || t.StartsWith("SAMPLE_", StringComparison.Ordinal) || t.StartsWith("DECLARE_", StringComparison.Ordinal) || t.StartsWith("_Camera", StringComparison.Ordinal)
                || t.StartsWith("_WorldSpace", StringComparison.Ordinal) || t.StartsWith("_Object2World", StringComparison.Ordinal) || t.StartsWith("_World2Object", StringComparison.Ordinal))
                return true;
            // HLSL vector and matrix types: float3, half4x4, int2, ...
            foreach (var b in VectorBases)
                if (t.StartsWith(b, StringComparison.Ordinal) && t.Length > b.Length && char.IsDigit(t[b.Length])) return true;
            return false;
        }

        static readonly string[] VectorBases = { "float", "half", "fixed", "int", "uint", "bool", "double", "min16float", "min10float" };

        static readonly HashSet<string> KeptNames = new HashSet<string>(StringComparer.Ordinal)
        {
            // HLSL / Cg
            "float", "half", "fixed", "int", "uint", "bool", "double", "void", "struct", "return", "if", "else", "for", "while", "do", "break", "continue",
            "discard", "in", "out", "inout", "uniform", "static", "const", "true", "false", "sampler", "sampler2D", "sampler3D", "samplerCUBE",
            "sampler2D_float", "sampler2D_half", "Texture2D", "Texture3D", "TextureCube", "SamplerState", "cbuffer", "register", "typedef", "switch", "case",
            "default", "matrix", "vector", "linear", "centroid", "nointerpolation", "noperspective", "row_major", "column_major", "precise", "groupshared",
            "abs", "acos", "all", "any", "asin", "atan", "atan2", "ceil", "clamp", "clip", "cos", "cosh", "cross", "ddx", "ddy", "degrees", "determinant",
            "distance", "dot", "exp", "exp2", "faceforward", "floor", "fmod", "frac", "frexp", "fwidth", "isfinite", "isinf", "isnan", "ldexp", "length",
            "lerp", "lit", "log", "log10", "log2", "max", "min", "modf", "mul", "normalize", "pow", "radians", "reflect", "refract", "round", "rsqrt",
            "saturate", "sign", "sin", "sincos", "sinh", "smoothstep", "sqrt", "step", "tan", "tanh", "transpose", "trunc", "rcp", "mad",
            "tex1D", "tex2D", "tex2Dlod", "tex2Dproj", "tex2Dgrad", "tex2Dbias", "tex3D", "texCUBE", "texCUBElod", "texCUBEbias", "Sample", "SampleLevel",
            "SampleBias", "SampleGrad", "Load", "POSITION", "NORMAL", "TANGENT", "BINORMAL", "VFACE", "VPOS", "BLENDWEIGHTS", "BLENDINDICES", "PSIZE", "FOG", "DEPTH",
            "pragma", "include", "define", "ifdef", "ifndef", "endif", "elif", "undef", "error", "vertex", "fragment", "surface", "target", "multi_compile",
            "shader_feature", "multi_compile_fwdbase", "multi_compile_fwdadd", "multi_compile_fog", "multi_compile_instancing", "multi_compile_shadowcaster",
            "CGPROGRAM", "ENDCG", "CGINCLUDE", "HLSLPROGRAM", "ENDHLSL", "HLSLINCLUDE",
            // ShaderLab
            "Shader", "Properties", "SubShader", "Pass", "Tags", "LOD", "Fallback", "FallBack", "CustomEditor", "GrabPass", "UsePass", "Name", "Cull",
            "ZWrite", "ZTest", "Blend", "BlendOp", "ColorMask", "Offset", "Stencil", "Ref", "Comp", "ReadMask", "WriteMask", "Fail", "ZFail", "Off", "On",
            "Back", "Front", "One", "Zero", "SrcAlpha", "OneMinusSrcAlpha", "DstColor", "SrcColor", "DstAlpha", "OneMinusDstColor", "OneMinusSrcColor",
            "OneMinusDstAlpha", "Always", "Never", "Less", "Greater", "LEqual", "GEqual", "Equal", "NotEqual", "Keep", "Replace", "IncrSat", "DecrSat",
            "Invert", "IncrWrap", "DecrWrap", "Range", "Color", "Vector", "Float", "Int", "Integer", "Texture", "Cube", "White", "Black", "AlphaToMask",
            "Lighting", "Fog", "Mode", "Material", "Diffuse", "Ambient", "Emission", "Specular", "Shininess", "SetTexture", "Combine", "constant",
            "primary", "previous", "texture", "Queue", "RenderType", "LightMode", "IgnoreProjector", "DisableBatching", "ForceNoShadowCasting",
            "PreviewType", "CanUseSpriteAtlas", "RenderPipeline", "Opaque", "Transparent", "TransparentCutout", "Geometry", "AlphaTest", "Overlay",
            "Background", "ForwardBase", "ForwardAdd", "ShadowCaster", "Deferred", "Meta", "PrepassBase", "PrepassFinal", "HideInInspector",
            "NoScaleOffset", "Normal", "HDR", "Gamma", "PerRendererData", "Toggle", "Enum", "KeywordEnum", "PowerSlider", "IntRange", "Header", "Space",
            // Unity shader library names without the Unity/unity_/UNITY_ prefix
            "appdata_base", "appdata_tan", "appdata_full", "appdata_img", "v2f_img", "vert_img", "TRANSFORM_TEX", "UnpackNormal", "UnpackScaleNormal",
            "ComputeScreenPos", "ComputeGrabScreenPos", "LinearEyeDepth", "Linear01Depth", "COMPUTE_EYEDEPTH", "ShadeSH9", "Shade4PointLights",
            "ObjSpaceViewDir", "WorldSpaceViewDir", "ObjSpaceLightDir", "WorldSpaceLightDir", "DecodeHDR", "DecodeLightmap", "EncodeFloatRGBA",
            "DecodeFloatRGBA", "DecodeDepthNormal", "LinearToGammaSpace", "GammaToLinearSpace", "Luminance", "_LightColor0", "_Time", "_SinTime",
            "_CosTime", "_ProjectionParams", "_ScreenParams", "_ZBufferParams", "SurfaceOutput", "SurfaceOutputStandard",
            "SurfaceOutputStandardSpecular", "UnityGI", "UnityGIInput", "SHADOW_COORDS", "V2F_SHADOW_CASTER", "LIGHT_ATTENUATION",
            "fullforwardshadows", "addshadow", "alpha", "alphatest", "keepalpha", "noshadow", "Standard", "StandardSpecular", "Lambert", "BlinnPhong",
            // C#
            "using", "namespace", "class", "public", "private", "protected", "internal", "sealed", "abstract", "virtual", "override", "readonly", "new",
            "this", "base", "null", "var", "void", "string", "object", "foreach", "try", "catch", "finally", "throw", "typeof", "is", "as", "ref",
            "params", "get", "set", "enum", "interface", "delegate", "event", "partial", "long", "short", "byte", "char", "decimal",
            "UnityEngine", "System", "Collections", "Generic", "MonoBehaviour", "ScriptableObject", "GameObject", "Transform", "Component", "Camera",
            "Renderer", "MeshRenderer", "MeshFilter", "Mesh", "Material", "Shader", "RenderTexture", "Texture", "Texture2D", "Graphics", "GL", "Vector2",
            "Vector3", "Vector4", "Quaternion", "Matrix4x4", "Mathf", "Time", "Screen", "Debug", "Hashtable", "Dictionary", "List", "HideFlags",
            "Awake", "Start", "Update", "LateUpdate", "FixedUpdate", "OnEnable", "OnDisable", "OnDestroy", "OnValidate", "OnPreCull", "OnPreRender",
            "OnPostRender", "OnRenderImage", "OnRenderObject", "OnWillRenderObject", "OnBecameVisible", "OnBecameInvisible", "OnTriggerEnter",
            "OnTriggerExit", "Render", "RenderWithShader", "SetReplacementShader", "ResetReplacementShader", "AddCommandBuffer", "RemoveCommandBuffer",
            "Blit", "CommandBuffer", "CameraEvent", "DepthTextureMode", "depthTextureMode", "main", "current", "targetTexture", "worldToCameraMatrix",
            "projectionMatrix", "CalculateObliqueMatrix", "invertCulling", "SetGlobalTexture", "SetTexture", "SetFloat", "SetInt", "SetVector",
            "SetMatrix", "SetColor", "GetComponent", "AddComponent", "CopyFrom", "enabled", "transform", "position", "rotation", "forward", "up",
            "right", "localToWorldMatrix", "worldToLocalMatrix", "cullingMask", "clearFlags", "backgroundColor", "fieldOfView", "aspect",
            "nearClipPlane", "farClipPlane", "orthographic", "orthographicSize", "ExecuteInEditMode", "ExecuteAlways", "RequireComponent",
            "SerializeField", "Range", "Header", "Tooltip", "ImageEffectOpaque", "FindObjectsOfType", "FindObjectsByType", "DestroyImmediate", "Destroy",
            "Projector", "RenderingPath", "ShadowCastingMode", "Rendering", "LayerMask", "Plane", "Bounds", "GeometryUtility",
        };
    }
}
