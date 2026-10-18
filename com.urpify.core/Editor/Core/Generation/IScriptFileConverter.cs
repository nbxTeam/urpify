using System.Collections.Generic;

namespace Urpify.Generation
{
    /// <summary>
    /// A C# script converter (Pro), found by the Urpify converters step through the TypeCache like
    /// <see cref="IShaderFileConverter"/>. The result uses <see cref="ShaderConversion"/>: the rewritten text, notes, tier.
    /// </summary>
    public interface IScriptFileConverter
    {
        string Name { get; }
        int Order { get; }
        /// <summary>True when the script has something this converter rewrites or reports.</summary>
        bool Handles(string text, string path);
        ShaderConversion Convert(string text, string path);
        /// <summary>Files converted scripts need, as (package path, project path) pairs copied into the project.</summary>
        IEnumerable<KeyValuePair<string, string>> RuntimeFiles();
        /// <summary>Renderer features the converted scripts need, as (full type name, display name); added to every renderer.</summary>
        IEnumerable<KeyValuePair<string, string>> RendererFeatures();
        /// <summary>Sets up one renderer feature of <see cref="RendererFeatures"/> for what the project needs; returns one line per change.</summary>
        List<string> ConfigureRendererFeature(UnityEngine.Object feature, ICollection<string> requirements);
        /// <summary>True when the runtime files are needed even with no script to rewrite (a requirement the feature serves).</summary>
        bool NeedsRuntime(ICollection<string> requirements);
    }
}
