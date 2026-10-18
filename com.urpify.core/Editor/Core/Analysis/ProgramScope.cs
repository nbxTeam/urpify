using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Urpify.Parsing;
using Urpify.Generation;

namespace Urpify.Analysis
{
    /// <summary>
    /// Everything the shader compiler sees for a program (architect decision 8 after M3): the program itself, the
    /// CGINCLUDE blocks in its scope, the project files it includes at any depth, and functions that a #define
    /// declares. The converters look up the author's functions and structs here. When a function is not in the project
    /// at all, <see cref="BuiltInOwner"/> tells which Built-in include of the Editor defines it.
    /// </summary>
    public sealed class ProgramScope
    {
        public sealed class Part
        {
            public HlslUnit Unit;
            public string Text = "";
            /// <summary>Full path of a project include file; "" for a block of the shader itself.</summary>
            public string File = "";
            /// <summary>The include as the shader or another include wrote it ("" for blocks).</summary>
            public string Written = "";
            /// <summary>Function found by expanding a #define (its text is the expansion, not the source).</summary>
            public bool FromMacro;
            /// <summary>
            /// The preprocessor condition under which the file is included ("" = always), from the #if directives around
            /// its #include lines (<see cref="HlslGuards"/>); a file included under several conditions gets them or-ed.
            /// </summary>
            public string Guard = "";
        }

        /// <summary>Upper bound on project include files followed (a guard against generated include trees).</summary>
        public const int MaxFiles = 256;

        readonly List<Part> parts = new List<Part>();
        readonly List<string> builtIns = new List<string>();

        public IReadOnlyList<Part> Parts => parts;
        public IEnumerable<Part> Files => parts.Where(p => p.File.Length > 0 && !p.FromMacro);
        /// <summary>Built-in include names (UnityCG.cginc, TerrainEngine.cginc...) written anywhere in the scope.</summary>
        public IReadOnlyList<string> BuiltInIncludes => builtIns;

        /// <param name="library">
        /// For a Built-in include Urpify's shader library replaces in place (Pro): the full path of the replacement, read as
        /// part of the scope, or "" when the replacement is not there; null for any other include.
        /// </param>
        public static ProgramScope Of(ShaderDocument doc, IEnumerable<ProgramBlock> blocks, Func<string, string> library = null)
        {
            var scope = new ProgramScope();
            var src = doc.Source.Text;
            var list = blocks.Where(b => b != null).Distinct().ToList();
            foreach (var b in list) scope.parts.Add(new Part { Unit = b.Unit, Text = src });
            var queue = new Queue<(string written, string from, string guard)>();
            foreach (var b in list) foreach (var i in b.Unit.Includes) queue.Enqueue((i.Path, doc.Source.Path ?? "", HlslGuards.At(b.Unit, i.Line)));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var byFile = new Dictionary<string, Part>(StringComparer.OrdinalIgnoreCase);
            while (queue.Count > 0 && seen.Count < MaxFiles)
            {
                var (written, from, guard) = queue.Dequeue();
                var lib = library?.Invoke(written);
                if (lib != null)
                {
                    // Urpify's replacement declares what the Built-in file did (Input, SplatmapVert ...): part of the scope.
                    if (lib.Length > 0 && seen.Add(lib))
                    {
                        var libText = File.ReadAllText(lib);
                        scope.parts.Add(new Part { Unit = HlslScanner.Scan(new SourceText(libText, lib)), Text = libText, File = lib, Written = written });
                    }
                    continue;
                }
                if (IsBuiltInInclude(written))
                {
                    if (!scope.builtIns.Contains(written, StringComparer.OrdinalIgnoreCase)) scope.builtIns.Add(written);
                    continue;
                }
                var full = ResolveProjectInclude(written, from);
                if (full != null && byFile.TryGetValue(full, out var again))
                {
                    // Included once more, maybe under another condition: it is there when either holds.
                    if (again.Guard.Length > 0) again.Guard = guard.Length == 0 ? "" : "(" + again.Guard + ") || (" + guard + ")";
                    continue;
                }
                if (full == null || !seen.Add(full) || !File.Exists(full)) continue;
                string text;
                try { text = File.ReadAllText(full); } catch { continue; }
                var unit = HlslScanner.Scan(new SourceText(text, full));
                var part = new Part { Unit = unit, Text = text, File = full, Written = written, Guard = guard };
                scope.parts.Add(part);
                byFile[full] = part;
                foreach (var i in unit.Includes) queue.Enqueue((i.Path, full, HlslGuards.And(guard, HlslGuards.At(unit, i.Line))));
            }
            return scope;
        }

