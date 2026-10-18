using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
namespace Urpify.Pipeline.AfterUnity
{
    /// <summary>
    /// Bloom after Unity's Post-processing v2 converter (architect decision 5 after M6). Unity's converter copies each PPv2
    /// Bloom value together with its override flag (URP 17.5.0 PPv2/EffectConverters/PropertyConversions.cs:16, 24;
    /// BloomConverter.cs: diffusion x 0.05 -> scatter). A value the PPv2 profile does not override then falls back to URP's
    /// own default instead of PPv2's, and the defaults differ: PPv2 threshold 1 and diffusion 7 (scatter 0.35), URP
    /// threshold 0.9 and scatter 0.7 (URP Runtime/Overrides/Bloom.cs:176, 188). In ShaderTutorials 050 / 051 (diffusion not
    /// overridden) URP spread the glow twice as wide: 44.7 % of 051's pixels differed, 7.8 % after this rule (grid of URP
    /// intensity x scatter against the Built-in frames, Tools/m7_bloom_sweep.sh; Unity's 1:1 intensity was already the best).
    /// The rule writes threshold and scatter from PPv2's effective values into the URP profile Unity's converter wrote next to
    /// the PPv2 one ("X(URP).asset"), with their override flags on. Nothing else changes.
    /// </summary>
    public static class PPv2BloomCalibration
    {
        /// <summary>PPv2 Bloom defaults (com.unity.postprocessing Runtime/Effects/Bloom.cs).</summary>
        const float PPv2Threshold = 1f, PPv2Diffusion = 7f;
        /// <summary>Unity's converter's diffusion -> scatter factor.</summary>
        const float ScatterPerDiffusion = 0.05f;

        public static List<KitConverterItem> Run()
        {
            var items = new List<KitConverterItem>();
            foreach (var urpPath in AssetDatabase.FindAssets("t:VolumeProfile", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                         .Where(p => p.EndsWith("(URP).asset", StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal))
            {
                var ppv2Path = urpPath.Substring(0, urpPath.Length - "(URP).asset".Length) + ".asset";
                if (!File.Exists(ppv2Path)) continue;
                var ppv2 = ReadPPv2Bloom(File.ReadAllText(ppv2Path));
                if (ppv2 == null) continue;
                var bloom = AssetDatabase.LoadAllAssetsAtPath(urpPath).FirstOrDefault(o => o != null && o.GetType().Name == "Bloom" && o.GetType().Namespace == "UnityEngine.Rendering.Universal");
                if (bloom == null) continue;
                var so = new SerializedObject(bloom);
                var changes = new List<string>();
                var p = ppv2.Value;
                if (!p.thresholdSet && Set(so, "threshold", PPv2Threshold)) changes.Add($"threshold {Num(PPv2Threshold)} (PPv2's default; URP's is 0.9)");
                float scatter = Math.Min(1f, Math.Max(0f, (p.diffusionSet ? p.diffusion : PPv2Diffusion) * ScatterPerDiffusion));
                if (!p.diffusionSet && Set(so, "scatter", scatter)) changes.Add($"scatter {Num(scatter)} (PPv2's default diffusion {Num(PPv2Diffusion)} x {Num(ScatterPerDiffusion)}; URP's default is 0.7)");
                if (changes.Count == 0) continue;
                if (so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(bloom);
                var item = new KitConverterItem { Path = urpPath, Converter = "PPv2 Bloom", Outcome = "AutoApprox" };
                item.Notes.Add("Approx: Bloom values the PPv2 profile left at their defaults now match PPv2's defaults, not URP's: " + string.Join(", ", changes)
                               + ". URP's bloom filters differently, so many small, very bright sources still glow less than in Built-in.");
                items.Add(item);
            }
            if (items.Count > 0) AssetDatabase.SaveAssets();
            return items;
        }

        static string Num(float f) => f.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Sets a VolumeParameter's value with its override flag on; false when it already was so.</summary>
        static bool Set(SerializedObject so, string field, float value)
        {
            var v = so.FindProperty(field + ".m_Value");
            var o = so.FindProperty(field + ".m_OverrideState");
            if (v == null) return false;
            if (o != null && o.boolValue && Math.Abs(v.floatValue - value) < 1e-5f) return false;
            v.floatValue = value;
            if (o != null) o.boolValue = true;
            return true;
        }

        /// <summary>PPv2's Bloom: whether threshold and diffusion are overridden, and diffusion's value.</summary>
        public static (bool thresholdSet, bool diffusionSet, float diffusion)? ReadPPv2Bloom(string yaml)
        {
            var m = Regex.Match(yaml, @"m_Name: Bloom\s*\n(?<body>(?:(?!--- !u!).*\n?)*)");
            if (!m.Success) return null;
            var body = m.Groups["body"].Value;
            (bool set, float value) Param(string name)
            {
                var v = Regex.Match(body, @"\n  " + name + @":\s*\n\s*overrideState: (\d)\s*\n\s*value: ([-0-9.eE]+)");
                if (!v.Success) return (false, 0f);
                float.TryParse(v.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f);
                return (v.Groups[1].Value == "1", f);
            }
            var t = Param("threshold");
            var d = Param("diffusion");
            return (t.set, d.set, d.value);
        }
    }
}
