using System;
using System.IO;
using System.Linq;
using Urpify.Generation;
using Urpify.Parsing;
using Urpify.Pipeline;

namespace Urpify.UI
{
    /// <summary>
    /// What the shader panel shows: the original file, the converter's result and the user's edits of it. The Code
    /// and Preview tabs share one model, so an edited and compiled result is what the preview renders.
    /// </summary>
    public sealed class ShaderPanelModel
    {
        public readonly string Path;
        /// <summary>The file is an asset of this project (Assets/ or Packages/), not a file opened from disk.</summary>
        public readonly bool InProject;
        public readonly string OriginalText;
        /// <summary>How the file is written (UTF-8, with a mark, or a code page kept byte for byte): what is saved keeps it (R-03).</summary>
        public readonly SourceFiles.Kind OriginalKind;
        public readonly ShaderDocument Doc;
        public readonly bool ProInstalled;
        /// <summary>The Pro converter that handles this shader; null when none does or Pro is not installed.</summary>
        public readonly IShaderFileConverter Converter;
        public readonly ShaderConversion Conversion;
        /// <summary>The Output the result is written for (the project setting, TZ 4.3).</summary>
        public readonly ShaderOutput Output;
        public string ConvertedText { get; private set; } = "";
        /// <summary>Raised when the converted text is edited and compiled.</summary>
        public event Action Changed;

        public ShaderPanelModel(string path) : this(path, KitProjectSettings.instance.Output) { }

        public ShaderPanelModel(string path, ShaderOutput output)
        {
            Path = path;
            Output = output;
            InProject = IsProjectPath(path);
            OriginalText = SourceFiles.Read(path, out OriginalKind);
            Doc = ShaderLabParser.Parse(OriginalText, path);
            // A file the migration already converted: its Built-in original is in the earliest backup that changed it. The Code
            // tab shows it next to the file, the Preview tab renders it (design review 13.10.2026, item 3).
            if (InProject && UrpOnly)
            {
                try
                {
                    var copy = new BackupStore(Directory.GetCurrentDirectory()).OriginalCopy(path, out var id);
                    if (copy != null) { BackupOriginalText = File.ReadAllText(copy); BackupId = id; }
                }
                catch (Exception) { BackupOriginalText = null; BackupId = null; }
            }
            var converters = MigrationRunner.ShaderConverters();
            ProInstalled = converters.Count > 0;
            Converter = converters.FirstOrDefault(c => c.Handles(Doc));
            if (Converter == null) return;
            ConversionOptions.FromProjectSettings();
            try { Conversion = Converter.Convert(OriginalText, path, output); }
            catch (Exception e) { Conversion = new ShaderConversion(); Conversion.Add(NoteKind.NotSupported, 0, Loc.Get("ui.shader.converterError", e.Message)); }
            ConvertedText = Conversion.Converted ? Conversion.Output : "";
        }

        public bool HasResult => ConvertedText.Length > 0;

        /// <summary>Every SubShader of the file is for URP: the file in the project is already the converted one.</summary>
        public bool UrpOnly => Doc.SubShaders.Count > 0
            && Doc.SubShaders.All(s => string.Equals(s.RenderPipeline, "UniversalPipeline", StringComparison.OrdinalIgnoreCase));

        /// <summary>The Built-in original of a file the migration converted, from its backup; null otherwise.</summary>
        public readonly string BackupOriginalText;
        /// <summary>The backup <see cref="BackupOriginalText"/> came from.</summary>
        public readonly string BackupId;

        /// <summary>From the scan: false when URP draws the shader as it is, null when the scan does not know the file.</summary>
        public bool? BreaksInUrp;

        /// <summary>
        /// The converter stopped: its partial result, read-only, marked where it stopped. It is saved only by an
        /// explicit Save as, for finishing by hand (architect decision 2 after M3); Convert never writes it.
        /// </summary>
        public bool HasDraft => !HasResult && Conversion != null && !string.IsNullOrEmpty(Conversion.Draft);
        public string DraftText => Conversion?.Draft ?? "";

        /// <summary>What Save as writes: the result, or the draft.</summary>
        public string SaveText => HasResult ? ConvertedText : DraftText;
        public bool Edited => Conversion != null && Conversion.Converted && ConvertedText != Conversion.Output;

        public void SetConverted(string text)
        {
            ConvertedText = text ?? "";
            Changed?.Invoke();
        }

        /// <summary>
        /// The converted text for an in-memory preview shader: until the project has its own copy of Urpify's shader
        /// library, includes point at the package's.
        /// </summary>
        public string PreviewText
        {
            get
            {
                var text = ConvertedText;
                if (Converter == null || text.Length == 0) return text;
                foreach (var f in Converter.RuntimeFiles())
                    if (!File.Exists(f.Value)) text = text.Replace(f.Value, f.Key);
                return text;
            }
        }

        public static bool IsProjectPath(string path) =>
            path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal);

        /// <summary>A dropped or picked path as the panel uses it: project-relative when the file is inside the project.</summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            path = path.Replace('\\', '/');
            if (!System.IO.Path.IsPathRooted(path)) return path;
            var root = System.IO.Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
            return path.StartsWith(root + "Assets/", StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : path;
        }

        public static bool IsShaderFile(string path) => !string.IsNullOrEmpty(path) && path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase);
    }
}
