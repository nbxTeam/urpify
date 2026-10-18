using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Urpify.Verify
{
    public enum SceneSelection { BuildSettings, All, Selected }

    [Serializable]
    public sealed class CaptureSettings
    {
        public int Width = 960;
        public int Height = 540;
        /// <summary>Frames the scene runs before the capture, at <see cref="Fps"/> fixed steps.</summary>
        public int Frames = 30;
        public int Fps = 30;
        /// <summary>Seed for UnityEngine.Random and particle systems, set before each scene loads.</summary>
        public int Seed = 12345;
        public SceneSelection Scenes = SceneSelection.BuildSettings;
        /// <summary>For <see cref="SceneSelection.Selected"/>: scene paths.</summary>
        public string[] Selected = Array.Empty<string>();
    }

    [Serializable]
    public sealed class CapturedView
    {
        /// <summary>"Main Camera" or the viewpoint's hierarchy path.</summary>
        public string Name = "";
        /// <summary>File name inside the capture folder.</summary>
        public string Image = "";
        public double PinkPct;
    }

    [Serializable]
    public sealed class CapturedScene
    {
        public string Scene = "";
        public List<CapturedView> Views = new List<CapturedView>();
        public string Error = "";
        /// <summary>
        /// Errors and exceptions in the console while the scene played and was captured, first line each, with a count when
        /// repeated. Compare counts the ones the other capture did not have against the scene (QA-002).
        /// </summary>
        public List<string> RenderErrors = new List<string>();
    }

    /// <summary>captures.json inside Urpify/Captures/&lt;label&gt;/.</summary>
    [Serializable]
    public sealed class CaptureManifest
    {
        public string Label = "";
        public string UnityVersion = "";
        public string Pipeline = "";
        public string StartedUtc = "", FinishedUtc = "";
        public CaptureSettings Settings = new CaptureSettings();
        public List<CapturedScene> Scenes = new List<CapturedScene>();
        public bool Cancelled;
        public string Error = "";

        public const string Root = "Urpify/Captures";
        public static string Folder(string label) => Root + "/" + label;
        public static string PathFor(string label) => Folder(label) + "/captures.json";

        public static CaptureManifest Load(string label)
        {
            var p = PathFor(label);
            return File.Exists(p) ? JsonUtility.FromJson<CaptureManifest>(File.ReadAllText(p)) : null;
        }

        public void Save()
        {
            Directory.CreateDirectory(Folder(Label));
            File.WriteAllText(PathFor(Label), JsonUtility.ToJson(this, true));
        }

        /// <summary>A file-system safe name for a scene and view.</summary>
        public static string ImageName(string scene, string view)
        {
            var s = scene.Replace("Assets/", "").Replace(".unity", "").Replace('/', '_').Replace('\\', '_');
            var v = view.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
            foreach (var c in Path.GetInvalidFileNameChars()) { s = s.Replace(c, '_'); v = v.Replace(c, '_'); }
            return s + "__" + v + ".png";
        }
    }
}
