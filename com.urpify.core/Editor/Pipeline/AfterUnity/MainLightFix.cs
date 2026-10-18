using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Urpify.Pipeline.AfterUnity
{
    /// <summary>
    /// Quality levels with Pixel Light Count 0 (architect decision 8 after M7). Unity's converter gives the URP Asset of
    /// such a level Main Light = Disabled and Additional Lights = Disabled (URP 17.5.0 BuiltInToURP3DRenderSettingsConverter.cs:122;
    /// URP 14.0.12 RenderSettingsConverter.cs:200). Built-in still draws the brightest directional light per pixel at any
    /// Pixel Light Count, and lights the others per vertex; under URP the level renders without its sun (Portals in a
    /// built game, which starts on such a level: 34-38 % of the pixels darker, M7). The URP Asset of each such level gets
    /// Main Light = Per Pixel and, when Disabled, Additional Lights = Per Vertex, as Built-in drew them.
    /// </summary>
    static class MainLightFix
    {
        const int Disabled = 0, PerPixel = 1, PerVertex = 2; // URP's LightRenderingMode (UniversalRenderPipelineAsset.cs)

        public static List<KitConverterItem> Run()
        {
            var items = new List<KitConverterItem>();
            var quality = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
            if (quality == null) return items;
            var levels = new SerializedObject(quality).FindProperty("m_QualitySettings");
            if (levels == null) return items;
            var done = new HashSet<Object>();
            for (int i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                var pixelLights = level.FindPropertyRelative("pixelLightCount");
                var rp = level.FindPropertyRelative("customRenderPipeline")?.objectReferenceValue;
                if (pixelLights == null || pixelLights.intValue != 0 || rp == null || !done.Add(rp)) continue;
                if (rp.GetType().Name != "UniversalRenderPipelineAsset") continue;
                var so = new SerializedObject(rp);
                var main = so.FindProperty("m_MainLightRenderingMode");
                var additional = so.FindProperty("m_AdditionalLightsRenderingMode");
                var path = AssetDatabase.GetAssetPath(rp);
                var item = new KitConverterItem { Path = path, Converter = "Main light" };
                var name = level.FindPropertyRelative("name")?.stringValue ?? i.ToString();
                if (main != null && main.intValue == Disabled)
                {
                    main.intValue = PerPixel;
                    item.Notes.Add($"Info: quality level \"{name}\" (Pixel Light Count 0): Main Light Disabled -> Per Pixel, as Built-in draws the main directional light at any Pixel Light Count.");
                }
                if (additional != null && additional.intValue == Disabled)
                {
                    additional.intValue = PerVertex;
                    item.Notes.Add($"Info: quality level \"{name}\": Additional Lights Disabled -> Per Vertex, as Built-in lights them with no pixel lights.");
                }
                if (item.Notes.Count == 0) continue;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rp);
                item.Outcome = "Auto";
                items.Add(item);
            }
            if (items.Count > 0) AssetDatabase.SaveAssets();
            return items;
        }
    }
}