        /// <summary>Built-in includes are written by bare file name; anything with a folder is a project file.</summary>
        public static bool IsBuiltInInclude(string written)
        {
            var p = (written ?? "").Trim();
            return p.IndexOf('/') < 0 && p.IndexOf('\\') < 0 && IncludeResolver.BuiltInIncludes.Contains(p);
        }

        /// <summary>Full path of a project include as the compiler resolves it: project-root paths, then relative to the includer.</summary>
        public static string ResolveProjectInclude(string written, string from)
        {
            try
            {
                var w = written.Replace('\\', '/');
                return Path.GetFullPath(w.StartsWith("Assets/", StringComparison.Ordinal) || w.StartsWith("Packages/", StringComparison.Ordinal)
                    ? w : Path.Combine(Path.GetDirectoryName(from) ?? "", w));
            }
            catch { return null; }
        }

        /// <summary>Declared in a project include file (kept as an #include, so a converter must not declare it again).</summary>
        /// Urpify's own library files (BirpCompat*.hlsl, included by path from a pass of a shader converted earlier) are not the
        /// author's declarations.
        public bool DeclaredInFiles(string name) => Files.Where(p => !IsKitLibrary(p.File) && !IsKitLibrary(p.Written)).Any(p => HlslDeclarations.Variables(p.Unit).Any(v => v.Names.Contains(name)));

        static bool IsKitLibrary(string path)
        {
            var file = Path.GetFileName(path ?? "");
            return file.StartsWith("BirpCompat", StringComparison.OrdinalIgnoreCase) && file.EndsWith(".hlsl", StringComparison.OrdinalIgnoreCase);
        }

        public IEnumerable<HlslFunction> Functions => parts.SelectMany(p => p.Unit.Functions);

        public (HlslFunction fn, string text) Function(string name)
        {
            var (fn, part) = FunctionPart(name);
            return (fn, part?.Text);
        }

        /// <summary>The function and where it is; a function a #define declares is expanded (and remembered) on first use.</summary>
        public (HlslFunction fn, Part part) FunctionPart(string name)
        {
            if (string.IsNullOrEmpty(name)) return (null, null);
            foreach (var p in parts) { var f = p.Unit.Function(name); if (f != null) return (f, p); }
            var expanded = FromMacros(name);
            if (expanded != null) { parts.Add(expanded); return (expanded.Unit.Function(name), expanded); }
            return (null, null);
        }

        public (HlslStruct st, string text) Struct(string name)
        {
            foreach (var p in parts) { var s = p.Unit.Structs.FirstOrDefault(x => x.Name == name); if (s != null) return (s, p.Text); }
            return (null, null);
        }

        // ------------------------------------------------------------------------------------------ #define

        /// <summary>
        /// A function declared by a macro: "#define DECLARE_VERT(n) v2f n(appdata v) {...}" used as "DECLARE_VERT(vert)",
        /// or an object-like macro whose body is a whole function. Expanded one level (the macro's parameters, # and ##).
        /// </summary>
        Part FromMacros(string name)
        {
            var macros = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in parts.Where(p => !p.FromMacro))
                foreach (var d in p.Unit.Defines) if (!macros.ContainsKey(d.Key)) macros[d.Key] = d.Value;
            foreach (var m in macros)
            {
                if (m.Value.IndexOf('{') < 0 || m.Value.IndexOf(name, StringComparison.Ordinal) < 0 && !m.Value.StartsWith("(", StringComparison.Ordinal)) continue;
                var (parameters, body) = SplitMacro(m.Value);
                foreach (var p in parts.Where(p => !p.FromMacro))
                    foreach (var args in Invocations(p.Text, m.Key, parameters != null))
                    {
                        var text = parameters == null ? body : Substitute(body, parameters, args);
                        if (text.IndexOf(name, StringComparison.Ordinal) < 0) continue;
                        var unit = HlslScanner.Scan(new SourceText(text, p.File));
                        if (unit.Function(name) != null) return new Part { Unit = unit, Text = text, File = p.File, FromMacro = true };
                    }
            }
            return null;
        }

        static (List<string> parameters, string body) SplitMacro(string value)
        {
            if (!value.StartsWith("(", StringComparison.Ordinal)) return (null, value);
            int close = value.IndexOf(')');
            if (close < 0) return (null, value);
            var ps = value.Substring(1, close - 1).Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            return (ps, value.Substring(close + 1).Trim());
        }

