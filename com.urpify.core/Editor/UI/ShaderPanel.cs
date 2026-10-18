using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Urpify.Analysis;
using Urpify.Generation;
using Urpify.Pipeline;

namespace Urpify.UI
{
    /// <summary>
    /// Right side for a shader (TZ 4.2): header, what the converter says, tabs Code / Preview / Usages, and the
    /// actions Convert (Pro, keeps a copy of the original), Save as and Open file. Opens shaders of the project and
    /// .shader files dragged in from disk.
    /// </summary>
    public sealed class ShaderPanel : VisualElement
    {
        static readonly string[] Tabs = { "Code", "Preview", "Usages" };
        static string openTab = "Code";

        readonly IWindowContext ctx;
        readonly ShaderPanelModel model;
        readonly VisualElement body = new VisualElement();
        readonly VisualElement tabBar = new VisualElement();

        public ShaderPanel(IWindowContext ctx, string path)
        {
            this.ctx = ctx;
            AddToClassList("urpify-shader-panel");
            try { model = new ShaderPanelModel(path); }
            catch (Exception e)
            {
                Add(UiKit.Header(Severity.Info, Path.GetFileName(path)));
                Add(UiKit.Note(Loc.Get("ui.shader.openFailed", e.Message)));
                return;
            }
            Build();
        }

        void Build()
        {
            var asset = ctx.Report?.Assets.FirstOrDefault(a => a.Path == model.Path);
            if (ctx.Report != null) model.BreaksInUrp = asset != null && asset.Severity > Severity.Info;
            string badge = null;
            if (model.Conversion != null) badge = Loc.Get("ui.details.fix", Loc.Get("tier." + model.Conversion.Tier));
            else if (asset != null && asset.Tier != FixTier.None) badge = Loc.Get("ui.details.fix", Loc.Get("tier." + asset.Tier));
            // In the free package a fix only Pro does says so here too, as the list's lock does (QA-021).
            if (badge != null && asset != null && ctx.Report != null && Edition.Locked(ctx.Report, asset)) badge += " · Pro";
            var header = UiKit.Header(asset?.Severity ?? Severity.Info, Path.GetFileName(model.Path), badge);
            // The license the file names (TZ M8 item 7): a tag next to the name, like a package tag; what it means for a
            // converted copy is in its tooltip. A non-commercial license is marked as a warning.
            var license = LicenseHeader.Find(model.OriginalText);
            if (license != null)
            {
                bool nc = LicenseHeader.NonCommercial(license);
                var tag = new Label(license) { tooltip = Loc.Get(nc ? "ui.shader.licenseNc" : "ui.shader.license", license) };
                tag.AddToClassList("urpify-license-tag");
                if (nc) tag.AddToClassList("urpify-license-nc");
                header.Insert(2, tag);
            }
            Add(header);
            Add(UiKit.Subtitle(Describe(asset)));
            Add(Actions());

            if (model.Conversion != null)
                foreach (var n in model.Conversion.Notes.Where(n => n.Kind != NoteKind.Info).Take(20))
                {
                    var l = new Label((n.Line > 0 ? Loc.Get("ui.shader.line", n.Line) + ": " : "") + n.Text);
                    l.AddToClassList(n.Kind == NoteKind.Manual || n.Kind == NoteKind.NotSupported ? "urpify-manual" : "urpify-text");
                    Add(l);
                }

            var findings = ctx.Report?.Findings.Where(f => f.AssetPath == model.Path).OrderByDescending(f => f.Severity).ThenBy(f => f.Line).ToList();
            if (findings != null && findings.Count > 0)
            {
                var fold = new Foldout { text = Loc.Get("ui.shader.findings", findings.Count), value = false };
                foreach (var f in findings) fold.Add(AssetPanel.FindingCard(f));
                Add(fold);
            }

            tabBar.AddToClassList("urpify-tabs");
            foreach (var t in Tabs)
            {
                var name = t;
                var b = new Button(() => { openTab = name; ShowTab(); }) { text = Loc.Get("ui.shader.tab." + name), name = "tab-" + name };
                b.AddToClassList("urpify-tab");
                tabBar.Add(b);
            }
            Add(tabBar);
            Add(body);
            ShowTab();
        }

