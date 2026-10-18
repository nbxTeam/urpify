using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Urpify.Verify
{
    /// <summary>
    /// Renders something with the Built-in pipeline inside a URP project by switching the pipeline off for one synchronous
    /// call. Architect decisions after M0 (item 1) and M1 (item 1): switch and restore in one try/finally with nothing
    /// asynchronous in between; restore by loading the assets by path; afterwards compare both settings with the originals
    /// and clear the dirty flag of GraphicsSettings and QualitySettings; never in Play Mode; no scene is opened or created
    /// inside. Used by the shader panel's Built-in preview and by the self-check of converted shaders (QA-004).
    /// </summary>
    public static class BuiltInRender
    {
        /// <summary>Calls <paramref name="render"/> with the Built-in pipeline active. False (with the reason) when it could not.</summary>
        public static bool Run(Action render, out string failure)
        {
            failure = "";
            if (EditorApplication.isPlayingOrWillChangePlaymode) { failure = "Play Mode"; return false; }
            var defaultAsset = GraphicsSettings.defaultRenderPipeline;
            var qualityAsset = QualitySettings.renderPipeline;
            var defaultPath = defaultAsset != null ? AssetDatabase.GetAssetPath(defaultAsset) : "";
            var qualityPath = qualityAsset != null ? AssetDatabase.GetAssetPath(qualityAsset) : "";
            try
            {
                GraphicsSettings.defaultRenderPipeline = null;
                QualitySettings.renderPipeline = null;
                render();
            }
            catch (Exception e)
            {
                failure = e.GetType().Name + ": " + e.Message;
            }
            finally
            {
                var d = defaultPath.Length > 0 ? AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(defaultPath) : null;
                var q = qualityPath.Length > 0 ? AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(qualityPath) : null;
                GraphicsSettings.defaultRenderPipeline = d;
                QualitySettings.renderPipeline = q;
                foreach (var file in new[] { "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset" })
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(file))
                        if (o != null) EditorUtility.ClearDirty(o);
                if (GraphicsSettings.defaultRenderPipeline != d || QualitySettings.renderPipeline != q
                    || (defaultPath.Length > 0 && d == null) || (qualityPath.Length > 0 && q == null))
                {
                    failure = "NotRestored";
                    Debug.LogError("[Urpify] The render pipeline could not be restored after a Built-in render; check Project Settings > Graphics and Quality.");
                }
            }
            return failure.Length == 0;
        }
    }
}
