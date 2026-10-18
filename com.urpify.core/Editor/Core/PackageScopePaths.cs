using System;

namespace Urpify
{
    /// <summary>
    /// Where a .unitypackage scope is imported for analysis (TZ 4.2: "import into a temporary folder"). Project scans and
    /// the migration steps leave this folder out.
    /// </summary>
    public static class PackageScopePaths
    {
        public const string Root = "Assets/UrpifyPackageScope";

        public static bool IsInside(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            var p = assetPath.Replace('\\', '/');
            return p.Equals(Root, StringComparison.OrdinalIgnoreCase) || p.StartsWith(Root + "/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
