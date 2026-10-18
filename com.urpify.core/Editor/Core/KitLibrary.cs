namespace Urpify
{
    /// <summary>
    /// The shader library of Urpify Pro as the Free scanner sees it in a project: where the project's copy lives
    /// and which version this kit ships (copies of other versions, in exported packages, are reported).
    /// </summary>
    public static class KitLibrary
    {
        /// <summary>Raise with every library change; an exported package's UrpCompat copy carries it in each file's header.</summary>
        public const string Version = "0.9.0";

        /// <summary>The project's own copy, which converted shaders include.</summary>
        public const string ProjectFolder = "Assets/Urpify/ShaderLibrary/";
    }
}
