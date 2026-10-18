namespace Urpify
{
    /// <summary>Stable rule identifiers. They appear in reports and settings overrides; never rename one after release.</summary>
    public static class RuleIds
    {
        public const string ParseFailed = "PARSE.FAILED";

        // Shaders: what URP does with the passes (from Unity's compiled shader data)
        public const string ShaderLegacyPass = "SHADER.URP.LEGACY_PASS";
        public const string ShaderNoUrpPass = "SHADER.URP.NO_DRAWN_PASS";
        public const string ShaderCompileErrors = "SHADER.COMPILE_ERRORS";
        public const string ShaderAlreadyUrp = "SHADER.URP.ALREADY_SUPPORTED";

        // Shaders: constructs (static analysis)
        public const string Surface = "SHADER.SURFACE";
        public const string SurfaceCustomLighting = "SHADER.SURFACE.CUSTOM_LIGHTING";
        public const string SurfaceTessellation = "SHADER.SURFACE.TESSELLATION";
        public const string SurfaceFinalGBuffer = "SHADER.SURFACE.FINALGBUFFER";
        public const string SurfaceUnknownOption = "SHADER.SURFACE.UNKNOWN_OPTION";
        public const string SurfaceExtraPass = "SHADER.SURFACE.EXTRA_PASS";
        public const string ForwardBase = "SHADER.PASS.FORWARDBASE";
        public const string ForwardAdd = "SHADER.PASS.FORWARDADD";
        public const string ShadowCasterPass = "SHADER.PASS.SHADOWCASTER";
        public const string UsePassBuiltIn = "SHADER.PASS.USEPASS";
        public const string UsePassMissing = "SHADER.PASS.USEPASS_MISSING";
        public const string CameraDelegates = "SCRIPT.CAMERA_DELEGATES";
        public const string EffectFedByLightBuffers = "SCRIPT.LIGHT_BUFFER_EFFECT";
        public const string EffectUnrecordable = "SCRIPT.EFFECT_UNRECORDABLE";
        public const string EffectReadsGBuffer = "SCRIPT.EFFECT_GBUFFER";
        public const string LegacyLightMode = "SHADER.PASS.LEGACY_LIGHTMODE";
        public const string MultipleUnlitPasses = "SHADER.PASS.MULTIPLE_UNTAGGED";
        public const string BuiltInLightingVariables = "SHADER.BUILTIN_LIGHTING";
        public const string GrabPass = "SHADER.GRABPASS";
        public const string DepthTexture = "SHADER.DEPTH_TEXTURE";
        /// <summary>A transparent shader reads the depth texture (lab D01; M4).</summary>
        public const string DepthTextureTransparent = "SHADER.DEPTH_TEXTURE_TRANSPARENT";
        public const string DepthNormalsTexture = "SHADER.DEPTHNORMALS_TEXTURE";
        /// <summary>A shader reads Built-in's deferred G-buffer, which URP does not have (SSRT, independent test of 2026-10-12).</summary>
        public const string GBufferTexture = "SHADER.GBUFFER_TEXTURE";
        public const string ProjectorShader = "SHADER.PROJECTOR";
        public const string ImageEffectShader = "SHADER.IMAGE_EFFECT";
        public const string MissingInclude = "SHADER.MISSING_INCLUDE";
        /// <summary>Several copies of Urpify's shader library with different versions (UrpCompat folders of packages; M7).</summary>
        public const string LibraryCopies = "SHADER.LIBRARY_COPIES";

        // Catalog of known shaders and scripts (TZ 11.1)
        public const string CatalogKnown = "CATALOG.KNOWN";

        // Scripts
        public const string OnRenderImage = "SCRIPT.ONRENDERIMAGE";
        public const string CameraCommandBuffer = "SCRIPT.COMMANDBUFFER.CAMERA";
        public const string LightCommandBuffer = "SCRIPT.COMMANDBUFFER.LIGHT";
        public const string CameraCallbacks = "SCRIPT.CAMERA_CALLBACKS";
        /// <summary>OnWillRenderObject renders a camera or reads Camera.current (mirrors, planar water reflections; M7).</summary>
        public const string WillRenderCamera = "SCRIPT.WILLRENDER_CAMERA";
        public const string ReplacementShader = "SCRIPT.REPLACEMENT_SHADER";
        public const string ShaderFindBuiltIn = "SCRIPT.SHADER_FIND_BUILTIN";
        public const string DepthTextureMode = "SCRIPT.DEPTH_TEXTURE_MODE";
        public const string BuiltInPropertyNames = "SCRIPT.BUILTIN_PROPERTY_NAMES";
        public const string AnimatedMaterialProperties = "ANIMATION.MATERIAL_PROPERTIES";

        // Materials
        public const string MaterialUnityConverter = "MATERIAL.UNITY_CONVERTER";
        public const string MaterialStandardTransparent = "MATERIAL.STANDARD_TRANSPARENT";
        public const string MaterialSelfIllumin = "MATERIAL.SELF_ILLUMIN";
        public const string MaterialBuiltInNoUpgrader = "MATERIAL.BUILTIN_NO_UPGRADER";
        /// <summary>Unity's Tree Creator and soft-occlusion tree shaders (architect decision 7 after M3).</summary>
        public const string MaterialBuiltInTree = "MATERIAL.BUILTIN_TREE";
        public const string MaterialCustomShader = "MATERIAL.CUSTOM_SHADER";
        public const string MaterialVariant = "MATERIAL.VARIANT";

        // Scenes and prefabs
        public const string ProjectorComponent = "SCENE.PROJECTOR";
        public const string PostProcessingV2 = "SCENE.PPV2";
        public const string TerrainBuiltIn = "SCENE.TERRAIN_BUILTIN";
        public const string BinaryScene = "SCENE.BINARY";
        public const string BuiltInMaterialReference = "SCENE.BUILTIN_MATERIAL";
        public const string MissingMaterial = "SCENE.MISSING_MATERIAL";
        /// <summary>A renderer uses a material inside a package whose shader URP cannot draw (ProBuilder 5's default; smo-shaders on 2022.3).</summary>
        public const string PackageMaterialReference = "SCENE.PACKAGE_MATERIAL";
        /// <summary>Ambient light from the skybox and no baked lighting data: darker in a URP player (Portals, M8).</summary>
        public const string EnvironmentNotBaked = "SCENE.ENVIRONMENT_NOT_BAKED";

        // Project and settings
        public const string ScriptCompilationFailed = "PROJECT.SCRIPT_COMPILATION_FAILED";
        public const string UrpNotInstalled = "SETTINGS.URP_NOT_INSTALLED";
        public const string PostProcessingPackage = "SETTINGS.PPV2_PACKAGE";
        /// <summary>The Post-processing v2 package is installed and no scene or prefab uses it (after Unity's converter, or never did).</summary>
        public const string PostProcessingPackageUnused = "SETTINGS.PPV2_PACKAGE_UNUSED";
        /// <summary>A URP Asset saved without its texture resources: a URP 14 player renders black (Portals, 2022.3, M8).</summary>
        public const string UrpAssetTextures = "SETTINGS.URP_ASSET_TEXTURES";
    }
}
