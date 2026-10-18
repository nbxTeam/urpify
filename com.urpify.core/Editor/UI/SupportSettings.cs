using UnityEngine;

namespace Urpify.UI
{
    /// <summary>
    /// Where Send report goes (TZ 11.3): the Discord webhook of the support channel, kept in an asset of the package
    /// (Editor/UI/SupportSettings.asset) so an update of the package can change it. Empty means Send is not available yet
    /// and only Save zip instead works.
    /// </summary>
    public sealed class SupportSettings : ScriptableObject
    {
        public string webhookUrl = "";

        /// <summary>
        /// Largest archive Send posts, in MB. Discord's limit for files sent without Nitro is not verified here; 10 MB is
        /// the conservative value (larger archives are offered as Save zip instead only).
        /// </summary>
        public float maxAttachmentMB = 10f;

        public static string AssetPath => KitPaths.In(KitPaths.Core, "Editor/UI/SupportSettings.asset");

        public static SupportSettings Load() => UnityEditor.AssetDatabase.LoadAssetAtPath<SupportSettings>(AssetPath);
    }
}
