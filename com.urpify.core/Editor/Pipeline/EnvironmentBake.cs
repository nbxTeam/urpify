using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Urpify.Pipeline
{
    /// <summary>
    /// Bake environment (architect decision 2 after M8): on request from the scene panel (or the CLI), never as part of a
    /// step. A scene that takes ambient light from the skybox and has no baked lighting data renders darker in a URP player
    /// (rule SCENE.ENVIRONMENT_NOT_BAKED). This bakes the environment only: baked and realtime GI are off in a copy of the
    /// scene's lighting settings (with Auto Generate off), so no lightmaps change what the lights do; the ambient probe and the default reflection
    /// go into new lighting data of the scene. Unity bakes the scene's baked reflection probes in the same pass.
    /// The project is backed up first (BackupStore), and the bake is recorded in MigrationState.Extras, so the scene panel
    /// and Rollback can undo it. Its files are not recorded as a step window: a bake between steps is never folded into a
    /// step's backup, it is undone on its own before any step is rolled back.
    /// </summary>
    public static class EnvironmentBake
    {
        public const string Kind = "BakeEnvironment";

        /// <summary>Why the scene cannot be baked now, or null.</summary>
        public static string CannotBake(string scenePath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return Loc.Get("ui.bake.playMode");
            if (MigrationRunner.Busy) return Loc.Get("ui.bake.busy");
            if (Lightmapping.isRunning) return Loc.Get("ui.bake.lightmapping");
            // Not tied to URP (M9a): a Unity 6 Built-in player lacks the skybox's ambient light without baked data too.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) return Loc.Get("ui.bake.noScene", scenePath);
            return null;
        }

        /// <summary>The bake of this scene that can be rolled back, or null.</summary>
        public static ExtraRecord Done(string scenePath) =>
            MigrationState.Load().Extras.LastOrDefault(e => e.Kind == Kind && e.Target == scenePath && e.Status == StepStatus.Done);

        /// <summary>Bakes the environment of the scene. Returns null on success, or what went wrong.</summary>
        public static string Bake(string scenePath)
        {
            var why = CannotBake(scenePath);
            if (why != null) return why;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return Loc.Get("ui.bake.cancelled");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var st = MigrationState.Load();
            var rec = new ExtraRecord { Kind = Kind, Target = scenePath, Status = StepStatus.Running, StartedUtc = Now() };
            st.Extras.Add(rec);
            var store = new BackupStore(".");
            Snapshot snap = null;
            string error = null;
            try
            {
                snap = store.Begin(Kind);
                rec.SnapshotId = snap.Id;
                st.Save();
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                LightingSettings settings = Lightmapping.TryGetLightingSettings(out var current) && current != null
                    ? UnityEngine.Object.Instantiate(current) : new LightingSettings();
                settings.name = "Environment only";
                settings.bakedGI = false;
                settings.realtimeGI = false;
                // A script cannot bake while Auto Generate is on ("not available in iterative mode", 2022.3.62f3 with
                // Portals' settings, M9a); the baked data replaces what Auto Generate gave the Editor.
                // Obsolete in Unity 6.6 without a replacement; a bake from a script still refuses to start while it is on.
#pragma warning disable 618
                settings.autoGenerate = false;
#pragma warning restore 618
                Lightmapping.lightingSettings = settings;
                if (!Lightmapping.Bake()) error = Loc.Get("ui.bake.failed");
                else
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    AssetDatabase.SaveAssets();
                    var data = Lightmapping.lightingDataAsset != null ? AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset) : "";
                    if (data.Length == 0) error = Loc.Get("ui.bake.noData");
                    else rec.Message = Loc.Get("ui.bake.done", data);
                }
            }
            catch (NotEnoughDiskSpaceException e) { error = e.Message; }
            catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }
            finally
            {
                if (snap != null) store.Complete(snap);
                if (error != null && snap != null)
                {
                    // A failed bake leaves nothing behind.
                    store.Rollback(snap);
                    AssetDatabase.Refresh();
                }
                try { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
                catch (Exception e) { Debug.LogWarning("[Urpify] Could not reopen the scenes: " + e.Message); }
            }
            rec.Status = error == null ? StepStatus.Done : StepStatus.Failed;
            if (error != null) rec.Message = error;
            rec.FinishedUtc = Now();
            st.Save();
            Debug.Log($"[Urpify] Bake environment {scenePath}: {rec.Status} - {rec.Message}");
            return error;
        }

        /// <summary>Undoes one bake. Files changed after it are left alone and listed.</summary>
        public static RollbackResult Undo(ExtraRecord rec)
        {
            var store = new BackupStore(".");
            var snap = store.Load(rec.SnapshotId);
            if (snap == null || !snap.Complete) return new RollbackResult { Errors = { $"{rec.Kind} {rec.Target}: backup {rec.SnapshotId} is missing or incomplete" } };
            var r = store.Rollback(snap);
            rec.Status = StepStatus.RolledBack;
            rec.Message = $"Rolled back: {r.Restored.Count} restored, {r.Removed.Count} removed, {r.Skipped.Count} left as changed later.";
            rec.FinishedUtc = Now();
            return r;
        }

        /// <summary>Rolls back the latest bake of the scene and saves the state.</summary>
        public static RollbackResult Undo(string scenePath)
        {
            var st = MigrationState.Load();
            var rec = st.Extras.LastOrDefault(e => e.Kind == Kind && e.Target == scenePath && e.Status == StepStatus.Done);
            if (rec == null) return new RollbackResult { Errors = { "No bake to roll back for " + scenePath } };
            var r = Undo(rec);
            st.Save();
            AssetDatabase.Refresh();
            return r;
        }

        static string Now() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
}
