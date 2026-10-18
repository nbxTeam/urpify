using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Urpify.Pipeline
{
    /// <summary>
    /// Adds a renderer feature to every Universal Renderer under Assets, the way URP's renderer inspector does
    /// (ScriptableRendererDataEditor.AddComponent: a sub-asset of the renderer, listed in m_RendererFeatures with its
    /// local file id in m_RendererFeatureMap). Serialized fields and a type found by name, so the Free package needs no
    /// reference to URP or to the generated code.
    /// </summary>
    public static class RendererFeatureInstaller
    {
        public static Type FindType(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName, false)).FirstOrDefault(t => t != null);

        /// <summary>Every feature of <paramref name="featureType"/> in the renderers under Assets.</summary>
        public static List<ScriptableObject> Instances(Type featureType) =>
            AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p)).OfType<ScriptableObject>().Where(o => o != null && o.GetType() == featureType).ToList();

        /// <summary>Adds one instance of <paramref name="featureType"/> to each renderer that has none; returns one line per renderer changed.</summary>
        public static List<string> AddToAllRenderers(Type featureType, string displayName)
        {
            var changes = new List<string>();
            foreach (var path in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal))
            {
                var data = AssetDatabase.LoadMainAssetAtPath(path) as ScriptableObject;
                if (data == null) continue;
                var so = new SerializedObject(data);
                var features = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                if (features == null || map == null) { changes.Add($"{path}: renderer features not found, {displayName} not added"); continue; }
                bool present = false;
                for (int i = 0; i < features.arraySize; i++)
                    if (features.GetArrayElementAtIndex(i).objectReferenceValue?.GetType() == featureType) present = true;
                if (present) continue;
                var feature = ScriptableObject.CreateInstance(featureType);
                feature.name = displayName;
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                int n = features.arraySize;
                features.arraySize = n + 1;
                features.GetArrayElementAtIndex(n).objectReferenceValue = feature;
                map.arraySize = n + 1;
                map.GetArrayElementAtIndex(n).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                changes.Add($"{path}: {displayName} renderer feature added");
            }
            AssetDatabase.SaveAssets();
            return changes;
        }
    }
}
