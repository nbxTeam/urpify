using System;
using System.Collections.Generic;

namespace Urpify.Scanner
{
    /// <summary>
    /// Facts about URP that decide findings. Every entry names where it was checked.
    /// URP 17.5.0 = 6000.5.9f1 package cache; URP 14.0.12 = 2022.3.62f3 BuiltInPackages.
    /// </summary>
    public static class UrpFacts
    {
        /// <summary>
        /// LightMode values URP draws with the error shader (pink) in the Editor and development builds, and skips in release builds.
        /// Source: Runtime/RenderingUtils.cs m_LegacyShaderPassNames and CreateRendererListObjectsWithError
        /// ([Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]) in URP 17.5.0 and 14.0.12.
        /// Experiment: LightMode "Always" renders pink in both (Spikes/M1/Results).
        /// </summary>
        public static readonly HashSet<string> LegacyLightModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Always", "ForwardBase", "PrepassBase", "Vertex", "VertexLMRGBM", "VertexLM" };

        /// <summary>
        /// LightMode values the forward renderer draws. "" (no tag) is drawn as SRPDefaultUnlit.
        /// Source: Runtime/Passes/DrawObjectsPass.cs shaderTagIds in URP 17.5.0; experiment: an untagged pass draws.
        /// </summary>
        public static readonly HashSet<string> DrawnLightModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "", "SRPDefaultUnlit", "UniversalForward", "UniversalForwardOnly" };

        /// <summary>
        /// Built-in shaders with an upgrader in Unity's Render Pipeline Converter, by URP major version.
        /// Source: URP 17.5.0 Editor/Tools/MaterialUpgrader/MaterialUpgraderProviders.cs;
        /// URP 14.0.12 Editor/UniversalRenderPipelineMaterialUpgrader.cs.
        /// </summary>
        static readonly string[] UpgradersCommon =
        {
            "Standard", "Standard (Specular setup)",
            "Legacy Shaders/Diffuse", "Legacy Shaders/Diffuse Detail", "Legacy Shaders/Diffuse Fast", "Legacy Shaders/Specular",
            "Legacy Shaders/Bumped Diffuse", "Legacy Shaders/Bumped Specular", "Legacy Shaders/Parallax Diffuse", "Legacy Shaders/Parallax Specular",
            "Legacy Shaders/VertexLit", "Legacy Shaders/Transparent/Cutout/VertexLit",
            "Legacy Shaders/Reflective/Bumped Diffuse", "Legacy Shaders/Reflective/Bumped Specular", "Legacy Shaders/Reflective/Bumped Unlit",
            "Legacy Shaders/Reflective/Bumped VertexLit", "Legacy Shaders/Reflective/Diffuse", "Legacy Shaders/Reflective/Specular",
            "Legacy Shaders/Reflective/VertexLit", "Legacy Shaders/Reflective/Parallax Diffuse", "Legacy Shaders/Reflective/Parallax Specular",
            "Legacy Shaders/Self-Illumin/Diffuse", "Legacy Shaders/Self-Illumin/Bumped Diffuse", "Legacy Shaders/Self-Illumin/Parallax Diffuse",
            "Legacy Shaders/Self-Illumin/Specular", "Legacy Shaders/Self-Illumin/Bumped Specular", "Legacy Shaders/Self-Illumin/Parallax Specular",
            "Legacy Shaders/Self-Illumin/VertexLit",
            "Legacy Shaders/Transparent/Diffuse", "Legacy Shaders/Transparent/Specular", "Legacy Shaders/Transparent/Bumped Diffuse",
            "Legacy Shaders/Transparent/Bumped Specular", "Legacy Shaders/Transparent/Cutout/Diffuse", "Legacy Shaders/Transparent/Cutout/Specular",
            "Legacy Shaders/Transparent/Cutout/Bumped Diffuse", "Legacy Shaders/Transparent/Cutout/Bumped Specular",
            "Legacy Shaders/Lightmapped/Diffuse", "Legacy Shaders/Lightmapped/Specular", "Legacy Shaders/Lightmapped/VertexLit",
            "Legacy Shaders/Lightmapped/Bumped Diffuse", "Legacy Shaders/Lightmapped/Bumped Specular",
            "Sprites/Diffuse", "UI/Lit/Bumped", "UI/Lit/Detail", "UI/Lit/Refraction", "UI/Lit/Refraction Detail", "UI/Lit/Transparent",
            "Mobile/Diffuse", "Mobile/Bumped Specular", "Mobile/Bumped Diffuse", "Mobile/Unlit (Supports Lightmap)", "Mobile/VertexLit",
            "Mobile/VertexLit (Only Directional Lights)", "Mobile/Particles/VertexLit Blended",
            "Nature/Terrain/Standard", "Nature/SpeedTree", "Nature/SpeedTree Billboard", "Nature/SpeedTree8",
            "Particles/Standard Surface", "Particles/Standard Unlit", "Autodesk Interactive",
        };

        static readonly string[] Upgraders17 = { "Mobile/Bumped Specular (1 Directional Realtime Light)", "Legacy Shaders/Particles/VertexLit Blended" };
        static readonly string[] Upgraders14 = { "Mobile/Bumped Specular (1 Directional Light)", "Particles/VertexLit Blended" };

        static HashSet<string> upgraders;

        /// <summary>URP major version the project will get: 17 on Unity 6, 14 on 2022.3.</summary>
        public static int UrpMajorForEditor
        {
            get
            {
                var v = UnityEngine.Application.unityVersion;
                return v.StartsWith("2022.", StringComparison.Ordinal) ? 14 : v.StartsWith("2021.", StringComparison.Ordinal) ? 12 : 17;
            }
        }

        public static bool HasUnityUpgrader(string shaderName)
        {
            if (upgraders == null)
            {
                upgraders = new HashSet<string>(UpgradersCommon, StringComparer.Ordinal);
                foreach (var s in UrpMajorForEditor == 14 ? Upgraders14 : Upgraders17) upgraders.Add(s);
            }
            return upgraders.Contains(shaderName);
        }

        /// <summary>
        /// Camera callbacks URP never invokes. Experiment (Spikes/M1/Results, URP 17.5.0 and 14.0.12, two Camera.Render calls):
        /// OnPreCull 0, OnPreRender 0, OnPostRender 0, OnRenderImage 0 under URP; each 2 under Built-in.
        /// OnRenderObject and OnWillRenderObject are invoked under URP.
        /// </summary>
        public static readonly string[] DeadCameraCallbacks = { "OnPreCull", "OnPreRender", "OnPostRender" };

        /// <summary>
        /// Shader variables that only the Built-in pipeline sets for normal rendering. URP 14.0.12 never sets them;
        /// URP 17.5.0 sets them only in Core Runtime/PathTracing/Environment/CubemapRender.cs (grep of Runtime and ShaderLibrary).
        /// </summary>
        public static readonly string[] BuiltInOnlyLightVariables = { "_LightColor0", "_WorldSpaceLightPos0" };
    }
}
