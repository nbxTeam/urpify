// Written by Tools/release.sh from the VERSION file at the repository root; edit VERSION, not this file.
namespace Urpify
{
    /// <summary>
    /// The version and the build of this copy of Urpify (owner's decision of 20.10.2026: one code base, one build flag). The
    /// Asset Store build installs no package itself (Asset Store Submission Guidelines 2.5.1.e) and uses no internal Editor
    /// API (2.5.g); its sources come from these with the full-build blocks taken out (Tools/strip_store.py). Static readonly
    /// rather than const, so the branches for the other build are not flagged as unreachable code.
    /// </summary>
    public static class BuildInfo
    {
        public static readonly string Version = "1.0.1";
        public static readonly bool AssetStore = false;
    }
}
