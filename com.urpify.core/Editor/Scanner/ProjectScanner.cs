using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Urpify.Parsing;
using Urpify.Rules;
using Debug = UnityEngine.Debug;

namespace Urpify.Scanner
{
    public sealed class ScanOptions
    {
        /// <summary>Folders or files, project-relative ("Assets", "Assets/Shaders/Toon.shader").</summary>
        public string[] Roots = { "Assets" };
        /// <summary>Shown in the report: "Project", "Folder", "File".</summary>
        public string ScopeLabel = "Project";
        public bool UseCache = true;
    }

    /// <summary>
    /// Read-only scan of a project. Drive <see cref="Run"/> step by step (the window does it on EditorApplication.update)
    /// or to the end (CLI). Never modifies assets.
    /// </summary>
    public sealed class ProjectScanner
    {
        public static readonly string KitVersion = BuildInfo.Version;

        public MigrationReport Report { get; private set; }
        public string Status { get; private set; } = "";
        public float Progress { get; private set; }
        public bool CancelRequested;

        static readonly string[] ModelExtensions = { ".fbx", ".obj", ".dae", ".3ds", ".blend", ".max", ".ma", ".mb", ".dxf" };
        static readonly string[] IncludeExtensions = { ".cginc", ".hlsl", ".cg", ".hlslinc", ".glslinc" };

        readonly string projectRoot = Path.GetFullPath(".").Replace('\\', '/');

        // Collected during the scan.
        readonly List<string> shaders = new List<string>(), scripts = new List<string>(), materials = new List<string>(),
            models = new List<string>(), scenes = new List<string>(), prefabs = new List<string>(), animations = new List<string>();
        readonly Dictionary<string, HashSet<string>> usedBy = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        readonly Dictionary<string, HashSet<string>> sceneAssets = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        readonly Dictionary<string, List<Finding>> findingsByAsset = new Dictionary<string, List<Finding>>(StringComparer.Ordinal);
        readonly Dictionary<string, (Severity sev, FixTier tier)> shaderVerdicts = new Dictionary<string, (Severity, FixTier)>(StringComparer.Ordinal);

        ShaderUrpEvaluator evaluator;
        IncludeResolver resolver;
        ScanCache cache;
        bool useCache = true;

        public void RunToEnd(ScanOptions options)
        {
            var it = Run(options);
            while (it.MoveNext()) { }
        }

