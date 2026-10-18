using System;
using System.Collections.Generic;

namespace Urpify.Generation
{
    /// <summary>
    /// The text every shader file of a running conversion step had before the step wrote anything. A converter that reads
    /// another shader of the project (UsePass) takes its original from here: the file on disk may already be the converted
    /// URP version, and converting that pass again declared _LightDirection and UnityPerMaterial twice (19 UnToon shaders,
    /// ShaderSet6P, 2026-10-12). Empty outside a step.
    /// </summary>
    public static class OriginalShaderTexts
    {
        static readonly Dictionary<string, string> s_Texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void Set(string path, string text)
        {
            if (!string.IsNullOrEmpty(path) && text != null) s_Texts[Key(path)] = text;
        }

        public static void Clear() => s_Texts.Clear();

        public static bool TryGet(string path, out string text) => s_Texts.TryGetValue(Key(path ?? ""), out text);

        static string Key(string path) => path.Replace('\\', '/');
    }
}
