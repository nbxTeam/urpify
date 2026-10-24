using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Compilation;

namespace Urpify.Pipeline
{
    /// <summary>
    /// Compiles C# sources that are not part of the project (the scripts of an exported package, which live in the scope's
    /// Scripts~ folder) against the project's assemblies, without a domain reload: UnityEditor.Compilation.AssemblyBuilder,
    /// waited for synchronously as Unity's own documentation does. The package export uses it so a converted script that
    /// would not compile never reaches a buyer (QA-010).
    /// </summary>
    public static class CSharpCheck
    {
        public sealed class Error
        {
            public string File = "";
            public int Line;
            public string Message = "";
            public override string ToString() => $"{File}:{Line}: {Message}";
        }

        /// <summary>Errors of compiling <paramref name="sources"/> (file name -> text) together; empty when they compile.</summary>
        public static List<Error> Compile(IDictionary<string, string> sources, int timeoutSeconds = 300, string[] defines = null)
        {
            var dir = Path.GetFullPath(Path.Combine("Temp", "Urpify", "check-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            Directory.CreateDirectory(dir);
            var errors = new List<Error>();
            try
            {
                var files = new List<string>();
                var back = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                int n = 0;
                foreach (var kv in sources)
                {
                    // Short unique names: two scripts of a package may share a file name in different folders.
                    var path = Path.Combine(dir, (n++).ToString("0000") + "_" + Path.GetFileName(kv.Key));
                    File.WriteAllText(path, kv.Value);
                    files.Add(path);
                    back[Path.GetFullPath(path)] = kv.Key;
                }
                // Engine modules, not the UnityEngine.dll facade: URP's assemblies name UnityEngine.CoreModule types.
                // AssemblyBuilder is marked obsolete in Unity 6.6 with no replacement in the Editor API ("use the Roslyn
                // compiler"); it is still the one way to compile files against the project's assemblies.
#pragma warning disable 618
                var builder = new AssemblyBuilder(Path.Combine(dir, "UrpifyCheck.dll"), files.ToArray())
                    { flags = AssemblyBuilderFlags.None, referencesOptions = ReferencesOptions.UseEngineModules };
#pragma warning restore 618
                if (defines != null && defines.Length > 0) builder.additionalDefines = defines;
                CompilerMessage[] messages = null;
                builder.buildFinished += (_, m) => messages = m;
                if (!builder.Build()) { errors.Add(new Error { Message = "The C# check could not start (the Editor is compiling)." }); return errors; }
                var started = DateTime.UtcNow;
                while (builder.status != AssemblyBuilderStatus.Finished)
                {
                    if ((DateTime.UtcNow - started).TotalSeconds > timeoutSeconds) { errors.Add(new Error { Message = "The C# check did not finish in time." }); return errors; }
                    System.Threading.Thread.Sleep(20);
                }
                foreach (var m in messages ?? Array.Empty<CompilerMessage>())
                {
                    if (m.type != CompilerMessageType.Error) continue;
                    var full = string.IsNullOrEmpty(m.file) ? "" : Path.GetFullPath(m.file);
                    errors.Add(new Error { File = back.TryGetValue(full, out var orig) ? orig : m.file ?? "", Line = m.line, Message = m.message });
                }
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
            return errors;
        }

        /// <summary>Errors of the second set that the first did not have (same file and message), to judge a rewrite.</summary>
        public static List<Error> New(List<Error> before, List<Error> after)
        {
            string Key(Error e) => e.File + "|" + System.Text.RegularExpressions.Regex.Replace(e.Message, @"\(\d+,\d+\)", "");
            var known = new HashSet<string>(before.Select(Key), StringComparer.Ordinal);
            return after.Where(e => !known.Contains(Key(e))).ToList();
        }
    }
}
