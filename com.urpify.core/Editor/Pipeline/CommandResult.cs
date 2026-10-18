using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Urpify.Pipeline
{
    /// <summary>
    /// What a command-line run ended with, as JSON next to the plain-text result file: written by every command, on success
    /// and on any error (QA-017: a wrong argument, a Verify without captures or a failed Package left only a text file).
    /// CI reads the exit code, the error, and where the reports are.
    /// </summary>
    [Serializable]
    public sealed class CommandResult
    {
        public const int SchemaVersion = 1;
        public int schemaVersion = SchemaVersion;
        public string command = "";
        /// <summary>The migration step, for Migrate.</summary>
        public string step = "";
        public int exitCode;
        /// <summary>Done, Failed, ... for Migrate; empty otherwise.</summary>
        public string status = "";
        public string error = "";
        public List<string> reports = new List<string>();
        public string finishedUtc = "";

        /// <summary>Writes &lt;dir&gt;/&lt;name&gt; (JSON); never throws.</summary>
        public void Write(string dir, string name)
        {
            try
            {
                finishedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, name), JsonUtility.ToJson(this, true));
            }
            catch (Exception e) { Debug.LogWarning("[Urpify] Cannot write " + name + ": " + e.Message); }
        }
    }
}
