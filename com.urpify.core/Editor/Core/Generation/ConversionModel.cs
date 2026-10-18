using System;
using System.Collections.Generic;
using System.Linq;

namespace Urpify.Generation
{
    public enum NoteKind
    {
        /// <summary>Nothing visible changes; worth knowing (e.g. not SRP Batcher compatible).</summary>
        Info,
        /// <summary>Converted, close to the original but not identical (TZ: AutoApprox).</summary>
        Approx,
        /// <summary>
        /// A person has to act: the project was broken before the migration, or the construct has no URP counterpart
        /// (decision before M8 No. 2). The note says what to do.
        /// </summary>
        Manual,
        /// <summary>Not converted yet (planned); the note says what is missing and what to do meanwhile.</summary>
        NotSupported,
    }

    [Serializable]
    public sealed class ConversionNote
    {
        public NoteKind Kind;
        /// <summary>1-based line in the original shader, 0 for the whole file.</summary>
        public int Line;
        public string Text = "";
        public override string ToString() => $"{Kind} {(Line > 0 ? "line " + Line + ": " : "")}{Text}";
    }

    /// <summary>A file a conversion writes besides the shader (a converted include file).</summary>
    [Serializable]
    public sealed class GeneratedFile
    {
        /// <summary>Path as the shader's own path is written (project-relative "Assets/..." or absolute).</summary>
        public string Path = "";
        public string Content = "";
        /// <summary>The project file this one is a converted copy of: the copy is written in its encoding (R-03). "" when none.</summary>
        public string Source = "";
    }

    /// <summary>
    /// Names of the files a conversion writes next to the author's (code review R-14): "Name_URP.ext", or "Name_URP2.ext" and
    /// on when a file of that name is already there and Urpify did not write it (a vendor's own URP include or shader, an
    /// earlier migration by hand). Such a file is never overwritten, so undoing the conversion never deletes it either.
    /// </summary>
    public static class CopyNames
    {
        public const string Suffix = "_URP";
        /// <summary>The first line of a converted include copy.</summary>
        public const string IncludeHeader = "// Converted for URP by Urpify from ";
        /// <summary>The first line of a shader written with Output = Write copy.</summary>
        public const string CopyHeader = "// Written for URP by Urpify (Output: Write copy) from ";

        /// <summary>True when the file's first line says Urpify wrote it (so a new conversion may write it again).</summary>
        public static bool WrittenByUrpify(string path)
        {
            try
            {
                using (var r = new System.IO.StreamReader(path))
                {
                    var first = r.ReadLine() ?? "";
                    return first.StartsWith(IncludeHeader, StringComparison.Ordinal) || first.StartsWith(CopyHeader, StringComparison.Ordinal);
                }
            }
            catch (Exception) { return false; }
        }

        /// <summary>"Folder/Name.ext" with <paramref name="suffix"/> before the extension.</summary>
        public static string With(string path, string suffix)
        {
            var slash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
            var dot = path.LastIndexOf('.');
            if (dot <= slash) return path + suffix;
            return path.Substring(0, dot) + suffix + path.Substring(dot);
        }

        /// <summary>
        /// The suffix for the copy of <paramref name="path"/>: "_URP", or "_URP2" and on, the first whose file does not exist
        /// or was written by Urpify.
        /// </summary>
        public static string FreeSuffix(string path)
        {
            for (int n = 1; n < 100; n++)
            {
                var suffix = n == 1 ? Suffix : Suffix + n;
                var candidate = With(path, suffix);
                if (!System.IO.File.Exists(candidate) || WrittenByUrpify(candidate)) return suffix;
            }
            return Suffix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }
    }

