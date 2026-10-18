using System;
using System.Collections.Generic;
using System.IO;

namespace Urpify.Parsing
{
    public enum IncludeKind
    {
        /// <summary>A file in the project or in a package, parsed and scanned.</summary>
        Local,
        /// <summary>A Built-in Render Pipeline include shipped inside the Editor (UnityCG.cginc and friends).</summary>
        BuiltIn,
        /// <summary>Could not be found.</summary>
        Missing,
    }

    public sealed class ResolvedInclude
    {
        public string Written = "";
        /// <summary>Project-relative path with forward slashes for Local; the file name for BuiltIn.</summary>
        public string Path = "";
        public IncludeKind Kind;
        public int Line;
        public HlslUnit Unit;
        public List<ResolvedInclude> Children = new List<ResolvedInclude>();
    }

    /// <summary>
    /// Resolves #include paths the way the shader compiler does for the cases that matter here:
    /// relative to the including file, then relative to the project root ("Assets/...", "Packages/...").
    /// Each file is scanned once per resolver and shared by every shader that includes it.
    /// </summary>
    public sealed class IncludeResolver
    {
        /// <summary>
        /// Include files that the Built-in Render Pipeline ships inside the Editor (CGIncludes folder).
        /// Taken from the file list of Editor/Data/CGIncludes in 6000.5.9f1 and 2022.3.62f3; see Documentation~/sources.md.
        /// </summary>
        public static readonly HashSet<string> BuiltInIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AutoLight.cginc",
            "GLSLSupport.glslinc",
            "GraniteShaderLib3.cginc",
            "HLSLSupport.cginc",
            "Internal/EditorUIE.cginc",
            "Internal/UnityUIE.cginc",
            "Lighting.cginc",
            "ShaderApiReflectionSupport.hlsl",
            "SpeedTree8Common.cginc",
            "SpeedTreeBillboardCommon.cginc",
            "SpeedTreeCommon.cginc",
            "SpeedTreeShaderLibrary.cginc",
            "SpeedTreeVertex.cginc",
            "SpeedTreeWind.cginc",
            "TerrainEngine.cginc",
            "TerrainPreview.cginc",
            "TerrainSplatmapCommon.cginc",
            "TerrainTool.cginc",
            "Tessellation.cginc",
            "TextCoreProperties.cginc",
            "TextCore_Properties.cginc",
            "TextCore_SDF_SSD.cginc",
            "UnityBuiltin2xTreeLibrary.cginc",
            "UnityBuiltin3xTreeLibrary.cginc",
            "UnityCG.cginc",
            "UnityCG.glslinc",
            "UnityColorGamut.cginc",
            "UnityCustomRenderTexture.cginc",
            "UnityDeferredLibrary.cginc",
            "UnityDeprecated.cginc",
            "UnityGBuffer.cginc",
            "UnityGlobalIllumination.cginc",
            "UnityImageBasedLighting.cginc",
            "UnityIndirect.cginc",
            "UnityInstancing.cginc",
            "UnityLegacyTextureStack.cginc",
            "UnityLightingCommon.cginc",
            "UnityMetaPass.cginc",
            "UnityPBSLighting.cginc",
            "UnityRayQuery.cginc",
            "UnityRayTracingMeshUtils.cginc",
            "UnityShaderUtilities.cginc",
            "UnityShaderVariables.cginc",
            "UnityShadowLibrary.cginc",
            "UnitySprites.cginc",
            "UnityStandardBRDF.cginc",
            "UnityStandardConfig.cginc",
            "UnityStandardCore.cginc",
            "UnityStandardCoreForward.cginc",
            "UnityStandardCoreForwardSimple.cginc",
            "UnityStandardInput.cginc",
            "UnityStandardMeta.cginc",
            "UnityStandardParticleEditor.cginc",
            "UnityStandardParticleInstancing.cginc",
            "UnityStandardParticleShadow.cginc",
            "UnityStandardParticles.cginc",
            "UnityStandardShadow.cginc",
            "UnityStandardUtils.cginc",
            "UnityStereoExtensions.glslinc",
            "UnityStereoSupport.glslinc",
            "UnityUI.cginc",
            "UnityUIEFilter.cginc",        };

        readonly string projectRoot;
        readonly Func<string, string> packagePathResolver;
        readonly Dictionary<string, HlslUnit> units = new Dictionary<string, HlslUnit>(StringComparer.OrdinalIgnoreCase);

        /// <param name="projectRoot">Absolute project root.</param>
        /// <param name="packagePathResolver">Maps "Packages/name/..." to an absolute path, or null when unknown.</param>
        public IncludeResolver(string projectRoot, Func<string, string> packagePathResolver = null)
        {
            this.projectRoot = projectRoot.Replace('\\', '/').TrimEnd('/');
            this.packagePathResolver = packagePathResolver;
        }