        string Describe(AssetSummary asset)
        {
            var state = PackageScopePaths.IsInside(model.Path) ? PackageScope.Load() : null;
            var where = state != null ? Loc.Get("ui.shader.inPackage", PackageScope.ToPackagePaths(model.Path), Path.GetFileName(state.PackageFile))
                : model.InProject ? model.Path : Loc.Get("ui.shader.fromDisk", model.Path);
            var kind = model.Doc.SubShaders.Any(s => s.SurfaceProgram != null) ? Loc.Get("ui.shader.kind.surface")
                : model.Doc.HasUrpSubShader ? Loc.Get("ui.shader.kind.urp") : Loc.Get("ui.shader.kind.other");
            var used = asset != null && asset.UsedBy.Length > 0 ? " · " + Loc.Get("ui.shader.usedByCount", asset.UsedBy.Length) : "";
            return kind + " · " + where + used;
        }

        VisualElement Actions()
        {
            var buttons = new VisualElement();
            buttons.AddToClassList("urpify-buttons");
            var convert = new Button(Convert) { text = Loc.Get("ui.shader.convert") };
            // A package's shaders live in a temporary folder: they are converted by the package export, not in place.
            var inPackage = PackageScopePaths.IsInside(model.Path);
            var canConvert = model.InProject && model.Path.StartsWith("Assets/", StringComparison.Ordinal) && model.HasResult && !inPackage;
            convert.SetEnabled(canConvert);
            convert.tooltip = canConvert ? Loc.Get("ui.shader.convertTip")
                : !model.ProInstalled ? Loc.Get("ui.shader.proNeeded")
                : inPackage ? Loc.Get("ui.shader.convertPackage")
                : !model.HasResult ? Loc.Get("ui.shader.nothingToConvert")
                : Loc.Get("ui.shader.convertOnlyAssets");
            if (canConvert) convert.AddToClassList("urpify-primary");
            buttons.Add(convert);
            var saveAs = new Button(SaveAs) { text = Loc.Get("ui.shader.saveAs"), tooltip = Loc.Get(model.HasDraft ? "ui.shader.saveDraftTip" : "ui.shader.saveAsTip") };
            saveAs.SetEnabled(model.HasResult || model.HasDraft);
            buttons.Add(saveAs);
            buttons.Add(new Button(OpenFile) { text = Loc.Get("ui.shader.openFile"), tooltip = Loc.Get("ui.shader.openFileTip") });
            if (model.ProInstalled) buttons.Add(UiKit.OutputChooser(() => ctx.Select(model.Path)));
            return buttons;
        }

        void ShowTab()
        {
            foreach (var t in Tabs)
            {
                var b = tabBar.Q<Button>("tab-" + t);
                var on = t == openTab;
                // Selected: bold with an underline bar (urpify-tab-on), so it does not rely on colour.
                b.EnableInClassList("urpify-tab-on", on);
            }
            body.Clear();
            switch (openTab)
            {
                case "Preview": body.Add(new ShaderPreview(model, ctx)); break;
                case "Usages": Usages(); break;
                default: body.Add(new ShaderCodeView(model)); break;
            }
        }

        void Usages()
        {
            var asset = ctx.Report?.Assets.FirstOrDefault(a => a.Path == model.Path);
            if (!model.InProject) { body.Add(UiKit.Note(Loc.Get("ui.shader.usagesDisk"))); return; }
            if (asset == null) { body.Add(UiKit.Note(Loc.Get(ctx.Report == null ? "ui.shader.usagesNoScan" : "ui.shader.usagesNone"))); return; }
            if (asset.UsedBy.Length == 0) { body.Add(UiKit.Note(Loc.Get("ui.shader.usagesNone"))); return; }
            foreach (var group in asset.UsedBy.GroupBy(UsageKind).OrderBy(g => g.Key))
            {
                body.Add(UiKit.H2(Loc.Get("ui.shader.usages." + group.Key, group.Count())));
                foreach (var u in group.Take(100)) { var p = u; body.Add(UiKit.PathLink(p, () => ctx.Select(p))); }
                if (group.Count() > 100) body.Add(UiKit.Note(Loc.Get("report.andMore", group.Count() - 100).Trim()));
            }
        }

