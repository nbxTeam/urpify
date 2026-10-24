using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Urpify.Verify
{
    /// <summary>
    /// Captures scenes in Play Mode (TZ section 10): runtime Start() code runs, so script-built rendering is in the frame.
    /// The job lives in a file and survives the domain reloads of entering and leaving Play Mode.
    /// Every scene gets its own Play Mode session and is the first scene loaded in it, as when the user presses Play in
    /// that scene: nothing carries over from the scene before (DontDestroyOnLoad objects, statics), and the time since
    /// Play Mode started equals the time since the scene loaded. The second matters because URP feeds shaders Time.time
    /// (ScriptableRenderer.SetupPerFrameShaderConstants): scenes loaded later in one session showed shader animation at
    /// another phase in URP than in Built-in (ShaderTutorials 037, 053, 048).
    /// Determinism: fixed Time.captureDeltaTime and Random seed from before the scene loads (CaptureHooks), fixed
    /// capture time, particle systems re-simulated with a fixed seed and time, DynamicGI.UpdateEnvironment, synchronous
    /// shader compilation and a warm-up render.
    /// Settings changed for the capture and the scenes the user had open are restored on success, error and cancel.
    /// </summary>
    [InitializeOnLoad]
    public static class CaptureRunner
    {
        const string JobPath = "Library/Urpify/capture-job.json";
        const int MaxFramesPerScene = 2000; // safety net for scenes that never finish loading

        enum Phase { Open, Entering, Wait, Capture, Leaving, Exiting, Done }

        [Serializable]
        sealed class Job
        {
            public string Label = "";
            public CaptureSettings Settings = new CaptureSettings();
            public string[] Scenes = Array.Empty<string>();
            public int Index;
            public Phase Phase;
            public string[] PrevScenes = Array.Empty<string>();
            public bool[] PrevLoaded = Array.Empty<bool>();
            public int PrevActive = -1;
            /// <summary>Found after an Editor restart: closed without touching the scenes of the session that found it.</summary>
            public bool Stale;
            /// <summary><see cref="EditorSession.Id"/> of the Editor that started the job.</summary>
            public string Session = "";
            public bool ExitEditorWhenDone;
            public bool CancelRequested;
            public bool PrevAllowAsync, PrevAsyncSetting;
            public float PrevCaptureDeltaTime;
            public CaptureManifest Manifest = new CaptureManifest();
            /// <summary>Console errors since the current scene was opened (kept across the Play Mode reloads).</summary>
            public List<string> PendingErrors = new List<string>();
        }

        const int MaxErrorsPerScene = 10;

        /// <summary>Collects console errors while a scene is open for capture: they are part of what the scene shows (QA-002).</summary>
        static void OnLog(string message, string stack, LogType type)
        {
            if (job == null || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            if (job.Phase != Phase.Entering && job.Phase != Phase.Wait && job.Phase != Phase.Capture) return;
            var line = (message ?? "").Split('\n')[0].Trim();
            if (line.Length > 300) line = line.Substring(0, 300) + "...";
            if (line.Length == 0) return;
            for (int i = 0; i < job.PendingErrors.Count; i++)
            {
                var e = job.PendingErrors[i];
                var m = System.Text.RegularExpressions.Regex.Match(e, @"^\(x(\d+)\) (.*)$");
                var text = m.Success ? m.Groups[2].Value : e;
                if (text != line) continue;
                job.PendingErrors[i] = $"(x{(m.Success ? int.Parse(m.Groups[1].Value) + 1 : 2)}) {line}";
                return;
            }
            if (job.PendingErrors.Count < MaxErrorsPerScene) job.PendingErrors.Add(line);
        }

        /// <summary>The message of a collected error without its repeat count.</summary>
        public static string ErrorText(string e)
        {
            var m = System.Text.RegularExpressions.Regex.Match(e ?? "", @"^\(x\d+\) (.*)$");
            return m.Success ? m.Groups[1].Value : e ?? "";
        }

        static Job job;
        static int enteringTicks, waitTicks;

        /// <summary>True while a capture job exists (including the reloads around Play Mode).</summary>
        public static bool IsRunning => job != null || File.Exists(JobPath);

        /// <summary>0..1 and a status line, for the window.</summary>
        public static float Progress => job == null || job.Scenes.Length == 0 ? 0 : Mathf.Clamp01((float)job.Index / job.Scenes.Length);
        public static string Status => job == null ? "" : job.Index < job.Scenes.Length ? job.Scenes[job.Index] : job.Phase.ToString();
        public static string CurrentLabel => job?.Label ?? "";

        /// <summary>Raised in Edit Mode when a job finishes (after the reload from Play Mode). Subscribe from [InitializeOnLoad] code.</summary>
        public static event Action<CaptureManifest> Finished;

        static CaptureRunner()
        {
            // Asset import workers are separate Unity processes that also run [InitializeOnLoad] code; the job is not theirs.
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            if (!File.Exists(JobPath)) return;
            try { job = JsonUtility.FromJson<Job>(File.ReadAllText(JobPath)); }
            catch { job = null; }
            if (job == null) { File.Delete(JobPath); return; }
            if (job.Session != EditorSession.Id)
            {
                // The job belongs to an Editor that is gone (closed, crashed or killed). Never resume it in this one: it
                // would run a long capture nobody asked for, and a command-line job would even quit this Editor.
                Debug.LogWarning($"[Urpify] Capture '{job.Label}' was started by another Editor ({job.Session}); closed as interrupted in this one ({EditorSession.Id}).");
                job.Manifest.Error = "Interrupted: the Editor was closed during the capture.";
                job.ExitEditorWhenDone = false;
                job.Stale = true;
                job.Phase = Phase.Exiting;
                Save();
            }
            else if (!EditorApplication.isPlayingOrWillChangePlaymode && (job.Phase == Phase.Wait || job.Phase == Phase.Capture))
            {
                // Play Mode ended while a scene was playing (reload): restore settings and close the job.
                job.Manifest.Error = "Interrupted: Play Mode ended during the capture.";
                job.Phase = Phase.Exiting;
            }
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        /// <summary>Scenes the settings select, project-relative.</summary>
        public static string[] ResolveScenes(CaptureSettings s)
        {
            switch (s.Scenes)
            {
                case SceneSelection.Selected:
                    return s.Selected.Where(p => !string.IsNullOrEmpty(p)).ToArray();
                case SceneSelection.All:
                    return AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                        .Where(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)).OrderBy(p => p, StringComparer.Ordinal).ToArray();
                default:
                    var build = EditorBuildSettings.scenes.Where(x => x.enabled && !string.IsNullOrEmpty(x.path)).Select(x => x.path).ToArray();
                    return build.Length > 0 ? build : ResolveScenes(new CaptureSettings { Scenes = SceneSelection.All });
            }
        }

        /// <summary>Starts a capture. Returns an error message, or null when the job started.</summary>
        public static string Start(string label, CaptureSettings settings, bool exitEditorWhenDone)
        {
            if (IsRunning) return "A capture is already running.";
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Exit Play Mode first.";
            if (EditorUtility.scriptCompilationFailed) return "Scripts have compile errors, so Play Mode cannot start. Fix them first.";
            var scenes = ResolveScenes(settings);
            if (scenes.Length == 0) return "No scenes to capture.";
            if (!string.IsNullOrEmpty(label) && label.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "The capture label contains invalid characters.";
            // Each scene is opened in turn; unsaved changes in the open scenes must not be lost silently.
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "Capture cancelled: the open scenes have unsaved changes.";
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var folder = CaptureManifest.Folder(label);
            if (Directory.Exists(folder))
                foreach (var f in Directory.GetFiles(folder)) File.Delete(f); // a capture replaces the previous one with the same label
            job = new Job
            {
                Label = label,
                Settings = settings,
                Scenes = scenes,
                ExitEditorWhenDone = exitEditorWhenDone,
                Session = EditorSession.Id,
                PrevAllowAsync = ShaderUtil.allowAsyncCompilation,
                PrevAsyncSetting = EditorSettings.asyncShaderCompilation,
                PrevCaptureDeltaTime = Time.captureDeltaTime,
                PrevScenes = setup.Select(x => x.path).ToArray(),
                PrevLoaded = setup.Select(x => x.isLoaded).ToArray(),
                PrevActive = Array.FindIndex(setup, x => x.isActive),
                Manifest = new CaptureManifest
                {
                    Label = label,
                    UnityVersion = Application.unityVersion,
                    Pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null ? "Built-in" : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().Name,
                    StartedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Settings = settings,
                },
            };
            SetSyncCompilation();
            SessionState.SetFloat(CaptureHooks.DeltaTimeKey, 1f / Mathf.Max(1, settings.Fps));
            SessionState.SetInt(CaptureHooks.SeedKey, settings.Seed);
            job.Phase = Phase.Open;
            Save();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return null;
        }

        public static void Cancel()
        {
            if (job == null) return;
            job.CancelRequested = true;
            Save();
        }

        static void SetSyncCompilation()
        {
            ShaderUtil.allowAsyncCompilation = false;
            if (EditorSettings.asyncShaderCompilation) EditorSettings.asyncShaderCompilation = false;
        }

        static void RestoreSettings()
        {
            ShaderUtil.allowAsyncCompilation = job.PrevAllowAsync;
            if (EditorSettings.asyncShaderCompilation != job.PrevAsyncSetting) EditorSettings.asyncShaderCompilation = job.PrevAsyncSetting;
            // Same value as before: do not let the Editor rewrite ProjectSettings/EditorSettings.asset for it.
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/EditorSettings.asset"))
                if (o != null) EditorUtility.ClearDirty(o);
            Time.captureDeltaTime = job.PrevCaptureDeltaTime;
            SessionState.EraseFloat(CaptureHooks.DeltaTimeKey);
            SessionState.EraseInt(CaptureHooks.SeedKey);
        }

        /// <summary>Reopens the scenes the user had open before the capture (untitled scenes cannot be reopened).</summary>
        static void RestoreScenes()
        {
            try
            {
                var setup = job.PrevScenes.Select((p, i) => new SceneSetup { path = p, isLoaded = job.PrevLoaded.Length > i && job.PrevLoaded[i], isActive = i == job.PrevActive })
                    .Where(x => !string.IsNullOrEmpty(x.path) && File.Exists(x.path)).ToArray();
                if (setup.Length == 0) { EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single); return; }
                if (!setup.Any(x => x.isActive)) setup[0].isActive = true;
                foreach (var x in setup) if (x.isActive) x.isLoaded = true;
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            catch (Exception e) { Debug.LogWarning("[Urpify] Could not reopen the scenes that were open before the capture: " + e.Message); }
        }

        static void Tick()
        {
            if (job == null) { EditorApplication.update -= Tick; return; }
            try
            {
                switch (job.Phase)
                {
                    case Phase.Open:
                        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                        if (job.CancelRequested) { job.Manifest.Cancelled = true; BeginExit(); return; }
                        if (job.Index >= job.Scenes.Length) { BeginExit(); return; }
                        job.PendingErrors.Clear();
                        EditorSceneManager.OpenScene(job.Scenes[job.Index], OpenSceneMode.Single);
                        job.Phase = Phase.Entering;
                        Save();
                        enteringTicks = 0;
                        EditorApplication.isPlaying = true;
                        return;
                    case Phase.Entering:
                        if (!EditorApplication.isPlaying)
                        {
                            // Entering Play Mode takes a few ticks; if it never starts (Editor closed meanwhile), give up.
                            if (!EditorApplication.isPlayingOrWillChangePlaymode && ++enteringTicks > 600) Fail("Play Mode did not start.");
                            return;
                        }
                        enteringTicks = 0;
                        waitTicks = 0;
                        SetSyncCompilation();
                        job.Phase = Phase.Wait;
                        break;
                    case Phase.Wait:
                        if (!EditorApplication.isPlaying) { Fail("Play Mode ended unexpectedly."); return; }
                        var active = SceneManager.GetActiveScene();
                        bool loaded = active.isLoaded && string.Equals(active.path, job.Scenes[job.Index], StringComparison.OrdinalIgnoreCase);
                        // The frame time is fixed from the first frame, so the scene's own clock says how long it ran.
                        float due = (float)job.Settings.Frames / Mathf.Max(1, job.Settings.Fps);
                        if (!loaded && ++waitTicks < MaxFramesPerScene) return;
                        if (loaded && Time.timeSinceLevelLoad + 1e-4f < due) return;
                        job.Phase = Phase.Capture;
                        break;
                    case Phase.Capture:
                        StepProgressHook?.Invoke($"{job.Label}: {job.Scenes[job.Index]}", job.Index, job.Scenes.Length);
                        CaptureScene();
                        job.Index++;
                        job.Manifest.Save();
                        job.Phase = Phase.Leaving;
                        Save();
                        EditorApplication.isPlaying = false;
                        return;
                    case Phase.Leaving:
                        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                        job.Phase = Phase.Open;
                        break;
                    case Phase.Exiting:
                        if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
                        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                        if (!job.Stale) RestoreScenes();
                        Finish();
                        return;
                }
                Save();
            }
            catch (Exception e)
            {
                Fail(e.GetType().Name + ": " + e.Message);
            }
        }

        static void BeginExit()
        {
            job.Phase = Phase.Exiting;
            Save();
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        static void Fail(string message)
        {
            Debug.LogError("[Urpify] Capture failed: " + message);
            job.Manifest.Error = message;
            BeginExit();
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            RestoreSettings();
            job.Manifest.FinishedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            job.Manifest.Save();
            var manifest = job.Manifest;
            bool exit = job.ExitEditorWhenDone;
            job = null;
            try { File.Delete(JobPath); } catch { }
            Debug.Log($"[Urpify] Capture '{manifest.Label}' finished: {manifest.Scenes.Count} scenes, {manifest.Scenes.Sum(s => s.Views.Count)} images" +
                      (manifest.Cancelled ? ", cancelled" : "") + (manifest.Error.Length > 0 ? ", error: " + manifest.Error : ""));
            try { Finished?.Invoke(manifest); }
            catch (Exception e) { Debug.LogException(e); }
            if (exit)
            {
                File.WriteAllText(Path.Combine(CaptureManifest.Folder(manifest.Label), "urpify-capture-result.txt"), manifest.Error.Length == 0 ? "0" : "1");
                EditorApplication.Exit(manifest.Error.Length == 0 ? 0 : 1);
            }
        }

        static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(JobPath));
            File.WriteAllText(JobPath, JsonUtility.ToJson(job));
        }

        // ------------------------------------------------------------------ one scene

        /// <summary>The migration step's progress report (Pipeline sets it; Verify does not reference Pipeline).</summary>
        public static Action<string, int, int> StepProgressHook;

        static void CaptureScene()
        {
            var path = job.Scenes[job.Index];
            var result = new CapturedScene { Scene = path };
            job.Manifest.Scenes.Add(result);
            try { CaptureViews(path, result); }
            finally
            {
                result.RenderErrors.AddRange(job.PendingErrors);
                job.PendingErrors.Clear();
            }
        }

        static void CaptureViews(string path, CapturedScene result)
        {
            var active = SceneManager.GetActiveScene();
            if (!string.Equals(active.path, path, StringComparison.OrdinalIgnoreCase))
            {
                result.Error = "Scene did not load (active scene: " + active.path + ").";
                return;
            }
            PrepareParticles();
            DynamicGI.UpdateEnvironment();

            var cam = MainCamera();
            if (cam == null) { result.Error = "No enabled camera renders to the screen."; return; }
            var views = new List<(string name, Vector3 pos, Quaternion rot, float fov, float far)> { ("Main Camera", cam.transform.position, cam.transform.rotation, cam.fieldOfView, cam.farClipPlane) };
            foreach (var vp in FindAll<MigrationViewpoint>()
                         .OrderBy(v => HierarchyPath(v.transform), StringComparer.Ordinal))
                views.Add((HierarchyPath(vp.transform), vp.transform.position, vp.transform.rotation, vp.fieldOfView > 0 ? vp.fieldOfView : cam.fieldOfView,
                    vp.farClipPlane > 0 ? vp.farClipPlane : cam.farClipPlane));

            var s = job.Settings;
            // Render the way the camera renders to the screen: with HDR into a float target, clamped only at the end.
            // An 8-bit target makes URP render in that format and clamp before blending (BiRP still renders through
            // an HDR buffer), which changed transparent highlights between the pipelines (lab S04, M3).
            bool hdr = cam.allowHDR;
            var rt = new RenderTexture(s.Width, s.Height, 24, hdr ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32) { hideFlags = HideFlags.DontSave, antiAliasing = 1 };
            var ldr = hdr ? new RenderTexture(s.Width, s.Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { hideFlags = HideFlags.DontSave } : rt;
            var tex = NewFrame(s.Width, s.Height);
            var origPos = cam.transform.position;
            var origRot = cam.transform.rotation;
            var origFov = cam.fieldOfView;
            var origFar = cam.farClipPlane;
            var origTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            try
            {
                foreach (var (name, pos, rot, fov, far) in views)
                {
                    cam.transform.SetPositionAndRotation(pos, rot);
                    cam.fieldOfView = fov;
                    cam.farClipPlane = far;
                    cam.targetTexture = rt;
                    cam.Render(); // warm-up: variants compiled synchronously, temporary buffers allocated
                    cam.Render();
                    if (hdr) Graphics.Blit(rt, ldr);
                    ReadFrame(ldr, tex);
                    RenderTexture.active = prevActive;
                    cam.targetTexture = origTarget;
                    var file = CaptureManifest.ImageName(path, name);
                    Directory.CreateDirectory(CaptureManifest.Folder(job.Label));
                    File.WriteAllBytes(Path.Combine(CaptureManifest.Folder(job.Label), file), tex.EncodeToPNG());
                    result.Views.Add(new CapturedView { Name = name, Image = file, PinkPct = ImageCompare.PinkPct(tex.GetPixels32()) });
                }
            }
            finally
            {
                cam.transform.SetPositionAndRotation(origPos, origRot);
                cam.fieldOfView = origFov;
                cam.farClipPlane = origFar;
                cam.targetTexture = origTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                if (hdr) { ldr.Release(); UnityEngine.Object.DestroyImmediate(ldr); }
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// A frame as the screen shows it: colour only, no alpha. A shader may write any alpha (the ASCII effect of the owner's
        /// project writes 0 into 92 % of the frame); the screen ignores it, and a PNG that kept it showed the "after" frame
        /// through the "before" one in the comparison (owner's check 19.10.2026, item 1). RGB24 has no alpha to keep.
        /// </summary>
        public static Texture2D NewFrame(int width, int height) =>
            new Texture2D(width, height, TextureFormat.RGB24, false) { hideFlags = HideFlags.DontSave };

        /// <summary>
        /// A captured frame for showing: opaque whatever the file holds. Captures made before frames were saved without alpha
        /// can be transparent where the shader wrote alpha 0; shown as they are, the frame under them shows through.
        /// Null when the file is missing or not an image.
        /// </summary>
        public static Texture2D LoadFrame(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            if (!t.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.DestroyImmediate(t); return null; }
            if (t.format == TextureFormat.RGB24) return t;
            var px = t.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i].a = 255;
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        /// <summary>Reads <paramref name="source"/> into <paramref name="frame"/> (made by <see cref="NewFrame"/>).</summary>
        public static void ReadFrame(RenderTexture source, Texture2D frame)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = source;
            frame.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0);
            frame.Apply();
            RenderTexture.active = prev;
        }

        /// <summary>Active objects of a type, in no particular order (the callers sort): Unity 6.6 deprecates the sort mode.</summary>
        static T[] FindAll<T>() where T : UnityEngine.Object =>
#if UNITY_6000_6_OR_NEWER
            UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude);
#else
            UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#endif

        /// <summary>MainCamera tag first, otherwise the highest depth; only enabled cameras that render to the screen.</summary>
        public static Camera MainCamera() =>
            FindAll<Camera>()
                .Where(c => c.enabled && c.gameObject.activeInHierarchy && c.targetTexture == null)
                .OrderByDescending(c => c.CompareTag("MainCamera")).ThenByDescending(c => c.depth).ThenBy(c => c.name, StringComparer.Ordinal)
                .FirstOrDefault();

        /// <summary>Particles restart from a fixed seed and are simulated for the same time in every capture.</summary>
        static void PrepareParticles()
        {
            var systems = FindAll<ParticleSystem>();
            float t = (float)job.Settings.Frames / Mathf.Max(1, job.Settings.Fps);
            foreach (var ps in systems)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.useAutoRandomSeed = false;
                ps.randomSeed = (uint)job.Settings.Seed;
            }
            foreach (var ps in systems)
            {
                ps.Simulate(t, false, true, true);
                ps.Pause(false);
            }
        }

        static string HierarchyPath(Transform t)
        {
            var parts = new List<string>();
            for (var x = t; x != null; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