        public IEnumerator<float> Run(ScanOptions options)
        {
            var total = Stopwatch.StartNew();
            Report = new MigrationReport
            {
                GeneratedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                UnityVersion = Application.unityVersion,
                UrpVersion = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.render-pipelines.universal")?.version ?? "",
                KitVersion = KitVersion,
                ProjectName = Path.GetFileName(projectRoot),
                Scope = options.ScopeLabel,
                ScopePaths = options.Roots,
            };
            var manifest = ReadManifest();
            useCache = options.UseCache;
            evaluator = new ShaderUrpEvaluator();
            resolver = new IncludeResolver(projectRoot, PackageAbsolutePath);
            // The assembly MVIDs change on every recompile, so edited rule code never reuses stale results.
            var salt = string.Join("|", KitVersion, Application.unityVersion, RuleRegistry.Fingerprint, Catalog.KnownSources.Shipped.Version,
                typeof(ProjectScanner).Assembly.ManifestModule.ModuleVersionId, typeof(RuleRegistry).Assembly.ManifestModule.ModuleVersionId,
                ScanCache.Sha1(string.Join(",", manifest.Select(kv => kv.Key + "=" + kv.Value))));
            cache = new ScanCache(projectRoot, ScanCache.Sha1(salt));

            Status = "Collecting files";
            Collect(options.Roots);
            yield return Progress = 0.02f;

            // Parse shader names and fallbacks first so the evaluator can follow Fallback chains.
            int stepsTotal = shaders.Count + scripts.Count + materials.Count + models.Count + scenes.Count * 2 + prefabs.Count * 2 + animations.Count + 1;
            int step = 0;
            float Next() => Progress = 0.02f + 0.97f * (++step) / Math.Max(1, stepsTotal);

            Phase("dependencies");
            // Usage: scenes and prefabs.
            foreach (var p in scenes.Concat(prefabs))
            {
                if (CancelRequested) { Finish(true, total); yield break; }
                Status = "Reading dependencies: " + p;
                var deps = new HashSet<string>(AssetDatabase.GetDependencies(p, true), StringComparer.Ordinal);
                deps.Remove(p);
                sceneAssets[p] = deps;
                foreach (var d in deps) AddUse(d, p);
                yield return Next();
            }

            Phase("shaders");
            foreach (var path in shaders)
            {
                if (CancelRequested) { Finish(true, total); yield break; }
                Status = "Shader: " + path;
                Guard(path, AssetKind.Shader, () => ScanShader(path));
                yield return Next();
            }
            Phase("materials");
            foreach (var path in materials)
            {
                if (CancelRequested) { Finish(true, total); yield break; }
                Status = "Material: " + path;
                Guard(path, AssetKind.Material, () => ReadOnly(path, () => ScanMaterial(path, AssetDatabase.LoadAssetAtPath<Material>(path), "")));
                yield return Next();
            }
            Phase("models");
            // Prefabs that carry their own materials (a Tree component's optimized bark and leaf materials) are scanned
            // like models: the materials inside are what renders.
            foreach (var path in models.Concat(prefabs.Where(HasEmbeddedMaterials)))
            {
                if (CancelRequested) { Finish(true, total); yield break; }
                Status = "Model: " + path;
                Guard(path, AssetKind.Material, () => ReadOnly(path, () =>
                {
                    foreach (var m in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                        ScanMaterial(path, m, m.name);
                }));
                yield return Next();
            }
            Phase("scripts");
            var scriptTexts = scripts.Select(p => { try { return (p, File.ReadAllText(Absolute(p))); } catch { return (p, (string)null); } }).ToList();
            lightFeeds = Analysis.LightBufferFeeds.Find(scriptTexts);
            effectReach = Analysis.ImageEffectReach.Find(scriptTexts,
                shaders.Select(p => { try { return (p, File.ReadAllText(Absolute(p))); } catch { return (p, (string)null); } }).ToList());
            foreach (var path in scripts)
            {
                if (CancelRequested) { Finish(true, total); yield break; }
                Status = "Script: " + path;
                Guard(path, AssetKind.Script, () => ScanScript(path));
                yield return Next();
            }
            Phase("scenes");
            foreach (var path in scenes.Concat(prefabs))
            {
                if (CancelRequested) { Finish(true, total); yield break; }
                Status = "Scene: " + path;
                Guard(path, path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? AssetKind.Prefab : AssetKind.Scene, () => ScanScene(path));
                yield return Next();
            }
            Phase("animations");
            foreach (var path in animations)
            {
                if (CancelRequested) { Finish(true, total); yield break; }
                Status = "Animation: " + path;
                Guard(path, AssetKind.Animation, () => ScanAnimation(path));
                yield return Next();
            }
            Phase("project");
            Status = "Project settings";
            ScanProject(manifest);
            yield return Next();

            Phase(null);
            Finish(false, total);
            yield return Progress = 1f;
        }

        string phaseName;
        readonly Stopwatch phaseClock = new Stopwatch();

        /// <summary>Closes the running phase (records its time) and starts the next; null only closes.</summary>
        void Phase(string next)
        {
            if (phaseName != null) Report.Timings.Add(phaseName + "=" + phaseClock.Elapsed.TotalSeconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
            phaseName = next;
            phaseClock.Restart();
        }

        // ------------------------------------------------------------------ collection

        void Collect(string[] roots)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in roots)
            {
                var r = root.Replace('\\', '/').TrimEnd('/');
                var abs = Absolute(r);
                IEnumerable<string> files;
                if (File.Exists(abs)) files = new[] { r };
                else if (Directory.Exists(abs)) files = Walk(abs).Select(f => r + f.Substring(abs.Length).Replace('\\', '/'));
                else continue;
                // A .unitypackage imported for analysis is scanned only as its own scope, never as part of the project.
                bool packageScope = PackageScopePaths.IsInside(r);
                foreach (var f in files)
                {
                    if (!packageScope && PackageScopePaths.IsInside(f)) continue;
                    if (KitPaths.IsKitFile(f) || KitPaths.IsWrittenByUrpify(f)) continue;
                    if (!seen.Add(f)) continue;
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    switch (ext)
                    {
                        case ".shader": shaders.Add(f); break;
                        case ".cs": scripts.Add(f); break;
                        case ".mat": materials.Add(f); break;
                        case ".unity": scenes.Add(f); break;
                        case ".prefab": prefabs.Add(f); break;
                        case ".anim": animations.Add(f); break;
                        default:
                            if (ModelExtensions.Contains(ext)) models.Add(f);
                            break;
                    }
                }
            }
            foreach (var l in new[] { shaders, scripts, materials, scenes, prefabs, models, animations }) l.Sort(StringComparer.Ordinal);
            Report.FilesScanned = shaders.Count + scripts.Count + materials.Count + scenes.Count + prefabs.Count + models.Count + animations.Count;
        }

