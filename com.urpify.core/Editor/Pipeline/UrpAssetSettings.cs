using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Urpify.Analysis;
using Urpify.Generation;
using Urpify.Parsing;

namespace Urpify.Pipeline
{
    /// <summary>
    /// URP Asset and renderer settings that converted and kept shaders rely on (TZ 6.5, 6.6; M4 finding on D01): Depth
    /// Texture, Opaque Texture at full resolution, and depth copied after opaque objects when a transparent shader reads
    /// it. Unity's converter leaves Copy Depth Mode at URP's default for new renderers, After Transparents (URP 14.0.12
    /// and 17.5.0 UniversalRendererData.m_CopyDepthMode). A transparent shader then reads no depth on URP 17 and the
    /// previous frame's depth on URP 14 (Spikes/M4/d01-lag-grid.png). Every URP Asset and renderer under Assets gets
    /// the setting, so all quality levels behave the same. Serialized fields are used, so the Free package needs no
    /// reference to URP.
    /// </summary>
    public static class UrpAssetSettings
    {
        /// <summary>What a shader URP draws as it is needs (the converters report the same for their results).</summary>
        public static List<string> NeedsOf(ShaderDocument doc, IncludeResolver resolver = null)
        {
            var needs = new List<string>();
            var sub = doc.UrpSubShader;
            if (sub == null) return needs;
            var blocks = sub.Passes.Where(p => p.Program != null).Select(p => p.Program).Concat(sub.Programs).Concat(doc.IncludeBlocksFor(sub.Index)).ToList();
            if (blocks.Count == 0) return needs;
            var scope = ProgramScope.Of(doc, blocks);
            bool Uses(string name) => scope.Parts.Any(p => p.Unit.Uses(name));
            if (Uses("_CameraDepthTexture") || Uses("_LastCameraDepthTexture"))
            {
                needs.Add(UrpRequirement.DepthTexture);
                if (sub.IsTransparentQueue) needs.Add(UrpRequirement.DepthBeforeTransparents);
            }
            if (Uses("_CameraOpaqueTexture")) needs.Add(UrpRequirement.OpaqueTexture);
            if (Uses("_CameraDepthNormalsTexture")) { needs.Add(UrpRequirement.DepthNormalsTexture); needs.Add(UrpRequirement.DepthTexture); }
            return needs;
        }

        /// <summary>Turns the settings on in every URP Asset and renderer under Assets; returns one line per change.</summary>
        public static List<string> Apply(ICollection<string> requirements)
        {
            var changes = new List<string>();
            if (requirements.Count == 0) return changes;
            foreach (var path in Find("UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                if (requirements.Contains(UrpRequirement.DepthTexture)) Set(so, "m_RequireDepthTexture", 1, path, "Depth Texture on", changes);
                if (requirements.Contains(UrpRequirement.OpaqueTexture))
                {
                    Set(so, "m_RequireOpaqueTexture", 1, path, "Opaque Texture on", changes);
                    Set(so, "m_OpaqueDownsampling", 0, path, "Opaque Downsampling None (GrabPass read the full-resolution screen)", changes);
                }
                if (so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(asset);
            }
            if (requirements.Contains(UrpRequirement.DepthBeforeTransparents))
                foreach (var path in Find("UniversalRendererData"))
                {
                    var data = AssetDatabase.LoadMainAssetAtPath(path);
                    if (data == null) continue;
                    var so = new SerializedObject(data);
                    var p = so.FindProperty("m_CopyDepthMode");
                    if (p != null && p.intValue == 1) // AfterTransparents; AfterOpaques and ForcePrepass both give depth in time
                    {
                        p.intValue = 0;
                        changes.Add($"{path}: Depth Texture Mode After Opaques (a transparent shader reads the depth texture)");
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(data);
                    }
                }
            AssetDatabase.SaveAssets();
            return changes;
        }

        static IEnumerable<string> Find(string typeName) =>
            AssetDatabase.FindAssets("t:" + typeName, new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal);

        static void Set(SerializedObject so, string field, int value, string path, string what, List<string> changes)
        {
            var p = so.FindProperty(field);
            if (p == null) { changes.Add($"{path}: {field} not found, {what} not set"); return; }
            if (p.propertyType == SerializedPropertyType.Boolean) { if (p.boolValue == (value != 0)) return; p.boolValue = value != 0; }
            else { if (p.intValue == value) return; p.intValue = value; }
            changes.Add($"{path}: {what}");
        }
    }
}
