using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Urpify.Parsing;

namespace Urpify.Analysis
{
    /// <summary>What an image effect's OnRenderImage reaches in the other scripts of the project.</summary>
    public sealed class EffectReach
    {
        /// <summary>
        /// Why the effect cannot run inside URP's frame, or "": a call in another script that runs at once and cannot be
        /// recorded (a camera render, a compute dispatch, immediate drawing Urpify does not record), or a shader of the effect
        /// that reads Built-in's G-buffer. Such an effect is left as it is (Not supported yet): under URP it is missing and
        /// the rest of the frame is drawn, as without Urpify Pro.
        /// </summary>
        public string Blocker = "";
        /// <summary>True when <see cref="Blocker"/> is a G-buffer read (another rule and text), false for an unrecordable call.</summary>
        public bool GBuffer;
        /// <summary>Other scripts whose drawing calls (Graphics.Blit and the rest Urpify records) the effect reaches.</summary>
        public List<string> Helpers = new List<string>();
    }

    /// <summary>
    /// Follows OnRenderImage into the other scripts it calls (QA-030, QA-025, QA-029). Under URP Urpify runs OnRenderImage while
    /// URP records the frame, and records the Graphics.Blit calls it rewrote. A Blit written in another class (image-ultra:
    /// a ScriptableObject effect behind the camera script; Terraforming: EffectManager, CloudManager, FXAAEffect) was not
    /// rewritten, ran at once on textures the frame had not filled yet, and every frame came out black or white, while
    /// without Pro the game was drawn. Shared by the scanner and Urpify converters so both say the same.
    /// Reach: from OnRenderImage, the methods it calls in its own file, static calls on project types, and instance calls of
    /// a method name declared by a project type the calling file names (with the types derived from it: an abstract
    /// effect's Render runs its subclasses' Render). An over-approximation: a reached file is only rewritten (outside an
    /// effect the rewritten calls behave as before) or, when it holds a call that cannot be recorded, keeps the effect as it is.
    /// </summary>
    public static class ImageEffectReach
    {
        static readonly Regex ShaderHeader = new Regex(@"(?m)^\s*Shader\s*""([^""]+)""", RegexOptions.Compiled);
        static readonly Regex GBufferRead = new Regex(@"\b_CameraGBufferTexture[0-3]\b", RegexOptions.Compiled);
        static readonly HashSet<string> TypeKeywords = new HashSet<string>(StringComparer.Ordinal) { "class", "struct", "interface" };

        /// <summary>The reach of the scripts being converted now (set by the caller around a conversion run), or null.</summary>
        public static Dictionary<string, EffectReach> Current { get; private set; }
        /// <summary>For <see cref="Current"/>: helper script path -> file names of the effects reaching it.</summary>
        static Dictionary<string, string> s_CurrentHelpers;

        public static void Use(Dictionary<string, EffectReach> reach)
        {
            Current = reach;
            s_CurrentHelpers = reach == null ? null : Helpers(reach);
        }

        /// <summary>The effects (file names) whose OnRenderImage reaches this script's drawing calls in the current run, or null.</summary>
        public static string HelperOf(string path) =>
            s_CurrentHelpers != null && path != null && s_CurrentHelpers.TryGetValue(path, out var effects) ? effects : null;

        /// <summary>The reach of an effect script in the current run, or null.</summary>
        public static EffectReach Of(string path) => Current != null && path != null && Current.TryGetValue(path, out var r) ? r : null;

