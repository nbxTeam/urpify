using System.Collections.Generic;

namespace Urpify.Pipeline
{
    /// <summary>
    /// A converter for project assets other than shaders and scripts (Pro): materials, scenes, prefabs. The Urpify converters
    /// step runs it after the shaders, with the report of the Scan step (taken before Unity's converter), and backs up what
    /// it changes. Found through the TypeCache, so the Free package works without it.
    /// </summary>
    public interface IKitAssetConverter
    {
        string Name { get; }
        int Order { get; }
        List<KitConverterItem> Run(MigrationReport scan);
    }
}