        /// <summary>Arguments of each use of a macro outside #define lines (an empty list for object-like macros).</summary>
        static IEnumerable<List<string>> Invocations(string text, string macro, bool functionLike)
        {
            foreach (Match m in Regex.Matches(text, @"\b" + Regex.Escape(macro) + @"\b"))
            {
                var lineStart = text.LastIndexOf('\n', Math.Max(0, m.Index - 1)) + 1;
                if (Regex.IsMatch(text.Substring(lineStart, m.Index - lineStart), @"^\s*#")) continue;
                if (!functionLike) { yield return new List<string>(); continue; }
                int i = m.Index + m.Length;
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                if (i >= text.Length || text[i] != '(') continue;
                var args = new List<string>();
                int depth = 0, start = i + 1;
                for (int k = i; k < text.Length; k++)
                {
                    char c = text[k];
                    if (c == '(') depth++;
                    else if (c == ')') { depth--; if (depth == 0) { args.Add(text.Substring(start, k - start).Trim()); break; } }
                    else if (c == ',' && depth == 1) { args.Add(text.Substring(start, k - start).Trim()); start = k + 1; }
                }
                yield return args;
            }
        }

        static string Substitute(string body, List<string> parameters, List<string> args)
        {
            var sb = new StringBuilder();
            bool pasting = false;
            foreach (Match t in Regex.Matches(body, @"##|#?[A-Za-z_][A-Za-z0-9_]*|[\s\S]"))
            {
                var tok = t.Value;
                if (tok == "##") { while (sb.Length > 0 && char.IsWhiteSpace(sb[sb.Length - 1])) sb.Length--; pasting = true; continue; }
                if (pasting && tok.Length == 1 && char.IsWhiteSpace(tok[0])) continue;
                pasting = false;
                var stringize = tok.StartsWith("#", StringComparison.Ordinal) && tok.Length > 1;
                var id = stringize ? tok.Substring(1) : tok;
                int k = parameters.IndexOf(id);
                if (k >= 0 && k < args.Count) sb.Append(stringize ? "\"" + args[k] + "\"" : args[k]);
                else sb.Append(tok);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------------------------------ Built-in owner

        static readonly Dictionary<string, HlslUnit> builtInCache = new Dictionary<string, HlslUnit>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Folder of the Editor's Built-in includes (Editor/Data/CGIncludes), or null outside the Editor.</summary>
        public static string BuiltInFolder
        {
            get
            {
                // Editor/Data/CGIncludes in 2022.3, Editor/Data/Resources/CGIncludes in 6000.x (paths in the compilers' messages).
                try
                {
                    var data = UnityEditor.EditorApplication.applicationContentsPath;
                    foreach (var f in new[] { Path.Combine(data, "CGIncludes"), Path.Combine(data, "Resources", "CGIncludes") })
                        if (Directory.Exists(f)) return f;
                    return null;
                }
                catch { return null; }
            }
        }

        /// <summary>The Built-in include (followed through its own includes) that defines <paramref name="function"/>, or null.</summary>
        public string BuiltInOwner(string function)
        {
            var folder = BuiltInFolder;
            if (folder == null || !Directory.Exists(folder)) return null;
            var queue = new Queue<string>(builtIns);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (queue.Count > 0)
            {
                var name = queue.Dequeue();
                if (!seen.Add(name)) continue;
                var unit = BuiltInUnit(folder, name);
                if (unit == null) continue;
                if (unit.Function(function) != null) return name;
                foreach (var i in unit.Includes) queue.Enqueue(i.Path);
            }
            return null;
        }

        static HlslUnit BuiltInUnit(string folder, string name)
        {
            lock (builtInCache)
            {
                if (builtInCache.TryGetValue(name, out var u)) return u;
                var path = Path.Combine(folder, name);
                u = File.Exists(path) ? HlslScanner.Scan(new SourceText(File.ReadAllText(path), path)) : null;
                builtInCache[name] = u;
                return u;
            }
        }

        /// <summary>
        /// Why a function the program names is not converted: in a Built-in include (with the file named), or nowhere the
        /// compiler would look.
        /// </summary>
        public string NotFound(string role, string name)
        {
            var owner = BuiltInOwner(name);
            if (owner != null)
                return $"The {role} function '{name}' is Unity's own, in the Built-in file {owner}; Urpify has no URP equivalent for that file yet.";
            return $"The {role} function '{name}' was not found in the program, its CGINCLUDE, the project files it includes or a #define.";
        }
    }
}
