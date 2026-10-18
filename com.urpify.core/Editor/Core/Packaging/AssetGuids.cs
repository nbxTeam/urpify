using System.Security.Cryptography;
using System.Text;

namespace Urpify.Packaging
{
    /// <summary>Asset GUIDs Urpify writes itself.</summary>
    public static class AssetGuids
    {
        /// <summary>
        /// The same GUID for the same key on every machine: Urpify's shader library files get one GUID in every project
        /// and every exported package, so two packages that ship the library do not import it twice.
        /// </summary>
        public static string Stable(string key)
        {
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes("Urpify/" + key));
                var sb = new StringBuilder(32);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>A .meta for a text asset Unity imports with its default importer (shader includes, shaders, text).</summary>
        public static string DefaultMeta(string guid, string importer) =>
            "fileFormatVersion: 2\nguid: " + guid + "\n" + importer + ":\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        /// <summary>The importer Unity uses for a file extension Urpify writes.</summary>
        public static string ImporterFor(string path)
        {
            var p = path.ToLowerInvariant();
            if (p.EndsWith(".shader")) return "ShaderImporter";
            if (p.EndsWith(".hlsl") || p.EndsWith(".cginc") || p.EndsWith(".cg") || p.EndsWith(".glslinc")) return "ShaderIncludeImporter";
            if (p.EndsWith(".cs")) return "MonoImporter";
            if (p.EndsWith(".asmdef")) return "AssemblyDefinitionImporter";
            return "DefaultImporter";
        }
    }
}