        /// <summary>Files scanned so far, project-relative.</summary>
        public IEnumerable<string> ScannedFiles => units.Keys;

        public List<ResolvedInclude> ResolveAll(HlslUnit unit, string includingAssetPath, int maxDepth = 16) =>
            ResolveAll(unit, includingAssetPath, maxDepth, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        List<ResolvedInclude> ResolveAll(HlslUnit unit, string includingAssetPath, int depth, HashSet<string> stack)
        {
            var list = new List<ResolvedInclude>();
            if (unit == null || depth <= 0) return list;
            foreach (var inc in unit.Includes)
            {
                var r = Resolve(inc.Path, includingAssetPath);
                r.Line = inc.Line;
                if (r.Kind == IncludeKind.Local && !stack.Contains(r.Path))
                {
                    r.Unit = Load(r.Path);
                    stack.Add(r.Path);
                    r.Children = ResolveAll(r.Unit, r.Path, depth - 1, stack);
                    stack.Remove(r.Path);
                }
                list.Add(r);
            }
            return list;
        }

        public ResolvedInclude Resolve(string written, string includingAssetPath)
        {
            var r = new ResolvedInclude { Written = written };
            var w = written.Replace('\\', '/');
            string dir = Path.GetDirectoryName(includingAssetPath ?? "")?.Replace('\\', '/') ?? "";
            var candidates = new List<string>();
            if (dir.Length > 0) candidates.Add(Normalize(dir + "/" + w));
            candidates.Add(Normalize(w));
            // "Assets/..." written against a nested project root, e.g. a package folder imported under Assets/:
            // resolve from every enclosing folder named Assets, innermost first. In a normal project this is the root.
            if (w.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && includingAssetPath != null)
            {
                var inc = includingAssetPath.Replace('\\', '/');
                for (int k = inc.LastIndexOf("/Assets/", StringComparison.OrdinalIgnoreCase); k > 0; k = inc.LastIndexOf("/Assets/", k - 1, StringComparison.OrdinalIgnoreCase))
                    candidates.Add(Normalize(inc.Substring(0, k + 1) + w));
            }
            foreach (var c in candidates)
            {
                if (c.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && File.Exists(projectRoot + "/" + c))
                { r.Kind = IncludeKind.Local; r.Path = c; return r; }
                if (c.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                {
                    var abs = packagePathResolver?.Invoke(c);
                    if (abs != null && File.Exists(abs)) { r.Kind = IncludeKind.Local; r.Path = c; return r; }
                    if (File.Exists(projectRoot + "/" + c)) { r.Kind = IncludeKind.Local; r.Path = c; return r; }
                }
            }
            var file = Path.GetFileName(w);
            if (BuiltInIncludes.Contains(w) || BuiltInIncludes.Contains(file)) { r.Kind = IncludeKind.BuiltIn; r.Path = file; return r; }
            r.Kind = IncludeKind.Missing;
            r.Path = w;
            return r;
        }

        HlslUnit Load(string assetPath)
        {
            if (units.TryGetValue(assetPath, out var u)) return u;
            string abs = assetPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)
                ? packagePathResolver?.Invoke(assetPath) ?? projectRoot + "/" + assetPath
                : projectRoot + "/" + assetPath;
            try
            {
                u = HlslScanner.Scan(new SourceText(File.ReadAllText(abs), assetPath));
            }
            catch (Exception e)
            {
                u = new HlslUnit();
                u.Errors.Add(new ParseError("Cannot read include: " + e.Message, 0));
            }
            units[assetPath] = u;
            return u;
        }

        static string Normalize(string p)
        {
            var parts = new List<string>();
            foreach (var part in p.Split('/'))
            {
                if (part.Length == 0 || part == ".") continue;
                if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); continue; }
                parts.Add(part);
            }
            return string.Join("/", parts);
        }

        /// <summary>All units reachable from a list of resolved includes, depth first, each once.</summary>
        public static IEnumerable<(ResolvedInclude include, HlslUnit unit)> Flatten(IEnumerable<ResolvedInclude> roots)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<ResolvedInclude>();
            foreach (var r in roots) stack.Push(r);
            var ordered = new List<(ResolvedInclude, HlslUnit)>();
            void Walk(ResolvedInclude r)
            {
                if (r.Kind != IncludeKind.Local || r.Unit == null || !seen.Add(r.Path)) return;
                ordered.Add((r, r.Unit));
                foreach (var c in r.Children) Walk(c);
            }
            foreach (var r in roots) Walk(r);
            return ordered;
        }
    }
}
