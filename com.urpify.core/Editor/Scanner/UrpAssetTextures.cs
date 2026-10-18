using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Urpify.Scanner
{
    /// <summary>
    /// URP Assets saved without their texture resources (architect decision 6 after M8). Up to URP 14 the asset holds the
    /// blue noise and Bayer matrix textures (m_Textures), and URP reads them every frame
    /// (UniversalRenderPipeline.SetupPerFrameShaderConstants, URP 14.0.12 UniversalRenderPipeline.cs:1675). In the Editor
    /// the getter fills empty ones from the package (UniversalRenderPipelineAsset.textures, URP 14.0.12 :1754); a built
    /// player cannot, throws NullReferenceException every frame and stays black (Portals, 2022.3, M8). Unity's converter
    /// creates its URP Assets with ScriptableObject.CreateInstance (URP 14.0.12 RenderSettingsConverter.cs:128) instead of
    /// UniversalRenderPipelineAsset.Create, which fills them (:607-624), so its assets are saved empty. URP 17 reads
    /// these textures from the Graphics settings instead (URP 17.5.0 UniversalRenderPipeline.cs:2275; m_Textures obsolete).
    /// </summary>
    public static class UrpAssetTextures
    {
        public const string Property = "m_Textures";

        /// <summary>True when the URP of this Editor reads the textures from the URP Asset.</summary>
        public static bool UsedByUrp => UrpFacts.UrpMajorForEditor < 17;

        /// <summary>URP Assets under Assets with at least one empty texture resource, or none on URP 17.</summary>
        public static List<string> EmptyAssets()
        {
            var list = new List<string>();
            if (!UsedByUrp) return list;
            foreach (var path in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, System.StringComparer.Ordinal))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null && asset.GetType().Name == "UniversalRenderPipelineAsset" && MissingTextures(asset).Count > 0) list.Add(path);
            }
            return list;
        }

        /// <summary>Names of the asset's texture resources that are empty in its serialized data.</summary>
        public static List<string> MissingTextures(UnityEngine.Object asset)
        {
            var missing = new List<string>();
            var textures = new SerializedObject(asset).FindProperty(Property);
            if (textures == null) return missing;
            var end = textures.GetEndProperty();
            var p = textures.Copy();
            for (bool enter = true; p.NextVisible(enter) && !SerializedProperty.EqualContents(p, end); enter = false)
                if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue == null) missing.Add(p.name);
            return missing;
        }
    }
}
