using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Urpify.Pipeline
{
    [Serializable]
    public sealed class ConverterItem
    {
        public string Name = "";
        /// <summary>Success, Warning, Error (URP 17) or Pass, Fail (URP 14), normalized to Success / Warning / Error.</summary>
        public string Status = "";
        public string Message = "";
    }

    [Serializable]
    public sealed class ConverterRunResult
    {
        public bool Ran;
        public string Api = "";
        public string Error = "";
        public List<ConverterItem> Items = new List<ConverterItem>();
        public int Success => Items.Count(i => i.Status == "Success");
        public int Warnings => Items.Count(i => i.Status == "Warning");
        public int Errors => Items.Count(i => i.Status == "Error");
        /// <summary>Items Unity's converter could not upgrade ("No upgrader found").</summary>
        public int NoUpgrader => Items.Count(i => i.Message.IndexOf("No upgrader", StringComparison.OrdinalIgnoreCase) >= 0);
        public List<string> Log = new List<string>();
        /// <summary>What Urpify did differently from a plain run, and why (shown with the step result).</summary>
        public List<string> Notes = new List<string>();
    }

    /// <summary>
    /// Runs Unity's Render Pipeline Converter (Built-in to URP container) in the current Editor, avoiding the measured bugs
    /// (TZ 8.2, 6000.5.9f1 / URP 17.5.0):
    /// 1. URP 17: Converters.RunInBatchMode(container) passes a null type filter to FilterConverters, which throws
    ///    NullReferenceException (Editor/Converter/Converters.cs, convertedTypesFilter.Contains). Urpify always passes an
    ///    empty exclusive list, which means "every converter".
    /// 2. The batch command line crashes while Unity exits (codes 21, 139) after the work is done. Urpify never uses the
    ///    command line entry point; success is decided from the converter's own log and a rescan, not from an exit code.
    /// URP 14 has another API: Converters.RunInBatchMode(ConverterContainerId) and a "Name: / Converter Status:" log.
    /// 3. URP 14.0.12 with Post-processing v2 installed: PPv2Converter.OnInitialize enumerates a search over the
    ///    "URPConverterIndex" index, which only the Render Pipeline Converter window builds; in RunInBatchMode it spins
    ///    forever (ShaderTutorials on 2022.3.62f3, M3). The public filter cannot exclude it: ConverterId.PPv2 maps to
    ///    a type only under PPV2_EXISTS, which is defined in the PPv2 converter's own assembly, not in the one holding
    ///    Converters, so the mapping yields nothing. The internal RunInBatchMode(Type container, List&lt;Type&gt;
    ///    converters, ConverterFilter) takes types, but its Exclusive branch removes items from the list it is iterating
    ///    and throws InvalidOperationException. Urpify uses its Inclusive branch with every converter type except
    ///    PPv2Converter (URP keeps the ones that belong to the container), and says so.
    /// 4. URP 17.5.0 with Post-processing v2 installed: PPv2Converter is an AssetsConverter that finds its objects with
    ///    Unity Search, asynchronously (SearchServiceUtils.RunQueuedSearch); Converters.BatchConverters calls
    ///    Scan(callback) and moves on, so a batch run converts no PPv2 object and the scenes lose their post-processing
    ///    (ShaderTutorials 050/051 lost bloom, M6: the scan answered 24 s later with 7 objects). In an open Editor the
    ///    late answer would convert the scenes after the step, outside its backup. <see cref="RunAsync"/> therefore runs
    ///    the container without PPv2 and then PPv2Converter itself, waiting for its scan.
    /// 5. URP 14.0.12: PPv2Converter.OnInitialize only collects GlobalObjectIds from that search, which does not return
    ///    in batch mode even after the index the converter window builds exists (SearchService.CreateIndex
    ///    "URPConverterIndex", measured on ShaderTutorials, M6: the step hung). <see cref="RunAsync"/> finds the PPv2
    ///    objects itself and runs PPv2Converter on them.
    /// Everything goes through reflection so Urpify compiles without URP.
    /// </summary>
    public static class UnityConverterRunner
    {
        public const string Container = "BuiltInToURP";
        const string ConvertersType = "UnityEditor.Rendering.Universal.Converters";
        const string EditorAssembly = "Unity.RenderPipelines.Universal.Editor";

        public static Type FindConvertersType() =>
            AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == EditorAssembly)
                .Select(a => a.GetType(ConvertersType, false)).FirstOrDefault(t => t != null);

        public const string PPv2Skipped = "Post-processing v2 converter skipped: on URP 14 it waits forever outside the Render Pipeline " +
            "Converter window. Convert PPv2 volumes from Window > Rendering > Render Pipeline Converter.";

        public static ConverterRunResult Run() => Run(FindConvertersType());

        /// <summary>Marker for <see cref="Run(Type, Type)"/>: Post-processing v2 is not installed.</summary>
        public sealed class NoPPv2 { }

        /// <summary>
        /// Runs the converter found on <paramref name="convertersType"/> (a parameter so tests can pass a stand-in).
        /// <paramref name="ppv2Converter"/>: URP 14's PPv2Converter type when it is compiled; omitted = look for it in the
        /// loaded assemblies, <see cref="NoPPv2"/> = there is none.
        /// </summary>
        public static ConverterRunResult Run(Type convertersType, Type ppv2Converter = null)
        {
            var result = new ConverterRunResult();
            if (convertersType == null)
            {
                result.Error = "URP's Render Pipeline Converter is not loaded. Install URP first.";
                return result;
            }
            // PPv2Converter is compiled into its own assembly (Editor/Converter/PPv2/PPv2ToURPConvertersDefinition.asmdef),
            // not into the URP editor assembly that holds Converters, and only when Post-processing v2 is installed.
            if (ppv2Converter == null)
                ppv2Converter = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("UnityEditor.Rendering.Universal.PPv2Converter", false)).FirstOrDefault(x => x != null);
            if (ppv2Converter == typeof(NoPPv2)) ppv2Converter = null;
            var (invoke, api, note) = Resolve(convertersType, ppv2Converter);
            if (note != null) result.Notes.Add(note);
            if (invoke == null)
            {
                result.Error = "This URP version has no supported converter API (" + convertersType.Assembly.GetName().Version + ").";
                return result;
            }
            result.Api = api;
            var lines = new List<string>();
            void OnLog(string message, string stack, LogType type) => lines.Add(message);
            Application.logMessageReceived += OnLog;
            try
            {
                invoke();
                result.Ran = true;
            }
            catch (TargetInvocationException e) when (e.InnerException is MissingMethodException && BatchWithoutAbstract(convertersType, ppv2Converter) is Action batch)
            {
                // Item 6 below: nothing was converted yet (the exception comes from listing the converters).
                result.Notes.Add(AbstractConverterNote + " (" + e.InnerException.Message + ")");
                result.Api += " -> BatchConverters, abstract converter types skipped";
                try { batch(); result.Ran = true; }
                catch (TargetInvocationException e2) { result.Error = (e2.InnerException ?? e2).GetType().Name + ": " + (e2.InnerException ?? e2).Message; }
                catch (Exception e2) { result.Error = e2.GetType().Name + ": " + e2.Message; }
            }
            catch (TargetInvocationException e)
            {
                result.Error = (e.InnerException ?? e).GetType().Name + ": " + (e.InnerException ?? e).Message;
            }
            catch (Exception e)
            {
                result.Error = e.GetType().Name + ": " + e.Message;
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
            result.Log = lines;
            result.Items = ParseLog(lines);
            return result;
        }

        public const string AbstractConverterNote = "Unity's batch converter could not list its converters on this URP version: it creates every " +
            "RenderPipelineConverter type, the abstract Base2DMaterialUpgrader included. Urpify listed the Built-in to URP converters itself and ran them the same way.";

        /// <summary>
        /// Item 6 (URP 17.3.0, Unity 6000.3.26f1, measured 13.10.2026 on the lab): Converters.GetConvertersInContainer calls
        /// Activator.CreateInstance on every type derived from RenderPipelineConverter, the abstract Base2DMaterialUpgrader
        /// included, so every RunInBatchMode overload throws MissingMethodException before it converts anything. This lists the
        /// container's converters the same way without abstract types (and without PPv2Converter and the read-only material
        /// converter when Post-processing v2 is installed, as <see cref="Resolve"/> does) and hands them to the internal
        /// Converters.BatchConverters, which is what RunInBatchMode calls next. Null when this URP has no such method.
        /// </summary>
        static Action BatchWithoutAbstract(Type t, Type ppv2Converter)
        {
            const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var baseType = t.Assembly.GetType("UnityEditor.Rendering.Universal.RenderPipelineConverter", false);
            var containerType = t.Assembly.GetType("UnityEditor.Rendering.Universal.BuiltInToURPConverterContainer", false);
            if (baseType == null || containerType == null) return null;
            var listType = typeof(List<>).MakeGenericType(baseType);
            var batch = t.GetMethod("BatchConverters", Any, null, new[] { listType }, null);
            var containerProp = baseType.GetProperty("container", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (batch == null || containerProp == null) return null;
            return () =>
            {
                var list = (System.Collections.IList)Activator.CreateInstance(listType);
                foreach (var type in UnityEditor.TypeCache.GetTypesDerivedFrom(baseType))
                {
                    if (type.IsAbstract || type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null) == null) continue;
                    if (ppv2Converter != null && (type == ppv2Converter || type.Name == ReadonlyConverterName)) continue;
                    var converter = Activator.CreateInstance(type, true);
                    if (containerProp.GetValue(converter) as Type == containerType) list.Add(converter);
                }
                batch.Invoke(null, new object[] { list });
            };
        }

        /// <summary>Picks the API: URP 17 (string container, List&lt;string&gt;, bool) or URP 14 (ConverterContainerId enum).</summary>
        static (Action invoke, string api, string note) Resolve(Type t, Type ppv2Converter)
        {
            const BindingFlags Pub = BindingFlags.Public | BindingFlags.Static;
            const BindingFlags Internal = BindingFlags.NonPublic | BindingFlags.Static;
            var m17 = t.GetMethod("RunInBatchMode", Pub, null, new[] { typeof(string), typeof(List<string>), typeof(bool) }, null);
            if (m17 != null)
                return (() => m17.Invoke(null, new object[] { Container, new List<string>(), false }), "RunInBatchMode(string, List<string> empty, exclusive)", null);
            var enumType = t.Assembly.GetType("UnityEditor.Rendering.Universal.ConverterContainerId", false);
            if (enumType != null)
            {
                var filterType = t.Assembly.GetType("UnityEditor.Rendering.Universal.ConverterFilter", false);
                var containerType = t.Assembly.GetType("UnityEditor.Rendering.Universal.BuiltInToURPConverterContainer", false);
                var baseType = t.Assembly.GetType("UnityEditor.Rendering.Universal.RenderPipelineConverter", false);
                if (ppv2Converter != null && filterType != null && containerType != null && baseType != null)
                {
                    var byType = t.GetMethod("RunInBatchMode", Internal, null, new[] { typeof(Type), typeof(List<Type>), filterType }, null);
                    if (byType != null)
                    {
                        // The read-only material converter searches too, and the batch run does not wait for it: Urpify runs it
                        // object by object after the batch (RunReadonlyUrp14), as it does PPv2 (architect decision 3 after M6).
                        var others = UnityEditor.TypeCache.GetTypesDerivedFrom(baseType).Where(x => !x.IsAbstract && x != ppv2Converter && x.Name != ReadonlyConverterName).ToList();
                        return (() => byType.Invoke(null, new[] { containerType, others, Enum.Parse(filterType, "Inclusive") }),
                            "RunInBatchMode(BuiltInToURPConverterContainer, every converter but PPv2Converter, Inclusive)", PPv2Skipped);
                    }
                }
                var m14 = t.GetMethod("RunInBatchMode", Pub, null, new[] { enumType }, null);
                if (m14 != null)
                    return (() => m14.Invoke(null, new[] { Enum.Parse(enumType, Container) }), "RunInBatchMode(ConverterContainerId)", null);
            }
            return (null, "", null);
        }

        // ------------------------------------------------------------------ Post-processing v2 (items 4 and 5)

        /// <summary>Seconds to wait for Unity Search (the index on URP 14, the scan on URP 17) before giving up on PPv2.</summary>
        public static double PPv2TimeoutSeconds = 900;


        static Type FindPPv2Converter() => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UnityEditor.Rendering.Universal.PPv2Converter", false)).FirstOrDefault(x => x != null);

        /// <summary>
        /// Runs Unity's converter like <see cref="Run()"/>, and converts Post-processing v2 objects too, which needs Unity
        /// Search and so cannot finish inside one call. <paramref name="done"/> is called once, from an Editor update.
        /// </summary>
        public static void RunAsync(Action<ConverterRunResult> done)
        {
            var convertersType = FindConvertersType();
            if (convertersType == null) { done(Run(null)); return; }
            var m17 = convertersType.GetMethod("RunInBatchMode", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(List<string>), typeof(bool) }, null);
            if (m17 != null) { RunUrp17(convertersType, m17, done); return; }
            var ppv2 = FindPPv2Converter();
            if (ppv2 == null) { done(Run(convertersType, typeof(NoPPv2))); return; }
            RunUrp14(convertersType, ppv2, done);
        }

        /// <summary>
        /// URP 17: the container without its converters that find their objects with Unity Search (AssetsConverter:
        /// Post-processing v2 and read-only materials, the materials inside imported models), then each of those with its
        /// scan waited for. A late answer from a scan the batch run did not wait for also crashed the Editor while it quit
        /// (Unity Search stack in the crash reports of the M2-M6 command-line runs).
        /// </summary>
        static void RunUrp17(Type convertersType, MethodInfo m17, Action<ConverterRunResult> done)
        {
            var setup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
            var finish = done;
            done = r => { RestoreScenes(setup); finish(r); };
            var waited = SearchingConverters(convertersType);
            var names = waited.Select(w => w.name).ToList();
            var result = Capture(() => m17.Invoke(null, new object[] { Container, names, false }),
                "RunInBatchMode(string, [" + string.Join(", ", names) + "], exclusive) + their scans waited");
            if (!result.Ran) { done(result); return; }
            int next = 0;
            void RunNext()
            {
                if (next >= waited.Count) { done(result); return; }
                var (name, type) = waited[next++];
                RunWaited(type, name, result, RunNext);
            }
            RunNext();
        }

        /// <summary>Converters of the container that derive from AssetsConverter (their Scan answers later), with their batch names.</summary>
        static List<(string name, Type type)> SearchingConverters(Type convertersType)
        {
            var list = new List<(string, Type)>();
            var attr = convertersType.Assembly.GetType("UnityEditor.Rendering.Universal.BatchModeConverterClassInfo", false);
            if (attr == null) return list;
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            string Read(object a, string member) => (attr.GetField(member, F)?.GetValue(a) ?? attr.GetProperty(member, F)?.GetValue(a))?.ToString() ?? "";
            foreach (var t in UnityEditor.TypeCache.GetTypesWithAttribute(attr))
            {
                var a = t.GetCustomAttributes(attr, false).FirstOrDefault();
                if (a == null || Read(a, "containerName") != Container) continue;
                bool searching = false;
                for (var b = t.BaseType; b != null; b = b.BaseType) if (b.Name == "AssetsConverter") searching = true;
                if (searching) list.Add((Read(a, "converterType"), t));
            }
            // Read-only materials first, Post-processing v2 last, as in Unity's own order.
            return list.OrderBy(x => x.Item1 == "PPv2" ? 1 : 0).ThenBy(x => x.Item1, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// A Unity Search failure that ends a search without an answer: the Project provider ran on a worker thread and threw
        /// ("GUIDToAssetPath_Internal can only be called from the main thread", then this message). Seen once on Portals,
        /// 6000.5.9f1, M7; the step then waited its whole timeout. The scan is started again instead.
        /// </summary>
        const string SearchFailure = "Failed to fetch Project provider items";
        const int ScanAttempts = 3;

        /// <summary>Runs one AssetsConverter: Scan with a callback, Editor updates until it answers, then Convert per item.</summary>
        static void RunWaited(Type type, string name, ConverterRunResult result, Action next)
        {
            MethodInfo scan = type.GetMethod("Scan", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (scan == null || scan.GetParameters().Length != 1) { result.Notes.Add($"{name}: not converted: {type.Name}.Scan was not found."); next(); return; }
            bool finished = false, searchFailed = false;
            int attempt = 0;
            var start = UnityEditor.EditorApplication.timeSinceStartup;
            void OnSearchLog(string message, string stack, LogType logType)
            {
                if ((logType == LogType.Exception || logType == LogType.Error) && message != null && message.Contains(SearchFailure)) searchFailed = true;
            }
            void Stop()
            {
                finished = true;
                Application.logMessageReceived -= OnSearchLog;
                UnityEditor.EditorApplication.update -= Wait;
            }
            // Each search gets its own converter: the one whose search answers converts its items.
            void OnItems(object converter, System.Collections.IList items)
            {
                if (finished) return;
                Stop();
                if (attempt > 1) result.Notes.Add($"{name}: Unity Search failed on a worker thread; the search was started again ({attempt} searches).");
                var lines = new List<string>();
                void OnLog(string message, string stack, LogType logType) => lines.Add(message);
                Application.logMessageReceived += OnLog;
                try
                {
                    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                    type.GetMethod("BeforeConvert", F)?.Invoke(converter, null);
                    var convert = type.GetMethods(F).First(m => m.Name == "Convert" && m.GetParameters().Length == 2);
                    foreach (var item in items)
                    {
                        // A converter that changes a scene without saving it (read-only materials) leaves it dirty, and
                        // then AssetDatabase.OpenAsset cannot open the next item's scene ("Failed to load ... Global ID",
                        // ShaderTutorials, M6). The converter window asks the user to save; the step saves.
                        SaveDirtyScenes();
                        var args = new[] { item, null };
                        var status = convert.Invoke(converter, args)?.ToString() ?? "Error";
                        var itemName = item.GetType().GetProperty("name", F)?.GetValue(item)?.ToString() ?? "?";
                        result.Items.Add(new ConverterItem { Name = itemName, Status = status == "Success" || status == "Warning" ? status : "Error", Message = args[1]?.ToString() ?? "" });
                    }
                    type.GetMethod("AfterConvert", F)?.Invoke(converter, null);
                    SaveDirtyScenes();
                    UnityEditor.AssetDatabase.SaveAssets();
                    result.Notes.Add($"{name}: {items.Count} objects, converted after its search finished (Unity's batch run does not wait for it).");
                }
                catch (Exception e) { result.Notes.Add($"{name}: conversion failed: " + ((e as TargetInvocationException)?.InnerException ?? e).Message); }
                finally { Application.logMessageReceived -= OnLog; }
                result.Log.AddRange(lines);
                next();
            }
            void Wait()
            {
                if (finished) { UnityEditor.EditorApplication.update -= Wait; return; }
                if (searchFailed && attempt < ScanAttempts) { searchFailed = false; StartScan(); return; }
                if (UnityEditor.EditorApplication.timeSinceStartup - start < PPv2TimeoutSeconds) return;
                Stop();
                result.Notes.Add($"{name}: not converted, Unity Search did not answer in time{(attempt > 1 ? $" ({attempt} searches, Unity Search failed on a worker thread)" : "")}. Run it from Window > Rendering > Render Pipeline Converter.");
                next();
            }
            void StartScan()
            {
                attempt++;
                try
                {
                    var converter = Activator.CreateInstance(type, true);
                    var cbType = scan.GetParameters()[0].ParameterType; // Action<List<IRenderPipelineConverterItem>>
                    var p = System.Linq.Expressions.Expression.Parameter(cbType.GetGenericArguments()[0], "items");
                    Action<System.Collections.IList> handler = items => OnItems(converter, items);
                    var call = System.Linq.Expressions.Expression.Invoke(System.Linq.Expressions.Expression.Constant(handler),
                        System.Linq.Expressions.Expression.Convert(p, typeof(System.Collections.IList)));
                    var callback = System.Linq.Expressions.Expression.Lambda(cbType, call, p).Compile();
                    scan.Invoke(converter, new object[] { callback });
                }
                catch (Exception e)
                {
                    if (finished) return;
                    Stop();
                    result.Notes.Add($"{name}: not converted: " + ((e as TargetInvocationException)?.InnerException ?? e).Message);
                    next();
                }
            }
            Application.logMessageReceived += OnSearchLog;
            UnityEditor.EditorApplication.update += Wait;
            StartScan();
        }

        /// <summary>
        /// URP 14: every converter but PPv2 as before (item 3), then PPv2Converter on the objects Urpify finds itself.
        /// Its OnInitialize only fills the list of GlobalObjectIds it later converts, from a search that never returns in
        /// batch mode even after URPConverterIndex is built (measured on ShaderTutorials, 2022.3.62f3, M6). Urpify fills
        /// that list and calls OnPreRun, OnRun for each object and OnPostRun, as Converters.BatchConverters does.
        /// </summary>
        static void RunUrp14(Type convertersType, Type ppv2, Action<ConverterRunResult> done)
        {
            var result = Run(convertersType, ppv2);
            if (!result.Ran) { done(result); return; }
            result.Notes.Remove(PPv2Skipped);
            RunReadonlyUrp14(convertersType, result);
            var setup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
            try
            {
                const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var ids = PostProcessingV2Objects();
                if (ids.Count == 0) { done(result); return; }
                var converter = Activator.CreateInstance(ppv2, true);
                var guids = (List<string>)ppv2.GetField("guids", F).GetValue(converter);
                guids.AddRange(ids.Select(x => x.id));
                ppv2.GetField("effectConverters", F)?.SetValue(converter, null);
                ppv2.GetField("postConversionDestroyables", F)?.SetValue(converter, new List<UnityEngine.Object>());
                var asm = convertersType.Assembly; // the item types live in the URP editor assembly, not in PPv2URPConverters
                var itemInfoType = asm.GetType("UnityEditor.Rendering.Universal.ConverterItemInfo", true);
                var descriptorType = asm.GetType("UnityEditor.Rendering.Universal.ConverterItemDescriptor", true);
                var contextType = asm.GetType("UnityEditor.Rendering.Universal.RunItemContext", true);
                ppv2.GetMethod("OnPreRun", F).Invoke(converter, null);
                var onRun = ppv2.GetMethod("OnRun", F);
                for (int i = 0; i < ids.Count; i++)
                {
                    var descriptor = Activator.CreateInstance(descriptorType);
                    descriptorType.GetField("name", F)?.SetValue(descriptor, ids[i].name);
                    var info = Activator.CreateInstance(itemInfoType);
                    // Both are structs with internal setters: set on the boxed copies that are passed on.
                    itemInfoType.GetProperty("index", F).SetValue(info, i);
                    itemInfoType.GetProperty("descriptor", F).SetValue(info, descriptor);
                    var ctx = Activator.CreateInstance(contextType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { info }, null);
                    contextType.GetProperty("isBatchMode", F)?.SetValue(ctx, true);
                    var args = new[] { ctx };
                    string status = "Success", message = "";
                    try
                    {
                        onRun.Invoke(converter, args);
                        if ((bool)(contextType.GetProperty("didFail", F)?.GetValue(args[0]) ?? false))
                        {
                            status = "Error";
                            message = contextType.GetProperty("info", F)?.GetValue(args[0])?.ToString() ?? "";
                        }
                    }
                    catch (TargetInvocationException e) { status = "Error"; message = (e.InnerException ?? e).Message; }
                    result.Items.Add(new ConverterItem { Name = ids[i].name, Status = status, Message = message });
                }
                ppv2.GetMethod("OnPostRun", F).Invoke(converter, null);
                UnityEditor.AssetDatabase.SaveAssets();
                result.Notes.Add(PPv2Direct);
            }
            catch (Exception e)
            {
                result.Notes.Add("Post-processing v2 was not converted: " + ((e as TargetInvocationException)?.InnerException ?? e).Message + " " + PPv2Manual);
            }
            finally { RestoreScenes(setup); }
            done(result);
        }

        public const string PPv2Direct = "Post-processing v2: converted object by object with the objects Urpify found (Unity's batch run cannot search for them).";
        const string PPv2Manual = "Convert PPv2 volumes from Window > Rendering > Render Pipeline Converter.";

        /// <summary>
        /// GlobalObjectIds of what PPv2Converter converts, in its order: PostProcessVolume and PostProcessLayer components
        /// in scenes and prefabs under Assets (found by their script's GUID in the file), then PostProcessProfile assets.
        /// </summary>
        const string ReadonlyConverterName = "ReadonlyMaterialConverter";
        public const string ReadonlyDirect = "Read-only materials: converted object by object with the objects Urpify found (on URP 14 Unity's batch run cannot search for them).";

        /// <summary>
        /// URP 14: Unity's read-only material converter (scene and prefab references to the Editor's built-in materials,
        /// Default-Material and the like) finds its objects with a search over URPConverterIndex in OnInitialize, which a
        /// batch run never waits for, so it converted nothing there (M6). Urpify finds the objects itself (renderers and
        /// terrains whose materials are the built-in ones Unity's map names), fills the converter's list of GlobalObjectIds
        /// and calls OnRun for each, scene by scene, as the converter window does (architect decision 3 after M6).
        /// </summary>
        static void RunReadonlyUrp14(Type convertersType, ConverterRunResult result)
        {
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var asm = convertersType.Assembly;
            var type = asm.GetType("UnityEditor.Rendering.Universal." + ReadonlyConverterName, false);
            var map = asm.GetType("UnityEditor.Rendering.Universal.ReadonlyMaterialMap", false)?.GetField("Map", F)?.GetValue(null) as Dictionary<string, string>;
            if (type == null || map == null) { result.Notes.Add("Read-only materials: Unity's converter for them was not found in this URP version."); return; }
            var setup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var objects = ReadonlyMaterialObjects(map);
                if (objects.Count == 0) return;
                var converter = Activator.CreateInstance(type, true);
                var guids = (List<string>)type.GetField("guids", F).GetValue(converter);
                guids.AddRange(objects.Select(o => o.id));
                var itemInfoType = asm.GetType("UnityEditor.Rendering.Universal.ConverterItemInfo", true);
                var descriptorType = asm.GetType("UnityEditor.Rendering.Universal.ConverterItemDescriptor", true);
                var contextType = asm.GetType("UnityEditor.Rendering.Universal.RunItemContext", true);
                type.GetMethod("OnPreRun", F)?.Invoke(converter, null);
                var onRun = type.GetMethod("OnRun", F);
                string openScene = null;
                for (int i = 0; i < objects.Count; i++)
                {
                    // The converter opens a scene object's scene itself; Urpify opens it first and saves the one before,
                    // so a dirty scene never blocks the next (M6).
                    if (objects[i].scene != null && objects[i].scene != openScene)
                    {
                        SaveDirtyScenes();
                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(objects[i].scene, UnityEditor.SceneManagement.OpenSceneMode.Single);
                        openScene = objects[i].scene;
                    }
                    var descriptor = Activator.CreateInstance(descriptorType);
                    descriptorType.GetField("name", F)?.SetValue(descriptor, objects[i].name);
                    var info = Activator.CreateInstance(itemInfoType);
                    itemInfoType.GetProperty("index", F).SetValue(info, i);
                    itemInfoType.GetProperty("descriptor", F).SetValue(info, descriptor);
                    var ctx = Activator.CreateInstance(contextType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { info }, null);
                    contextType.GetProperty("isBatchMode", F)?.SetValue(ctx, true);
                    var args = new[] { ctx };
                    string status = "Success", message = "";
                    try
                    {
                        onRun.Invoke(converter, args);
                        if ((bool)(contextType.GetProperty("didFail", F)?.GetValue(args[0]) ?? false))
                        {
                            status = "Error";
                            message = contextType.GetProperty("info", F)?.GetValue(args[0])?.ToString() ?? "";
                        }
                    }
                    catch (TargetInvocationException e) { status = "Error"; message = (e.InnerException ?? e).Message; }
                    result.Items.Add(new ConverterItem { Name = objects[i].name, Status = status, Message = message });
                }
                SaveDirtyScenes();
                type.GetMethod("OnPostRun", F)?.Invoke(converter, null);
                UnityEditor.AssetDatabase.SaveAssets();
                result.Notes.Add(ReadonlyDirect);
            }
            catch (Exception e)
            {
                result.Notes.Add("Read-only materials were not converted: " + ((e as TargetInvocationException)?.InnerException ?? e).Message);
            }
            finally { RestoreScenes(setup); }
        }

        /// <summary>Renderers and terrains in the project's scenes and prefabs that use the Editor's built-in materials Unity's map names.</summary>
        static List<(string id, string name, string scene)> ReadonlyMaterialObjects(Dictionary<string, string> map)
        {
            var list = new List<(string, string, string)>();
            bool BuiltIn(Material m) => m != null && map.ContainsKey(m.name) && UnityEditor.AssetDatabase.GetAssetPath(m).StartsWith("Resources/unity_builtin_extra", StringComparison.Ordinal);
            bool Uses(Component c) => c is Renderer r ? r.sharedMaterials.Any(BuiltIn) : c is Terrain t && BuiltIn(t.materialTemplate);
            string Id(UnityEngine.Object o) => UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();
            foreach (var path in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(UnityEditor.AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal))
                foreach (var c in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path).OfType<Component>().Where(c => c is Renderer || c is Terrain).Where(Uses))
                    list.Add((Id(c), c.gameObject.name, null));
            foreach (var path in UnityEditor.AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(UnityEditor.AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal))
            {
                if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) continue;
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var c in root.GetComponentsInChildren<Component>(true).Where(c => c is Renderer || c is Terrain).Where(Uses))
                        list.Add((Id(c), c.gameObject.name, path));
            }
            return list;
        }

        static List<(string id, string name)> PostProcessingV2Objects()
        {
            var list = new List<(string, string)>();
            var types = new[] { "PostProcessVolume", "PostProcessLayer" }
                .Select(n => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEngine.Rendering.PostProcessing." + n, false)).FirstOrDefault(t => t != null))
                .Where(t => t != null).ToList();
            var profileType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEngine.Rendering.PostProcessing.PostProcessProfile", false)).FirstOrDefault(t => t != null);
            var scriptGuids = UnityEditor.MonoImporter.GetAllRuntimeMonoScripts().Where(s => s != null && types.Contains(s.GetClass()))
                .Select(s => UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(s))).Where(g => g.Length > 0).ToList();
            if (scriptGuids.Count > 0)
            {
                bool Uses(string path)
                {
                    try { var text = System.IO.File.ReadAllText(path); return scriptGuids.Any(g => text.IndexOf(g, StringComparison.Ordinal) >= 0); }
                    catch { return false; }
                }
                foreach (var path in UnityEditor.AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(UnityEditor.AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal))
                {
                    if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || !Uses(path)) continue;
                    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var t in types)
                            foreach (var c in root.GetComponentsInChildren(t, true))
                                list.Add((UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(c).ToString(), System.IO.Path.GetFileNameWithoutExtension(path)));
                }
                foreach (var path in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(UnityEditor.AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal))
                {
                    if (!Uses(path)) continue;
                    var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;
                    foreach (var t in types)
                        foreach (var c in go.GetComponentsInChildren(t, true))
                            list.Add((UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(c).ToString(), System.IO.Path.GetFileNameWithoutExtension(path)));
                }
            }
            if (profileType != null)
                foreach (var path in UnityEditor.AssetDatabase.FindAssets("t:" + profileType.Name, new[] { "Assets" }).Select(UnityEditor.AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal))
                {
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath(path, profileType);
                    if (asset != null) list.Add((UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString(), System.IO.Path.GetFileNameWithoutExtension(path)));
                }
            return list;
        }

        /// <summary>Saves the open scenes a converter changed (untitled scenes are left alone).</summary>
        static void SaveDirtyScenes()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path)) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }
        }

        static void RestoreScenes(UnityEditor.SceneManagement.SceneSetup[] setup)
        {
            try
            {
                var keep = setup.Where(s => !string.IsNullOrEmpty(s.path) && System.IO.File.Exists(s.path)).ToArray();
                if (keep.Length == 0) return;
                if (!keep.Any(s => s.isActive)) keep[0].isActive = true;
                foreach (var s in keep) if (s.isActive) s.isLoaded = true;
                UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(keep);
            }
            catch (Exception e) { Debug.LogWarning("[Urpify] Could not reopen the scenes open before the converter: " + e.Message); }
        }

        /// <summary>Runs <paramref name="invoke"/> with the converter's log captured and parsed.</summary>
        static ConverterRunResult Capture(Action invoke, string api)
        {
            var result = new ConverterRunResult { Api = api };
            var lines = new List<string>();
            void OnLog(string message, string stack, LogType type) => lines.Add(message);
            Application.logMessageReceived += OnLog;
            try { invoke(); result.Ran = true; }
            catch (TargetInvocationException e) { result.Error = (e.InnerException ?? e).GetType().Name + ": " + (e.InnerException ?? e).Message; }
            catch (Exception e) { result.Error = e.GetType().Name + ": " + e.Message; }
            finally { Application.logMessageReceived -= OnLog; }
            result.Log = lines;
            result.Items = ParseLog(lines);
            return result;
        }

        static readonly Regex Line17 = new Regex(@"^- (?<name>.+?) \((?<status>Success|Warning|Error)\)(?: \((?<msg>.*)\))?$", RegexOptions.Compiled);
        static readonly Regex Name14 = new Regex(@"^Name: (?<name>.+)$", RegexOptions.Compiled | RegexOptions.Multiline);
        static readonly Regex Status14 = new Regex(@"^Converter Status: (?<status>Pass|Fail)$", RegexOptions.Compiled | RegexOptions.Multiline);
        static readonly Regex Info14 = new Regex(@"^Info: (?<info>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>Items from both log formats: URP 17 "- Item (Status) (message)" lines, URP 14 "Name: / Converter Status: / Info:".</summary>
        public static List<ConverterItem> ParseLog(IEnumerable<string> messages)
        {
            var items = new List<ConverterItem>();
            foreach (var msg in messages)
            {
                if (string.IsNullOrEmpty(msg)) continue;
                var n14 = Name14.Match(msg);
                var s14 = Status14.Match(msg);
                if (n14.Success && s14.Success)
                {
                    var i14 = Info14.Match(msg);
                    items.Add(new ConverterItem
                    {
                        Name = n14.Groups["name"].Value.Trim(),
                        Status = s14.Groups["status"].Value == "Pass" ? "Success" : "Error",
                        Message = i14.Success ? i14.Groups["info"].Value.Trim() : "",
                    });
                    continue;
                }
                foreach (var raw in msg.Split('\n'))
                {
                    var m = Line17.Match(raw.TrimEnd('\r'));
                    if (!m.Success) continue;
                    items.Add(new ConverterItem { Name = m.Groups["name"].Value, Status = m.Groups["status"].Value, Message = m.Groups["msg"].Value });
                }
            }
            foreach (var i in items.Where(i => i.Status == "Error" && IsAlreadyDone(i.Message)))
            {
                i.Status = "Success";
                i.Message = "Already done by an earlier run: " + i.Message;
            }
            return items;
        }

        /// <summary>
        /// Errors that only say the work is already there: a step run again after an interruption finds the quality levels
        /// the first run assigned ("Quality Level 0 already references a Render Pipeline Asset: Very Low.", URP 17.5.0
        /// RenderSettingsConverter). The rerun is then not an error (QA-008).
        /// </summary>
        public static bool IsAlreadyDone(string message) =>
            message != null && message.IndexOf("already references a Render Pipeline Asset", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
