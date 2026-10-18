using System;
using UnityEditor;
using UnityEngine;

namespace Urpify
{
    /// <summary>What a shader converter writes (TZ 4.3 "Output").</summary>
    public enum ShaderOutput
    {
        /// <summary>The converted shader replaces the original (backed up by the migration step).</summary>
        ReplaceOriginal = 0,
        /// <summary>The converted shader next to the original as Name_URP.shader, "Name (URP)"; materials move to it.</summary>
        WriteCopy = 1,
        // 1 is kept for "Write copy" (TZ 4.3), which comes with the Settings panel.
        /// <summary>
        /// One file for both pipelines (TZ 6.12): the original SubShaders stay as they are and a URP SubShader with
        /// PackageRequirements is added before them.
        /// </summary>
        KeepBuiltInSupport = 2,
    }

    /// <summary>What a converted GrabPass reads (TZ 4.3 "GrabPass source").</summary>
    public enum GrabPassSource
    {
        /// <summary>URP's Opaque Texture: the screen after opaque objects (transparent objects behind are missing).</summary>
        OpaqueTexture = 0,
        /// <summary>The screen after transparent objects, copied by Urpify's renderer feature; the GrabPass objects draw after it.</summary>
        AfterTransparents = 1,
    }

    /// <summary>Where OnRenderImage effects run in URP (TZ 4.3 "Screen effects inject at"); values are URP RenderPassEvent numbers.</summary>
    public enum ScreenEffectsPoint
    {
        AfterPostProcessing = 600,
        BeforePostProcessing = 550,
        AfterOpaques = 300,
    }

    /// <summary>
    /// Team settings (TZ 4.3): ProjectSettings/UrpifySettings.asset, meant to be committed. Personal settings are
    /// in <see cref="KitUserSettings"/>.
    /// </summary>
    /// <summary>Shadows of converted surface shaders (TZ 4.3).</summary>
    public enum ShadowsOption
    {
        SameAsOriginal = 0,
        Always = 1,
        Never = 2,
    }

    /// <summary>Which scenes Compare captures (TZ 4.3): those in Build Settings, every scene under Assets, or a chosen list.</summary>
    public enum CompareScenes
    {
        BuildSettings = 0,
        All = 1,
        Selected = 2,
    }

    /// <summary>
    /// Size of the Compare captures (TZ 4.3): the Game view's, so effects that depend on the resolution (the ASCII effect of
    /// the owner's project draws a character per 8 x 8 pixels) look as the user sees them; or a size of the user's own.
    /// </summary>
    public enum CompareSize
    {
        GameView = 0,
        Custom = 1,
    }

    [FilePath("ProjectSettings/UrpifySettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class KitProjectSettings : ScriptableSingleton<KitProjectSettings>
    {
        [SerializeField] ShaderOutput output = ShaderOutput.ReplaceOriginal;
        [SerializeField] GrabPassSource grabPass = GrabPassSource.OpaqueTexture;
        [SerializeField] ScreenEffectsPoint screenEffects = ScreenEffectsPoint.AfterPostProcessing;
        [SerializeField] ShadowsOption shadows = ShadowsOption.SameAsOriginal;
        [SerializeField] bool srpBatcher = true;
        [SerializeField] CompareSize compareSize = CompareSize.GameView;
        [SerializeField] int compareWidth = 960;
        [SerializeField] int compareHeight = 540;
        [SerializeField] int compareFrames = 30;
        [SerializeField] CompareScenes compareScenes = CompareScenes.BuildSettings;
        [SerializeField] string[] compareSelected = new string[0];

        public ShadowsOption Shadows { get => shadows; set { if (shadows == value) return; shadows = value; Save(true); } }
        /// <summary>Material properties in the UnityPerMaterial CBUFFER, so the SRP Batcher can batch the converted shaders.</summary>
        public bool SrpBatcher { get => srpBatcher; set { if (srpBatcher == value) return; srpBatcher = value; Save(true); } }

        /// <summary>Compare: the Game view's size (the default, owner's check 19.10.2026, item 5) or <see cref="CompareWidth"/> x <see cref="CompareHeight"/>.</summary>
        public CompareSize CompareSize { get => compareSize; set { if (compareSize == value) return; compareSize = value; Save(true); } }
        /// <summary>Compare: capture size when <see cref="CompareSize"/> is Custom, and when there is no Game view (960 x 540 by default).</summary>
        public int CompareWidth { get => compareWidth; set { value = Mathf.Clamp(value, 64, 7680); if (compareWidth == value) return; compareWidth = value; Save(true); } }
        public int CompareHeight { get => compareHeight; set { value = Mathf.Clamp(value, 64, 4320); if (compareHeight == value) return; compareHeight = value; Save(true); } }
        /// <summary>Compare: frames played before the capture, at a fixed time step (30 by default).</summary>
        public int CompareFrames { get => compareFrames; set { value = Mathf.Clamp(value, 1, 600); if (compareFrames == value) return; compareFrames = value; Save(true); } }
        public CompareScenes CompareScenes { get => compareScenes; set { if (compareScenes == value) return; compareScenes = value; Save(true); } }
        public string[] CompareSelected { get => compareSelected ?? new string[0]; set { compareSelected = value ?? new string[0]; Save(true); } }

        public ScreenEffectsPoint ScreenEffects
        {
            get => screenEffects;
            set
            {
                if (screenEffects == value) return;
                screenEffects = value;
                Save(true);
            }
        }

        public GrabPassSource GrabPass
        {
            get => grabPass;
            set
            {
                if (grabPass == value) return;
                grabPass = value;
                Save(true);
            }
        }

        /// <summary>"opaque-texture" / "after-transparents" as the command line writes them.</summary>
        public static bool TryParseGrabPass(string text, out GrabPassSource value)
        {
            value = GrabPassSource.OpaqueTexture;
            switch ((text ?? "").Trim().ToLowerInvariant().Replace("_", "-"))
            {
                case "opaque": case "opaque-texture": case "opaquetexture": value = GrabPassSource.OpaqueTexture; return true;
                case "after-transparents": case "aftertransparents": case "transparent": value = GrabPassSource.AfterTransparents; return true;
                default: return false;
            }
        }

        public ShaderOutput Output
        {
            get => output;
            set
            {
                if (output == value) return;
                output = value;
                Save(true);
            }
        }

        /// <summary>"replace" / "keep-builtin" as the command line writes them.</summary>
        public static bool TryParseOutput(string text, out ShaderOutput value)
        {
            value = ShaderOutput.ReplaceOriginal;
            switch ((text ?? "").Trim().ToLowerInvariant().Replace("_", "-"))
            {
                case "replace": case "replace-original": case "replaceoriginal": value = ShaderOutput.ReplaceOriginal; return true;
                case "write-copy": case "writecopy": case "copy": value = ShaderOutput.WriteCopy; return true;
                case "keep-builtin": case "keep-built-in": case "keepbuiltinsupport": case "keep-builtin-support": value = ShaderOutput.KeepBuiltInSupport; return true;
                default: return false;
            }
        }

        public static string CliName(ShaderOutput value) => value == ShaderOutput.KeepBuiltInSupport ? "keep-builtin" : value == ShaderOutput.WriteCopy ? "write-copy" : "replace";
    }
}