        /// <summary>Walks a folder the way Unity imports it: skips hidden entries and folders ending with '~'.</summary>
        static IEnumerable<string> Walk(string dir)
        {
            var stack = new Stack<string>();
            stack.Push(dir);
            while (stack.Count > 0)
            {
                var d = stack.Pop();
                string[] files, dirs;
                try { files = Directory.GetFiles(d); dirs = Directory.GetDirectories(d); }
                catch { continue; }
                foreach (var f in files)
                {
                    var name = Path.GetFileName(f);
                    if (name.StartsWith(".", StringComparison.Ordinal) || name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                    yield return f;
                }
                foreach (var sd in dirs)
                {
                    var name = Path.GetFileName(sd);
                    if (name.StartsWith(".", StringComparison.Ordinal) || name.EndsWith("~", StringComparison.Ordinal) || name == "cvs") continue;
                    stack.Push(sd);
                }
            }
        }

        string Absolute(string assetPath)
        {
            if (assetPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                var p = PackageAbsolutePath(assetPath);
                if (p != null) return p;
            }
            return projectRoot + "/" + assetPath;
        }

        static string PackageAbsolutePath(string packagePath)
        {
            var parts = packagePath.Split('/');
            if (parts.Length < 2) return null;
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(parts[0] + "/" + parts[1]);
            if (info == null) return null;
            return info.resolvedPath.Replace('\\', '/') + "/" + string.Join("/", parts.Skip(2));
        }

        void AddUse(string asset, string user)
        {
            if (!usedBy.TryGetValue(asset, out var set)) usedBy[asset] = set = new HashSet<string>(StringComparer.Ordinal);
            set.Add(user);
        }

        void Add(string asset, IEnumerable<Finding> findings)
        {
            if (!findingsByAsset.TryGetValue(asset, out var list)) findingsByAsset[asset] = list = new List<Finding>();
            list.AddRange(findings);
        }

        /// <summary>
        /// Keeps the scan read-only. Loading an asset written by an older Unity upgrades it in memory and marks it dirty,
        /// and the Editor then saves it on exit (KinoGlitch's Unity 5 materials were rewritten this way by M1's scan).
        /// Objects that were not loaded before the scan cannot hold user edits, so their dirty flag is cleared again;
        /// objects the user already had loaded and dirty are left alone.
        /// </summary>
        static void ReadOnly(string path, Action body)
        {
            bool wasLoaded = AssetDatabase.IsMainAssetAtPathLoaded(path);
            HashSet<UnityEngine.Object> dirtyBefore = null;
            if (wasLoaded)
                dirtyBefore = new HashSet<UnityEngine.Object>(AssetDatabase.LoadAllAssetsAtPath(path).Where(o => o != null && EditorUtility.IsDirty(o)));
            try { body(); }
            finally
            {
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o != null && EditorUtility.IsDirty(o) && (dirtyBefore == null || !dirtyBefore.Contains(o)))
                        EditorUtility.ClearDirty(o);
            }
        }

