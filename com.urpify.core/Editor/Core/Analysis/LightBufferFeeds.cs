using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Urpify.Analysis
{
    /// <summary>
    /// Image effects whose data come from command buffers on lights (QA-003, VolumetricLights). Light.AddCommandBuffer does
    /// not run under URP and Urpify does not convert it yet (decision before M8, No. 2: may wait until after the
    /// release), so such an effect would draw from textures nobody fills: VolumetricLightRenderer multiplied the whole frame
    /// by an empty transmittance and every frame went black, alpha 0. An effect fed that way is not converted ("Not
    /// supported yet"): under URP the effect is missing and the rest of the frame is drawn. Shared by the scanner and Kit
    /// converters so both say the same.
    /// </summary>
    public static class LightBufferFeeds
    {
        static readonly Regex AddToLight = new Regex(@"\.AddCommandBuffer\s*\(\s*(UnityEngine\.Rendering\.)?LightEvent\.", RegexOptions.Compiled);
        static readonly Regex Effect = new Regex(@"\bvoid\s+OnRenderImage\s*\(|\.AddCommandBuffer\s*\(\s*(UnityEngine\.Rendering\.)?CameraEvent\.", RegexOptions.Compiled);

        /// <summary>
        /// For each effect script (OnRenderImage, or command buffers on cameras) tied to a script that adds command buffers
        /// to lights (one names the other's class): the effect's path -> the light script's file name.
        /// </summary>
        public static Dictionary<string, string> Find(IEnumerable<(string path, string text)> scripts)
        {
            var list = scripts.Where(s => s.text != null).ToList();
            var feeders = list.Where(s => AddToLight.IsMatch(Strip(s.text))).ToList();
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (feeders.Count == 0) return result;
            foreach (var s in list)
            {
                var code = Strip(s.text);
                if (!Effect.IsMatch(code)) continue;
                var name = Path.GetFileNameWithoutExtension(s.path);
                foreach (var f in feeders)
                {
                    if (f.path == s.path) continue;
                    var feeder = Path.GetFileNameWithoutExtension(f.path);
                    if (Names(Strip(f.text), name) || Names(code, feeder)) { result[s.path] = Path.GetFileName(f.path); break; }
                }
            }
            return result;
        }

        static bool Names(string text, string identifier) =>
            identifier.Length > 0 && Regex.IsMatch(text, @"\b" + Regex.Escape(identifier) + @"\b");

        /// <summary>The text without comments, so a commented-out line does not count.</summary>
        static string Strip(string text) => Regex.Replace(text, @"//[^\n]*|/\*.*?\*/", "", RegexOptions.Singleline);
    }
}