    /// <summary>URP Asset and renderer settings a converted shader relies on (TZ 6.5, 6.6).</summary>
    public static class UrpRequirement
    {
        /// <summary>Depth Texture on in every URP Asset.</summary>
        public const string DepthTexture = "DepthTexture";
        /// <summary>A transparent shader reads the depth texture: Copy Depth Mode After Opaques in every renderer.</summary>
        public const string DepthBeforeTransparents = "DepthBeforeTransparents";
        /// <summary>Opaque Texture on, at full resolution (GrabPass read the full-resolution screen).</summary>
        public const string OpaqueTexture = "OpaqueTexture";
        /// <summary>Passes 3 and later of a SubShader, tagged UrpifyPass3 and on, drawn by Urpify's renderer feature.</summary>
        public const string ExtraPasses = "ExtraPasses";
        /// <summary>GrabPass source After transparents: Urpify's renderer feature copies the screen and draws UrpifyGrabPass passes.</summary>
        public const string GrabAfterTransparents = "GrabAfterTransparents";
        /// <summary>Built-in's _CameraDepthNormalsTexture, written by Urpify's renderer feature (and Depth Texture on).</summary>
        public const string DepthNormalsTexture = "DepthNormalsTexture";
        /// <summary>Scenes or prefabs hold Projector components: Urpify's renderer feature draws them (TZ 6.10).</summary>
        public const string Projectors = "Projectors";
    }

    /// <summary>Settings the converters read (set by the migration step from the project settings or the command line).</summary>
    public static class ConversionOptions
    {
        public static GrabPassSource GrabPass = GrabPassSource.OpaqueTexture;
        /// <summary>Output = Keep Built-in support: what the converters write must keep rendering in Built-in as before.</summary>
        public static bool KeepBuiltIn;
        /// <summary>
        /// Settings of TZ 4.3 for converted surface shaders (Shadows) and every converted shader (SRP Batcher). Additional
        /// lights follow the URP Asset, as in URP's own shaders (architect decision 4 after M9a: no kit setting).
        /// </summary>
        public static ShadowsOption Shadows = ShadowsOption.SameAsOriginal;
        public static bool SrpBatcher = true;

        /// <summary>
        /// Whether a shader (by name) is a screen effect that scripts run with Graphics.Blit and a pass index, never drawn on
        /// objects, though its passes do not say so (no ZTest Always): a Hidden shader no material asset of the project
        /// uses. Its passes are not retagged (QA-002: Tonemapping, BlendModes got UrpifyPass3..16). Set by the step that knows
        /// the project's materials; false otherwise.
        /// </summary>
        public static Func<string, bool> ScreenEffectShader = name => false;

        /// <summary>The name rule of <see cref="ScreenEffectShader"/>, given the shader names the project's materials use.</summary>
        public static Func<string, bool> ScreenEffectsAmong(ICollection<string> materialShaders) =>
            name => name != null && name.StartsWith("Hidden/", StringComparison.Ordinal) && !materialShaders.Contains(name);

        /// <summary>The project's settings into the options (the Urpify converters step, the shader panel).</summary>
        public static void FromProjectSettings()
        {
            var s = KitProjectSettings.instance;
            GrabPass = s.GrabPass;
            Shadows = s.Shadows;
            SrpBatcher = s.SrpBatcher;
        }
    }

    [Serializable]
    public sealed class ShaderConversion
    {
        public bool Converted;
        public string Output = "";
        /// <summary>
        /// When the converter stopped: its partial result with a comment where it stopped, for the shader panel to show
        /// read-only and save only by an explicit Save as (architect decision 2 after M3). Never written by the migration.
        /// </summary>
        public string Draft = "";

        /// <summary>The comment a draft carries where the converter stopped.</summary>
        public const string DraftMarker = "// Urpify draft - ";
        /// <summary>Files written with the shader: converted copies of its project include files.</summary>
        public List<GeneratedFile> Files = new List<GeneratedFile>();
        /// <summary>URP settings the converted shader needs (UrpRequirement names), applied by the migration step.</summary>
        public List<string> Requires = new List<string>();
        public List<ConversionNote> Notes = new List<ConversionNote>();

        public FixTier Tier =>
            Notes.Any(n => n.Kind == NoteKind.Manual) ? FixTier.Manual
            : !Converted || Notes.Any(n => n.Kind == NoteKind.NotSupported) ? FixTier.NotSupportedYet
            : Notes.Any(n => n.Kind == NoteKind.Approx) ? FixTier.AutoApprox : FixTier.Auto;

        public void Require(string requirement) { if (!Requires.Contains(requirement)) Requires.Add(requirement); }

        public void Add(NoteKind kind, int line, string text)
        {
            if (!Notes.Any(n => n.Kind == kind && n.Line == line && n.Text == text)) Notes.Add(new ConversionNote { Kind = kind, Line = line, Text = text });
        }
    }
}