        /// <summary>Runs one asset's analysis; an unexpected failure becomes a PARSE.FAILED finding instead of stopping the scan.</summary>
        void Guard(string path, AssetKind kind, Action body)
        {
            try { body(); }
            catch (Exception e)
            {
                var sink = new FindingSink(path, kind);
                sink.Add(RuleIds.ParseFailed, Severity.Info, FixTier.NotSupportedYet, 0, e.GetType().Name + ": " + e.Message, e.Message);
                Add(path, sink.Findings);
                Debug.LogWarning($"[Urpify] {path}: {e}");
            }
        }

        // ------------------------------------------------------------------ shaders

        void ScanShader(string path)
        {
            var hash = cache.FileHash(path, Absolute);
            if (useCache && cache.TryGet("shader", path, hash, inc => cache.FileHash(inc, Absolute), out var cached, out var extra) && extra.Count >= 2)
            {
                evaluator.Fallbacks[extra[0]] = extra[1];
                Report.FilesFromCache++;
                RecordShader(path, cached);
                return;
            }
            var text = File.ReadAllText(Absolute(path));
            var doc = ShaderLabParser.Parse(new SourceText(text, path));
            var sink = new FindingSink(path, AssetKind.Shader);
            foreach (var err in doc.Errors)
                sink.Add(RuleIds.ParseFailed, Severity.Info, FixTier.NotSupportedYet, err.Line, doc.Source.LineText(err.Line), err.Message);
            if (doc.ShaderName.Length > 0 && doc.Fallback.Length > 0) evaluator.Fallbacks[doc.ShaderName] = doc.Fallback;

            var includes = new List<ResolvedInclude>();
            foreach (var prog in doc.Programs) includes.AddRange(resolver.ResolveAll(prog.Unit, path));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            var ctx = new ShaderContext
            {
                AssetPath = path,
                Doc = doc,
                Includes = includes,
                Compiled = shader != null ? ShaderUrpEvaluator.Read(shader) : null,
            };
            foreach (var (attr, rule) in RuleRegistry.ShaderRules)
            {
                sink.CurrentRule = attr.Id;
                try { rule.Check(ctx, sink); }
                catch (Exception e) { sink.Add(RuleIds.ParseFailed, Severity.Info, FixTier.NotSupportedYet, 0, attr.Id + ": " + e.Message, e.Message); }
            }
            // Broken before the migration (group A of "Manual"): Unity cannot compile it, or the file is not a shader at all
            // (empty, binary, cut off). Nothing about it is "Pro converts" or "not supported yet"; it is the project's to fix
            // first (QA-011: CyclicNoGuard, DeepInclude, MissingInclude, Truncated were Pink / Auto and counted as convertible).
            MarkBrokenBeforeMigration(sink.Findings, ctx.Compiled, doc);
            CatalogRecognition.Shader(text, path, sink);
            var localIncludes = IncludeResolver.Flatten(includes).Select(x => (x.include.Path, cache.FileHash(x.include.Path, Absolute))).ToList();
            cache.Put("shader", path, hash, localIncludes, sink.Findings, new List<string> { doc.ShaderName, doc.Fallback });
            RecordShader(path, sink.Findings);
        }

        /// <summary>
        /// A shader Unity cannot compile, or a file that is not a shader at all, is broken before the migration: its breaking
        /// findings are the project's to fix (Manual, group A), never "Pro converts" or "Not supported yet" (QA-011).
        /// </summary>
        public static void MarkBrokenBeforeMigration(List<Finding> findings, CompiledShaderInfo compiled, ShaderDocument doc)
        {
            if (compiled?.HasErrors != true && doc.ShaderName.Length > 0) return;
            foreach (var f in findings.Where(f => f.Severity > Severity.Info || f.RuleId == RuleIds.ParseFailed))
                f.Tier = FixTier.Manual;
        }