        /// <summary>Helper path -> the effects reaching it, for <see cref="CurrentHelpers"/>; effects with a blocker are not run, so they reach nothing.</summary>
        public static Dictionary<string, string> Helpers(Dictionary<string, EffectReach> reach)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in reach.Where(r => r.Value.Blocker.Length == 0).OrderBy(r => r.Key, StringComparer.Ordinal))
                foreach (var h in kv.Value.Helpers)
                    d[h] = d.TryGetValue(h, out var s) ? s + ", " + Path.GetFileName(kv.Key) : Path.GetFileName(kv.Key);
            return d;
        }

        sealed class Script
        {
            public string Path;
            public CSharpFile Code;
            public readonly HashSet<string> Types = new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, List<CSharpFile.Method>> Methods = new Dictionary<string, List<CSharpFile.Method>>(StringComparer.Ordinal);
            public HashSet<string> Named;
        }

        /// <summary>For each script with an OnRenderImage body: what it reaches. Scripts without one are not in the result.</summary>
        /// <param name="shaders">The project's shaders (path, text), for the G-buffer test; may be empty.</param>
        public static Dictionary<string, EffectReach> Find(IEnumerable<(string path, string text)> scripts, IEnumerable<(string path, string text)> shaders)
        {
            var result = new Dictionary<string, EffectReach>(StringComparer.Ordinal);
            var all = new List<Script>();
            foreach (var (path, text) in scripts)
            {
                if (text == null || !IsRuntime(path)) continue;
                var code = CSharpFile.Parse(text, path);
                if (code.Errors.Count > 0) continue;
                var s = new Script { Path = path, Code = code };
                foreach (var m in code.Methods.Where(m => m.BodyStart >= 0))
                {
                    if (!s.Methods.TryGetValue(m.Name, out var list)) s.Methods[m.Name] = list = new List<CSharpFile.Method>();
                    list.Add(m);
                }
                all.Add(s);
            }
            var effects = all.Where(s => s.Methods.ContainsKey("OnRenderImage")).ToList();
            if (effects.Count == 0) return result;

            // Project types: name -> scripts declaring it, and base -> derived.
            var declared = new Dictionary<string, List<Script>>(StringComparer.Ordinal);
            var derived = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var bases = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var s in all)
            {
                var t = s.Code.Tokens;
                for (int k = 0; k + 1 < t.Count; k++)
                {
                    if (t[k].Kind != CSharpFile.TokKind.Ident || !TypeKeywords.Contains(t[k].Text) || t[k + 1].Kind != CSharpFile.TokKind.Ident) continue;
                    var name = t[k + 1].Text;
                    s.Types.Add(name);
                    if (!declared.TryGetValue(name, out var list)) declared[name] = list = new List<Script>();
                    if (!list.Contains(s)) list.Add(s);
                    // class Name<T> : Base, IOther { -- the last identifier of each base name.
                    int j = k + 2, depth = 0;
                    for (; j < t.Count && !t[j].Is("{") && !t[j].Is(";"); j++)
                    {
                        if (t[j].Is("<")) depth++;
                        else if (t[j].Is(">")) depth--;
                        else if (t[j].Is(":") && depth == 0) break;
                    }
                    if (j >= t.Count || !t[j].Is(":")) continue;
                    for (j++; j < t.Count && !t[j].Is("{") && t[j].Text != "where"; j++)
                    {
                        if (t[j].Kind != CSharpFile.TokKind.Ident || (j + 1 < t.Count && t[j + 1].Is("."))) continue;
                        if (!derived.TryGetValue(t[j].Text, out var d)) derived[t[j].Text] = d = new List<string>();
                        d.Add(name);
                        if (!bases.TryGetValue(name, out var b)) bases[name] = b = new List<string>();
                        b.Add(t[j].Text);
                    }
                }
            }
            // The G-buffer readers among the shaders: shader name and Resources path -> the texture it reads.
            var gbuffer = new Dictionary<string, (string shader, string texture)>(StringComparer.Ordinal);
            foreach (var (path, text) in shaders ?? Enumerable.Empty<(string, string)>())
            {
                if (text == null) continue;
                var g = GBufferRead.Match(text);
                var h = ShaderHeader.Match(text);
                if (!g.Success || !h.Success) continue;
                gbuffer[h.Groups[1].Value] = (h.Groups[1].Value, g.Value);
                var p = path.Replace('\\', '/');
                int r = p.LastIndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
                if (r >= 0) gbuffer[System.IO.Path.ChangeExtension(p.Substring(r + "/Resources/".Length), null)] = (h.Groups[1].Value, g.Value);
            }

            foreach (var effect in effects)
            {
                var reach = new EffectReach();
                result[effect.Path] = reach;
                var seen = new HashSet<CSharpFile.Method>();
                var files = new HashSet<Script> { effect };
                var queue = new Queue<(Script s, CSharpFile.Method m)>();
                foreach (var m in effect.Methods["OnRenderImage"]) queue.Enqueue((effect, m));
                while (queue.Count > 0)
                {
                    var (s, m) = queue.Dequeue();
                    if (!seen.Add(m)) continue;
                    files.Add(s);
                    var code = s.Code;
                    if (s != effect)
                    {
                        if (reach.Blocker.Length == 0)
                        {
                            var b = Unrecordable(code, m);
                            if (b != null) reach.Blocker = $"{b} in {System.IO.Path.GetFileName(s.Path)}, line {LineOf(code, m, b)}";
                        }
                        if (Draws(code, m) && !reach.Helpers.Contains(s.Path)) reach.Helpers.Add(s.Path);
                    }
                    // The types this file names: instance calls go to their methods (and their subclasses').
                    var named = s.Named ?? (s.Named = Named(s, declared));
                    var t = code.Tokens;
                    for (int k = m.BodyStart; k + 1 < m.BodyEnd; k++)
                    {
                        if (t[k].Kind != CSharpFile.TokKind.Ident || !t[k + 1].Is("(")) continue;
                        var name = t[k].Text;
                        bool member = k > 0 && t[k - 1].Is(".");
                        if (!member || (k > 1 && (t[k - 2].Text == "this" || t[k - 2].Text == "base")))
                        {
                            if (s.Methods.TryGetValue(name, out var own)) foreach (var x in own) queue.Enqueue((s, x));
                            // A method of a base class in another file.
                            foreach (var type in s.Types) foreach (var other in BaseScripts(type, s, declared, bases)) Enqueue(other, name, queue);
                            continue;
                        }
                        var receiver = t[k - 2];
                        if (receiver.Kind == CSharpFile.TokKind.Ident && declared.TryGetValue(receiver.Text, out var statics))
                        {
                            foreach (var other in statics) Enqueue(other, name, queue);
                            continue;
                        }
                        foreach (var type in named)
                            foreach (var other in WithDerived(type, declared, derived)) Enqueue(other, name, queue);
                    }
                }
                if (reach.Blocker.Length == 0)
                    foreach (var s in files)
                    {
                        foreach (var tok in s.Code.Tokens.Where(x => x.Kind == CSharpFile.TokKind.String))
                        {
                            var v = CSharpFile.StringValue(tok);
                            if (v.Length == 0 || !gbuffer.TryGetValue(v, out var hit)) continue;
                            reach.Blocker = $"{hit.shader} reads {hit.texture}";
                            reach.GBuffer = true;
                            break;
                        }
                        if (reach.Blocker.Length > 0) break;
                    }
                reach.Helpers.Sort(StringComparer.Ordinal);
            }
            return result;
        }

        static void Enqueue(Script s, string method, Queue<(Script, CSharpFile.Method)> queue)
        {
            if (s.Methods.TryGetValue(method, out var list)) foreach (var m in list) queue.Enqueue((s, m));
        }

        /// <summary>Project types named anywhere in the script (fields, parameters, generics), its own included.</summary>
        static HashSet<string> Named(Script s, Dictionary<string, List<Script>> declared)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tok in s.Code.Tokens)
                if (tok.Kind == CSharpFile.TokKind.Ident && declared.ContainsKey(tok.Text)) set.Add(tok.Text);
            return set;
        }

        static IEnumerable<Script> WithDerived(string type, Dictionary<string, List<Script>> declared, Dictionary<string, List<string>> derived)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>();
            stack.Push(type);
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (!seen.Add(t)) continue;
                if (declared.TryGetValue(t, out var scripts)) foreach (var s in scripts) yield return s;
                if (derived.TryGetValue(t, out var children)) foreach (var c in children) stack.Push(c);
            }
        }

        /// <summary>Scripts declaring the base types of <paramref name="type"/>, at any depth, other than <paramref name="self"/>.</summary>
        static IEnumerable<Script> BaseScripts(string type, Script self, Dictionary<string, List<Script>> declared, Dictionary<string, List<string>> bases)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { type };
            var stack = new Stack<string>(bases.TryGetValue(type, out var first) ? first : new List<string>());
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (!seen.Add(t)) continue;
                if (declared.TryGetValue(t, out var scripts)) foreach (var s in scripts) if (s != self) yield return s;
                if (bases.TryGetValue(t, out var more)) foreach (var b in more) stack.Push(b);
            }
        }

        /// <summary>A call in the method that runs at once and that Urpify cannot record into URP's frame, or null.</summary>
        static string Unrecordable(CSharpFile code, CSharpFile.Method m)
        {
            var t = code.Tokens;
            var cameras = CameraScriptFacts.CameraRenderCalls(code);
            foreach (var i in cameras) if (i > m.BodyStart && i < m.BodyEnd) return "Camera.Render";
            for (int k = m.BodyStart; k + 1 < m.BodyEnd; k++)
            {
                if (t[k].Kind != CSharpFile.TokKind.Ident || !t[k + 1].Is("(") || k == 0 || !t[k - 1].Is(".")) continue;
                switch (t[k].Text)
                {
                    case "RenderWithShader": return "Camera.RenderWithShader";
                    case "RenderToCubemap": return "Camera.RenderToCubemap";
                    case "Dispatch": case "DispatchIndirect": return "ComputeShader." + t[k].Text;
                    case "ReadPixels": return "Texture2D.ReadPixels";
                    case "RequestIntoNativeArray": case "Request": if (k > 1 && t[k - 2].Text == "AsyncGPUReadback") return "AsyncGPUReadback." + t[k].Text; break;
                }
            }
            for (int k = m.BodyStart; k + 2 < m.BodyEnd; k++)
            {
                if (t[k].Kind != CSharpFile.TokKind.Ident || !t[k + 1].Is(".")) continue;
                var member = t[k + 2].Text;
                if (t[k].Text == "GL" && !CameraScriptFacts.RecordedGL.Contains(member)) return "GL." + member;
                if (t[k].Text == "Graphics" && (member == "DrawTexture" || member == "CopyTexture" || member == "ConvertTexture" || member == "DrawMesh"
                                                || member == "DrawMeshInstanced" || member == "DrawProcedural" || member == "RenderMesh")) return "Graphics." + member;
            }
            return null;
        }

        static int LineOf(CSharpFile code, CSharpFile.Method m, string what)
        {
            var name = what.Substring(what.LastIndexOf('.') + 1);
            for (int k = m.BodyStart; k < m.BodyEnd; k++) if (code.Tokens[k].Text == name) return code.Tokens[k].Line;
            return m.Line;
        }

        /// <summary>Drawing Urpify records in the method's body: Graphics.Blit and the calls the converter rewrites with it.</summary>
        static bool Draws(CSharpFile code, CSharpFile.Method m)
        {
            var t = code.Tokens;
            for (int k = m.BodyStart; k + 2 < m.BodyEnd; k++)
            {
                if (t[k].Kind != CSharpFile.TokKind.Ident) continue;
                if (t[k + 1].Is(".") && t[k].Text == "Graphics" && (t[k + 2].Text == "Blit" || t[k + 2].Text == "ExecuteCommandBuffer"
                    || CameraScriptFacts.RecordedGraphics.Contains(t[k + 2].Text))) return true;
                if (t[k + 1].Is(".") && t[k].Text == "GL" && CameraScriptFacts.RecordedGL.Contains(t[k + 2].Text) && t[k + 2].Text != "GetGPUProjectionMatrix") return true;
                if (t[k + 1].Is(".") && t[k].Text == "RenderTexture" && t[k + 2].Text == "active") return true;
                if (t[k].Text == "SetPass" && k > 0 && t[k - 1].Is(".") && t[k + 1].Is("(")) return true;
            }
            return false;
        }

        /// <summary>Editor scripts never run in the player; Urpify's own runtime folder is not converted again.</summary>
        static bool IsRuntime(string path)
        {
            var p = (path ?? "").Replace('\\', '/');
            return p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && p.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) < 0
                   && !p.StartsWith("Assets/Urpify/", StringComparison.Ordinal);
        }
    }
}
