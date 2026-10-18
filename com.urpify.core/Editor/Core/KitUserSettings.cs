using System;
using System.IO;
using UnityEngine;

namespace Urpify
{
    public enum BuiltInPreview
    {
        /// <summary>Render the original with the Built-in pipeline switched on for one call.</summary>
        Live,
        /// <summary>Show the before/after captures of scenes that use the shader instead.</summary>
        FromCaptures,
    }

    /// <summary>
    /// Personal settings (TZ 4.3: "личные в UserSettings/"), in UserSettings/Urpify.json of the project. Never
    /// in EditorPrefs: the owner's Editor settings are not touched (architect decision M1 item 2).
    /// </summary>
    [Serializable]
    public sealed class KitUserSettings
    {
        public const string FilePath = "UserSettings/Urpify.json";

        public BuiltInPreview BuiltInPreview = BuiltInPreview.Live;

        /// <summary>A small mark on assets in the Project window that the last scan found broken (TZ 4.2), on by default.</summary>
        public bool ProjectOverlays = true;

        /// <summary>The window's language: one of Loc.Languages, or "" for the Editor's language (English when the window does not have it).</summary>
        public string Language = "";

        static KitUserSettings current;

        public static KitUserSettings Current
        {
            get
            {
                if (current != null) return current;
                current = new KitUserSettings();
                try { if (File.Exists(FilePath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), current); }
                catch (Exception e) { Debug.LogWarning("[Urpify] " + FilePath + " could not be read, using defaults: " + e.Message); }
                return current;
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
        }
    }
}
