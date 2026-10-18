using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Urpify.UI
{
    /// <summary>
    /// Renders something with the Built-in pipeline inside a URP project (Built-in preview of the shader panel), by
    /// switching the pipeline off for one synchronous call. Architect decisions after M0 (item 1) and M1 (item 1):
    /// switch and restore in one try/finally with nothing asynchronous in between; restore by loading the assets by
    /// path; afterwards compare both settings with the originals and clear the dirty flag of GraphicsSettings and
    /// QualitySettings; never in Play Mode or while importing or compiling; no scene is opened or created inside.
    /// Any failure switches the Built-in preview setting to From captures and says why.
    /// </summary>
    public static class PipelineSwap
    {
        /// <summary>Why the last swap failed; empty when it did not.</summary>
        public static string Failure { get; private set; } = "";

        public static bool CanSwap(out string reason)
        {
            reason = "";
            if (KitUserSettings.Current.BuiltInPreview != BuiltInPreview.Live) reason = Failure.Length > 0 ? Failure : Loc.Get("ui.shader.swap.setting");
            else if (EditorApplication.isPlayingOrWillChangePlaymode) reason = Loc.Get("ui.shader.swap.playMode");
            else if (EditorApplication.isCompiling || EditorApplication.isUpdating) reason = Loc.Get("ui.shader.swap.busy");
            return reason.Length == 0;
        }

        /// <summary>Calls <paramref name="render"/> with the Built-in pipeline active. False when it could not.</summary>
        public static bool RenderBuiltIn(Action render)
        {
            if (!CanSwap(out _)) return false;
            Verify.BuiltInRender.Run(render, out var reason);
            string failure = reason.Length == 0 ? "" : reason == "NotRestored" ? Loc.Get("ui.shader.swap.notRestored") : Loc.Get("ui.shader.swap.failed", reason);
            if (failure.Length == 0) return true;
            Failure = failure;
            var settings = KitUserSettings.Current;
            settings.BuiltInPreview = BuiltInPreview.FromCaptures;
            try { settings.Save(); } catch (Exception e) { Debug.LogWarning("[Urpify] " + e.Message); }
            return false;
        }
    }
}