        static string UsageKind(string path)
        {
            if (path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) return "Materials";
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) return "Scenes";
            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return "Prefabs";
            return "Other";
        }

        // ------------------------------------------------------------------------------------------ actions

        /// <summary>Replaces the shader with the (possibly edited) result; a copy of the original goes to Urpify/Backup/ShaderPanel.</summary>
        void Convert()
        {
            var path = model.Path;
            if (!EditorUtility.DisplayDialog(Loc.Get("ui.shader.convertTitle"), Loc.Get("ui.shader.convertText", path),
                    Loc.Get("ui.shader.convert"), Loc.Get("ui.cancel"))) return;
            var backupDir = "Urpify/Backup/ShaderPanel/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backupDir);
            var backup = backupDir + "/" + Path.GetFileName(path);
            File.Copy(path, backup, true);
            MigrationRunner.InstallRuntimeFiles(model.Converter);
            WriteIncludeCopies();
            SourceFiles.Write(path, model.ConvertedText, model.OriginalKind);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            var errors = shader == null ? new System.Collections.Generic.List<CompileMessage> { new CompileMessage { Message = Loc.Get("ui.shader.notImported") } }
                : ShaderCompileCheck.Errors(shader, MigrationRunner.KeywordSets(shader));
            if (errors.Count > 0)
            {
                File.Copy(backup, path, true); // the original's bytes, whatever its encoding (R-03)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                EditorUtility.DisplayDialog(Loc.Get("ui.shader.convertTitle"), Loc.Get("ui.shader.convertFailed", errors[0].Message, errors[0].Line), Loc.Get("ui.ok"));
                return;
            }
            EditorUtility.DisplayDialog(Loc.Get("ui.shader.convertTitle"), Loc.Get("ui.shader.convertDone", backup), Loc.Get("ui.ok"));
            ctx.Select(path);
        }

        void SaveAs()
        {
            var dir = Path.GetDirectoryName(model.Path);
            var target = EditorUtility.SaveFilePanel(Loc.Get("ui.shader.saveAs"), dir, Path.GetFileNameWithoutExtension(model.Path) + "_URP", "shader");
            if (string.IsNullOrEmpty(target)) return;
            SourceFiles.Write(target, model.SaveText, model.OriginalKind);
            var inAssets = ShaderPanelModel.Normalize(target).StartsWith("Assets/", StringComparison.Ordinal);
            if (inAssets)
            {
                MigrationRunner.InstallRuntimeFiles(model.Converter);
                if (model.HasResult) WriteIncludeCopies();
                AssetDatabase.Refresh();
            }
            else EditorUtility.DisplayDialog(Loc.Get("ui.shader.saveAs"), Loc.Get("ui.shader.savedOutside", target), Loc.Get("ui.ok"));
        }

        /// <summary>Converted copies of the shader's include files ("Name_URP.ext" next to the original).</summary>
        void WriteIncludeCopies()
        {
            foreach (var f in model.Conversion?.Files ?? new System.Collections.Generic.List<GeneratedFile>())
            {
                if (File.Exists(f.Path) && SourceFiles.Read(f.Path) == f.Content) continue;
                SourceFiles.WriteLike(f.Path, f.Content, f.Source.Length > 0 ? f.Source : model.Path);
                if (ShaderPanelModel.IsProjectPath(f.Path)) AssetDatabase.ImportAsset(f.Path, ImportAssetOptions.ForceSynchronousImport);
            }
        }

        void OpenFile()
        {
            if (model.InProject) UiKit.OpenAsset(model.Path, 0);
            else EditorUtility.OpenWithDefaultApp(model.Path);
        }
    }
}