        void RecordShader(string path, List<Finding> findings)
        {
            Add(path, findings);
            shaderVerdicts[path] = (SeverityUtil.Worst(findings), MigrationReport.BreakingTier(findings));
        }

        // ------------------------------------------------------------------ materials

        void ScanMaterial(string path, Material m, string subAsset)
        {
            if (m == null || m.shader == null) return;
            var shaderPath = AssetDatabase.GetAssetPath(m.shader);
            var ctx = new MaterialContext
            {
                AssetPath = path,
                SubAssetName = subAsset,
                Material = m,
                ShaderName = m.shader.name,
                ShaderPath = shaderPath,
                Outcome = evaluator.Evaluate(m.shader),
            };
            if (shaderVerdicts.TryGetValue(shaderPath, out var v)) { ctx.ShaderSeverity = v.sev; ctx.ShaderTier = v.tier; }
            else if (!ctx.Outcome.BuiltIn) { ctx.ShaderSeverity = ctx.Outcome.Severity; ctx.ShaderTier = ctx.Outcome.Severity > Severity.Info ? FixTier.NotSupportedYet : FixTier.None; }
            if (!string.IsNullOrEmpty(shaderPath) && shaderPath.StartsWith("Assets/", StringComparison.Ordinal)) AddUse(shaderPath, path);
            var sink = new FindingSink(path, AssetKind.Material);
            foreach (var (attr, rule) in RuleRegistry.MaterialRules)
            {
                try { rule.Check(ctx, sink); }
                catch (Exception e) { sink.Add(RuleIds.ParseFailed, Severity.Info, FixTier.NotSupportedYet, 0, attr.Id + ": " + e.Message, e.Message); }
            }
            Add(path, sink.Findings);
        }

        // ------------------------------------------------------------------ scripts

        Dictionary<string, string> lightFeeds = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, Analysis.EffectReach> effectReach = new Dictionary<string, Analysis.EffectReach>(StringComparer.Ordinal);

        /// <summary>The part of the reach a script's findings depend on, for the cache key.</summary>
        static string ReachKey(Analysis.EffectReach r) => r == null ? "" : r.Blocker + "|" + string.Join(";", r.Helpers);

        void ScanScript(string path)
        {
            var hash = cache.FileHash(path, Absolute);
            lightFeeds.TryGetValue(path, out var fedBy);
            fedBy = fedBy ?? "";
            effectReach.TryGetValue(path, out var reach);
            var reachKey = ReachKey(reach);
            // The result depends on other files too (the light script feeding the effect, the scripts OnRenderImage calls): part of the cache key.
            if (useCache && cache.TryGet("script", path, hash, inc => inc == "lightfeeds" ? fedBy : inc == "reach" ? reachKey : "", out var cached, out _))
            {
                Report.FilesFromCache++;
                Add(path, cached);
                return;
            }
            var code = CSharpFile.Parse(File.ReadAllText(Absolute(path)), path);
            var ctx = new ScriptContext { AssetPath = path, Code = code, ShaderOutcome = evaluator.Evaluate, FedByLightBuffers = fedBy, Reach = reach };
            var sink = new FindingSink(path, AssetKind.Script);
            foreach (var (attr, rule) in RuleRegistry.ScriptRules)
            {
                try { rule.Check(ctx, sink); }
                catch (Exception e) { sink.Add(RuleIds.ParseFailed, Severity.Info, FixTier.NotSupportedYet, 0, attr.Id + ": " + e.Message, e.Message); }
            }
            if (fedBy.Length > 0)
            {
                // The same decision as Urpify converters: the effect is not converted, the frame is left alone (QA-003).
                foreach (var f in sink.Findings.Where(f => f.Severity > Severity.Info && (f.Tier == FixTier.Auto || f.Tier == FixTier.AutoApprox)))
                    f.Tier = FixTier.NotSupportedYet;
                sink.Add(RuleIds.EffectFedByLightBuffers, Severity.Silent, FixTier.NotSupportedYet, 0, fedBy, fedBy);
            }
            else if (reach != null && reach.Blocker.Length > 0 && !ScriptRuleUtil.IsEditorScript(ctx))
            {
                // The same decision as Urpify converters (ScriptBatchConverter.EffectLeftAsIs): the effect is left as it is (QA-025, QA-029).
                foreach (var f in sink.Findings.Where(f => f.Severity > Severity.Info && (f.Tier == FixTier.Auto || f.Tier == FixTier.AutoApprox)))
                    f.Tier = FixTier.NotSupportedYet;
                var line = code.FindMethod("OnRenderImage")?.Line ?? 0;
                sink.Add(reach.GBuffer ? RuleIds.EffectReadsGBuffer : RuleIds.EffectUnrecordable, Severity.Silent, FixTier.NotSupportedYet, line, reach.Blocker, reach.Blocker);
            }
            CatalogRecognition.Script(code.Source.Text, path, sink);
            cache.Put("script", path, hash, new[] { ("lightfeeds", fedBy), ("reach", reachKey) }, sink.Findings);
            Add(path, sink.Findings);
        }

