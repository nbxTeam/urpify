using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using Urpify.Scanner;

namespace Urpify.Pipeline.AfterUnity
{
    /// <summary>
    /// URP Assets without their texture resources (UrpAssetTextures; architect decision 6 after M8): a built player of
    /// URP 14 renders black with them. Each one gets its textures the way URP fills them in the Editor, through the
    /// asset's own textures getter (ResourceReloader.ReloadAllNullIn with the package's paths), and is saved, so the
    /// player has them too.
    /// </summary>
    static class UrpAssetTexturesFix
    {
        public static List<KitConverterItem> Run()
        {
            var items = new List<KitConverterItem>();
            foreach (var path in UrpAssetTextures.EmptyAssets())
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                var item = new KitConverterItem { Path = path, Converter = "URP Asset textures" };
                var missing = UrpAssetTextures.MissingTextures(asset);
                var getter = asset.GetType().GetProperty("textures", BindingFlags.Public | BindingFlags.Instance);
                try { getter?.GetValue(asset); }
                catch (TargetInvocationException e) { item.Notes.Add("Not supported yet: URP could not load its textures: " + e.InnerException?.Message); }
                var still = UrpAssetTextures.MissingTextures(asset);
                if (getter == null || still.Count > 0)
                {
                    item.Outcome = "NotSupportedYet";
                    item.Notes.Add($"Not supported yet: {string.Join(", ", still)} still empty. Open the URP Asset in the Inspector and save the project; URP fills them in the Editor.");
                }
                else
                {
                    EditorUtility.SetDirty(asset);
                    item.Outcome = "Auto";
                    item.Notes.Add($"Info: texture resources {string.Join(", ", missing)} filled from the URP package. Without them a built player throws NullReferenceException every frame and renders black.");
                }
                items.Add(item);
            }
            if (items.Any(i => i.Outcome == "Auto")) AssetDatabase.SaveAssets();
            return items;
        }
    }
}
