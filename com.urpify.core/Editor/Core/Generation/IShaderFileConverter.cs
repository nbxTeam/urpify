using System.Collections.Generic;
using Urpify.Parsing;

namespace Urpify.Generation
{
    /// <summary>
    /// A shader converter (Pro). The Urpify converters migration step finds implementations through the TypeCache, so the
    /// Free package works without them.
    /// </summary>
    public interface IShaderFileConverter
    {
        string Name { get; }
        /// <summary>Lower runs first when several converters handle a shader.</summary>
        int Order { get; }
        bool Handles(ShaderDocument doc);
        ShaderConversion Convert(string text, string path);
        /// <summary>The conversion written for an Output setting (TZ 4.3): replacing the original, or both pipelines in one file.</summary>
        ShaderConversion Convert(string text, string path, ShaderOutput output);
        /// <summary>Include files converted shaders need, as (package path, project path) pairs copied into the project.</summary>
        IEnumerable<KeyValuePair<string, string>> RuntimeFiles();
    }
}