        /// <summary>A prefab file with a Material object of its own (YAML class 21).</summary>
        bool HasEmbeddedMaterials(string path)
        {
            try
            {
                foreach (var line in File.ReadLines(Path.Combine(projectRoot, path)))
                    if (line.StartsWith("--- !u!21 ", StringComparison.Ordinal)) return true;
            }
            catch (Exception) { }
            return false;
        }

        // ------------------------------------------------------------------ scenes and prefabs

        void ScanScene(string path)
        {
            var kind = path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? AssetKind.Prefab : AssetKind.Scene;
            var hash = cache.FileHash(path, Absolute);
            List<string> scriptGuids;
            if (useCache && cache.TryGet("scene", path, hash, _ => "", out var cached, out var extra))
            {
                Report.FilesFromCache++;
                Add(path, cached);
                scriptGuids = extra;
            }
            else
            {
                var yaml = UnityYamlFile.Parse(File.ReadAllText(Absolute(path)));
                var ctx = new SceneContext
                {
                    AssetPath = path, IsPrefab = kind == AssetKind.Prefab, Yaml = yaml,
                    ScriptPathForGuid = AssetDatabase.GUIDToAssetPath, BuiltInMaterial = BuiltInMaterial, PackageMaterial = PackageMaterial,
                };
                var sink = new FindingSink(path, kind);
                foreach (var (attr, rule) in RuleRegistry.SceneRules)
                {
                    try { rule.Check(ctx, sink); }
                    catch (Exception e) { sink.Add(RuleIds.ParseFailed, Severity.Info, FixTier.NotSupportedYet, 0, attr.Id + ": " + e.Message, e.Message); }
                }
                scriptGuids = yaml.OfType("MonoBehaviour").Select(d => d.RefGuid("m_Script")).Where(g => g.Length > 0).Distinct().ToList();
                cache.Put("scene", path, hash, Array.Empty<(string, string)>(), sink.Findings, scriptGuids);
                Add(path, sink.Findings);
            }
            // Scripts on objects in this scene: more precise than asset dependencies.
            if (!sceneAssets.TryGetValue(path, out var assets)) sceneAssets[path] = assets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var g in scriptGuids)
            {
                var sp = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(sp)) continue;
                assets.Add(sp);
                AddUse(sp, path);
            }
        }

        Dictionary<(string, long), Material> builtInMaterials;

        /// <summary>Materials shipped inside the Editor, keyed the way scenes reference them (guid, fileID).</summary>
        (string name, ShaderUrpOutcome outcome)? BuiltInMaterial(string guid, long fileId)
        {
            if (builtInMaterials == null)
            {
                builtInMaterials = new Dictionary<(string, long), Material>();
                foreach (var res in new[] { "Resources/unity_builtin_extra", "Library/unity default resources" })
                {
                    UnityEngine.Object[] all;
                    try { all = AssetDatabase.LoadAllAssetsAtPath(res); }
                    catch { continue; }
                    foreach (var m in all.OfType<Material>())
                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out string g, out long id)) builtInMaterials[(g, id)] = m;
                }
            }
            if (!builtInMaterials.TryGetValue((guid, fileId), out var mat) || mat.shader == null) return null;
            return (mat.name, evaluator.Evaluate(mat.shader));
        }

        readonly Dictionary<string, (string name, string path, ShaderUrpOutcome outcome)?> packageMaterials =
            new Dictionary<string, (string, string, ShaderUrpOutcome)?>(StringComparer.Ordinal);

        /// <summary>A material file inside a package, by GUID: Unity's converter cannot change it (packages are read-only).</summary>
        (string name, string path, ShaderUrpOutcome outcome)? PackageMaterial(string guid)
        {
            if (packageMaterials.TryGetValue(guid, out var known)) return known;
            (string, string, ShaderUrpOutcome)? result = null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Packages/", StringComparison.Ordinal) && path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && mat.shader != null) result = (mat.name, path, evaluator.Evaluate(mat.shader));
            }
            packageMaterials[guid] = result;
            return result;
        }

        void ScanAnimation(string path)
        {
            var text = File.ReadAllText(Absolute(path));
            var ctx = new SceneContext { AssetPath = path, Yaml = UnityYamlFile.Parse(""), RawText = text };
            var sink = new FindingSink(path, AssetKind.Animation);
            foreach (var (attr, rule) in RuleRegistry.SceneRules)
            {
                try { rule.Check(ctx, sink); }
                catch (Exception e) { sink.Add(RuleIds.ParseFailed, Severity.Info, FixTier.NotSupportedYet, 0, attr.Id + ": " + e.Message, e.Message); }
            }
            Add(path, sink.Findings);
        }

        // ------------------------------------------------------------------ project

        void ScanProject(Dictionary<string, string> manifest)
        {
            var ctx = new ProjectContext
            {
                ProjectRoot = projectRoot,
                ManifestDependencies = manifest,
                UrpVersion = Report.UrpVersion,
                ScriptCompilationFailed = EditorUtility.scriptCompilationFailed,
                PostProcessingV2InUse = (Report.ScopePaths ?? Array.Empty<string>()).Any(r => r.Replace('\\', '/').TrimEnd('/') == "Assets")
                    ? findingsByAsset.Values.Any(l => l.Any(f => f.RuleId == RuleIds.PostProcessingV2)) : (bool?)null,
            };
            var sink = new FindingSink("ProjectSettings", AssetKind.Settings);
            foreach (var (attr, rule) in RuleRegistry.ProjectRules)
            {
                try { rule.Check(ctx, sink); }
                catch (Exception e) { sink.Add(RuleIds.ParseFailed, Severity.Info, FixTier.NotSupportedYet, 0, attr.Id + ": " + e.Message, e.Message); }
            }
            foreach (var f in sink.Findings)
            {
                // Project-level findings point at the file they are about.
                if (f.RuleId == RuleIds.UrpNotInstalled || f.RuleId == RuleIds.PostProcessingPackage || f.RuleId == RuleIds.PostProcessingPackageUnused) f.AssetPath = "Packages/manifest.json";
                if (f.RuleId == RuleIds.ScriptCompilationFailed) { f.AssetPath = "Assets"; f.Kind = AssetKind.Project; }
            }
            LibraryCopies(sink);
            foreach (var g in sink.Findings.GroupBy(f => f.AssetPath)) Add(g.Key, g);
        }

        /// <summary>
        /// Copies of Urpify's shader library (architect decision 1 after M5): the project's own one and the UrpCompat folders
        /// of imported packages. Different versions side by side are fine (each package includes its own), but worth knowing.
        /// </summary>
        void LibraryCopies(FindingSink sink)
        {
            var copies = new List<(string path, string version)>();
            try
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(projectRoot, "Assets"), "BirpCompat.hlsl", SearchOption.AllDirectories))
                {
                    var rel = "Assets" + file.Substring(Path.Combine(projectRoot, "Assets").Length).Replace('\\', '/');
                    if (KitPaths.IsKitFile(rel)) continue;
                    string first;
                    using (var r = new StreamReader(file)) first = r.ReadLine() ?? "";
                    var m = System.Text.RegularExpressions.Regex.Match(first, @"^// UrpCompat (\S+):");
                    copies.Add((rel, m.Success ? m.Groups[1].Value : KitLibrary.Version));
                }
            }
            catch (Exception) { return; }
            var versions = copies.Select(c => c.version).Distinct().OrderBy(v => v, StringComparer.Ordinal).ToList();
            if (versions.Count < 2) return;
            foreach (var (path, version) in copies)
            {
                var f = sink.Add(RuleIds.LibraryCopies, Severity.Info, FixTier.None, 1, version, copies.Count, string.Join(", ", versions), version);
                f.AssetPath = path;
                f.Kind = AssetKind.Shader;
            }
        }

        Dictionary<string, string> ReadManifest()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                var text = File.ReadAllText(Path.Combine(projectRoot, "Packages", "manifest.json"));
                var start = text.IndexOf("\"dependencies\"", StringComparison.Ordinal);
                if (start < 0) return d;
                var open = text.IndexOf('{', start);
                var close = text.IndexOf('}', open);
                foreach (Match m in Regex.Matches(text.Substring(open, close - open), "\"([^\"]+)\"\\s*:\\s*\"([^\"]*)\""))
                    d[m.Groups[1].Value] = m.Groups[2].Value;
            }
            catch (Exception e) { Debug.LogWarning("[Urpify] Cannot read Packages/manifest.json: " + e.Message); }
            return d;
        }

        // ------------------------------------------------------------------ report

        void Finish(bool cancelled, Stopwatch total)
        {
            Report.Cancelled = cancelled;
            foreach (var kv in findingsByAsset)
            {
                var users = usedBy.TryGetValue(kv.Key, out var u) ? u.OrderBy(x => x, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
                foreach (var f in kv.Value) { f.UsedBy = users; Report.Findings.Add(f); }
            }
            foreach (var scene in scenes)
            {
                var own = findingsByAsset.TryGetValue(scene, out var sf) ? sf : new List<Finding>();
                var related = new List<(string asset, Finding f)>();
                foreach (var f in own) related.Add((scene, f));
                if (sceneAssets.TryGetValue(scene, out var assets))
                    foreach (var a in assets)
                        if (findingsByAsset.TryGetValue(a, out var af))
                            foreach (var f in af) related.Add((a, f));
                var sev = related.Count == 0 ? Severity.Info : related.Max(r => r.f.Severity);
                var causes = related.Where(r => r.f.Severity == sev && sev > Severity.Info).ToList();
                Report.Scenes.Add(new SceneVerdict
                {
                    Path = scene,
                    Severity = sev,
                    Tier = MigrationReport.BreakingTier(causes.Select(c => c.f)),
                    Causes = causes.Select(c => c.asset).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                    Rules = causes.Select(c => c.f.RuleId).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                    Breaks = related.Where(r => r.f.Severity > Severity.Info)
                        .GroupBy(r => (r.f.Severity, r.f.RuleId)).OrderByDescending(g => g.Key.Severity).ThenBy(g => g.Key.RuleId, StringComparer.Ordinal)
                        .Select(g => g.Key.Severity + " " + g.Key.RuleId).ToArray(),
                });
            }
            Report.Findings = Report.Findings
                .OrderByDescending(f => f.Severity).ThenBy(f => f.Kind).ThenBy(f => f.AssetPath, StringComparer.Ordinal).ThenBy(f => f.Line).ToList();
            Report.Summarize();
            Report.ScanSeconds = Math.Round(total.Elapsed.TotalSeconds, 3);
            Status = cancelled ? "Cancelled" : "Done";
        }
    }
}
