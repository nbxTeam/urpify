using System;
using System.Collections.Generic;
using System.Linq;
using Urpify.Generation;
using Urpify.Parsing;

namespace Urpify.Pipeline.AfterUnity
{
    /// <summary>
    /// Shader.Find with a Built-in shader name in a run-time script (TZ 6.11, lab C04), a fix after Unity's converter: the
    /// call gets the URP shader Unity's converter picks for that shader, and the property names the same method sets
    /// (_MainTex, _Color, _Glossiness...) get URP's (URP keeps the Built-in names only as hidden properties). With Output =
    /// Keep Built-in support each name is chosen when the script runs, by the active render pipeline, so the script keeps
    /// working in Built-in. Runs before the Pro script converters, which then get the rewritten text.
    /// </summary>
    public sealed class ShaderFindFix : IScriptFileConverter
    {
        public string Name => "Shader.Find";
        public int Order => 50;

        static readonly HashSet<string> PropertyCalls = new HashSet<string>(StringComparer.Ordinal)
        {
            "SetColor", "SetTexture", "SetFloat", "SetInt", "SetInteger", "SetVector", "SetTextureOffset", "SetTextureScale",
            "GetColor", "GetTexture", "GetFloat", "GetInt", "GetInteger", "GetVector", "GetTextureOffset", "GetTextureScale", "HasProperty", "PropertyToID",
        };

        /// <summary>Editor scripts never run in the player; Urpify's own runtime folder is not converted again.</summary>
        public static bool IsRuntimeScript(string path)
        {
            var p = (path ?? "").Replace('\\', '/');
            return p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && p.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) < 0
                   && !p.StartsWith("Assets/Urpify/", StringComparison.Ordinal) && !KitPaths.IsKitFile(p);
        }

        public bool Handles(string text, string path) => IsRuntimeScript(path) && text.Contains("Shader.Find");

        public IEnumerable<KeyValuePair<string, string>> RuntimeFiles() => Enumerable.Empty<KeyValuePair<string, string>>();
        public IEnumerable<KeyValuePair<string, string>> RendererFeatures() => Enumerable.Empty<KeyValuePair<string, string>>();
        public List<string> ConfigureRendererFeature(UnityEngine.Object feature, ICollection<string> requirements) => new List<string>();
        public bool NeedsRuntime(ICollection<string> requirements) => false;

        /// <summary>The run-time test a script converted with Keep Built-in support asks.</summary>
        const string UrpActive = "UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null";

        public ShaderConversion Convert(string text, string path)
        {
            var result = new ShaderConversion();
            if (!IsRuntimeScript(path)) return result;
            var code = CSharpFile.Parse(text, path);
            if (code.Errors.Count > 0) { result.Add(NoteKind.NotSupported, 0, "Stopped: the script could not be read: " + code.Errors[0].Message); return result; }
            var edits = new List<(int start, int end, string text)>();
            foreach (var i in code.FindAll("Shader", ".", "Find", "("))
            {
                if (i + 4 >= code.Tokens.Count || code.Tokens[i + 4].Kind != CSharpFile.TokKind.String) continue;
                var tok = code.Tokens[i + 4];
                var builtIn = CSharpFile.StringValue(tok);
                if (!BuiltInShaderTable.ForScripts.TryGetValue(builtIn, out var urp))
                {
                    if (BuiltInShaderTable.IsBuiltInName(builtIn)) result.Add(NoteKind.NotSupported, tok.Line, $"Shader.Find(\"{builtIn}\"): Urpify knows no URP shader for it; pick one and rename the properties the code sets.");
                    continue;
                }
                var renames = new Dictionary<string, string>(BuiltInShaderTable.BaseRenames);
                foreach (var r in urp.Renames) renames[r.Key] = r.Value;
                // Keep Built-in support: the script runs in both pipelines, so the choice is made when it runs.
                bool keep = ConversionOptions.KeepBuiltIn;
                string Pick(string urpValue, string builtInValue) =>
                    keep ? $"({UrpActive} ? \"{urpValue}\" : \"{builtInValue}\")" : "\"" + urpValue + "\"";
                edits.Add((tok.Start, tok.End, Pick(urp.Urp, builtIn)));
                result.Add(urp.Note != null ? NoteKind.Approx : NoteKind.Info, tok.Line, (urp.Note ?? $"Shader.Find(\"{builtIn}\") -> \"{urp.Urp}\".")
                    + (keep ? " Under Built-in the script still asks for the Built-in shader (Output: Keep Built-in support)." : ""));
                var method = code.MethodAt(i);
                if (method == null) continue;
                for (int k = method.BodyStart; k + 2 < method.BodyEnd; k++)
                {
                    var t = code.Tokens[k];
                    if (t.Kind != CSharpFile.TokKind.Ident || !PropertyCalls.Contains(t.Text) || !code.Tokens[k + 1].Is("(")) continue;
                    var arg = code.Tokens[k + 2];
                    if (arg.Kind != CSharpFile.TokKind.String) continue;
                    var v = CSharpFile.StringValue(arg);
                    if (v == "_Mode") { result.Add(NoteKind.NotSupported, arg.Line, "The code sets Standard's _Mode (rendering mode); URP Lit uses _Surface and _Blend with their keywords. Set them by hand."); continue; }
                    if (!renames.TryGetValue(v, out var nv)) continue;
                    edits.Add((arg.Start, arg.End, Pick(nv, v)));
                }
                result.Add(NoteKind.Info, tok.Line, $"In {method.Name}: {string.Join(", ", renames.Select(r => r.Key + " -> " + r.Value))} in the property calls.");
            }
            if (edits.Count == 0) return result;
            var sb = new System.Text.StringBuilder(text);
            foreach (var e in edits.OrderByDescending(e => e.start).ThenByDescending(e => e.end))
                sb.Remove(e.start, e.end - e.start).Insert(e.start, e.text);
            result.Output = sb.ToString();
            result.Converted = true;
            return result;
        }
    }
}
