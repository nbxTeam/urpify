using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using Urpify.Analysis;
using Urpify.Parsing;

namespace Urpify.UI
{
    /// <summary>
    /// Send report (TZ 11.3): a shader Urpify cannot convert, with its include files, its findings, the versions and the
    /// compiler errors, plus an optional comment and contact, packed into a zip. The window lists every file that would
    /// leave the computer and says plainly that it is the user's code, used only to find what the converter is missing
    /// (decision before M8 No. 3). Nothing is sent without pressing Send; Save zip instead keeps it local. Send posts the
    /// zip to the support channel's Discord webhook, at most once a minute from this project. This is the only network
    /// call in Urpify.
    /// </summary>
    public sealed class SendReportWindow : EditorWindow
    {
        const string LastSentFile = "Library/Urpify/send-report.json";

        [Serializable] sealed class LastSent { public string Utc = ""; }

        string shaderPath;
        MigrationReport report;
        readonly List<string> files = new List<string>();
        TextField comment, contact;
        Button send;
        Label status;
        UnityWebRequestAsyncOperation pending;

        public static void Open(string selectedPath, MigrationReport report)
        {
            var w = GetWindow<SendReportWindow>(true);
            w.shaderPath = ShaderPanelModel.IsShaderFile(selectedPath) ? selectedPath : null;
            w.report = report;
            w.titleContent = new GUIContent(Loc.Get("ui.send.title"));
            w.minSize = new Vector2(460, 420);
            w.Collect();
            w.Build();
            w.Show();
        }

        void Collect()
        {
            files.Clear();
            if (shaderPath == null) return;
            files.Add(shaderPath);
            try
            {
                var doc = ShaderLabParser.Parse(File.ReadAllText(shaderPath), shaderPath);
                var scope = ProgramScope.Of(doc, doc.Programs);
                var root = Path.GetFullPath(".").Replace('\\', '/') + "/";
                foreach (var f in scope.Files.Select(p => p.File.Replace('\\', '/')).Distinct())
                    files.Add(f.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? f.Substring(root.Length) : f);
            }
            catch (Exception) { }
        }

        void Build()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = root.style.paddingRight = 10;
            root.style.paddingTop = 8;
            var title = new Label(Loc.Get("ui.send.heading"));
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            root.Add(title);
            if (shaderPath == null)
            {
                root.Add(Text(Loc.Get("ui.send.noShader")));
                return;
            }
            var warn = new HelpBox(Loc.Get("ui.send.warning"), HelpBoxMessageType.Warning);
            root.Add(warn);
            root.Add(Text(Loc.Get("ui.send.files", files.Count + 1)));
            var list = new ScrollView();
            list.style.maxHeight = 140;
            foreach (var f in files) list.Add(Text("  " + f));
            list.Add(Text("  report.json  " + Loc.Get("ui.send.reportFile")));
            root.Add(list);
            comment = new TextField(Loc.Get("ui.send.comment")) { multiline = true };
            comment.style.minHeight = 60;
            root.Add(comment);
            contact = new TextField(Loc.Get("ui.send.contact")) { tooltip = Loc.Get("ui.send.contactTip") };
            root.Add(contact);
            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginTop = 8;
            send = new Button(Send) { text = Loc.Get("ui.send.send") };
            var settings = SupportSettings.Load();
            bool configured = settings != null && !string.IsNullOrEmpty(settings.webhookUrl);
            send.SetEnabled(configured);
            send.tooltip = configured ? Loc.Get("ui.send.sendTip") : Loc.Get("ui.send.notConfigured");
            buttons.Add(send);
            buttons.Add(new Button(SaveZip) { text = Loc.Get("ui.send.saveZip"), tooltip = Loc.Get("ui.send.saveZipTip") });
            root.Add(buttons);
            // A disabled Send says why in the window, not only in its tooltip.
            status = Text(configured ? "" : Loc.Get("ui.send.notConfigured"));
            root.Add(status);
        }

        static Label Text(string s) { var l = new Label(s); l.style.whiteSpace = WhiteSpace.Normal; return l; }

        byte[] Zip()
        {
            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    foreach (var f in files.Where(File.Exists))
                    {
                        var entry = zip.CreateEntry(f.Replace(':', '_').TrimStart('/'));
                        using (var w = entry.Open()) { var bytes = File.ReadAllBytes(f); w.Write(bytes, 0, bytes.Length); }
                    }
                    var info = zip.CreateEntry("report.json");
                    using (var w = new StreamWriter(info.Open(), new UTF8Encoding(false))) w.Write(ReportJson());
                }
                return ms.ToArray();
            }
        }

        string ReportJson()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            var errors = shader != null ? ShaderCompileCheck.Errors(shader).Select(e => e.ToString()).Take(50).ToList() : new List<string>();
            var findings = report?.Findings.Where(f => f.AssetPath == shaderPath).Select(f => f.ToString()).ToList() ?? new List<string>();
            var urp = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.render-pipelines.universal/package.json")?.version ?? "";
            var sb = new StringBuilder("{\n");
            void Prop(string k, string v, bool last = false) => sb.Append("  \"").Append(k).Append("\": \"").Append(Escape(v)).Append(last ? "\"\n" : "\",\n");
            Prop("shader", shaderPath);
            Prop("unity", Application.unityVersion);
            Prop("urp", urp);
            Prop("kit", Scanner.ProjectScanner.KitVersion + (Edition.ProInstalled ? " Pro" : " Free"));
            Prop("comment", comment?.value ?? "");
            Prop("contact", contact?.value ?? "");
            Prop("findings", string.Join("\n", findings));
            Prop("compileErrors", string.Join("\n", errors), true);
            return sb.Append("}\n").ToString();
        }

        static string Escape(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') continue;
                else if (c < 0x20) sb.Append(' ');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        void SaveZip()
        {
            var target = EditorUtility.SaveFilePanel(Loc.Get("ui.send.saveZip"), "", Path.GetFileNameWithoutExtension(shaderPath) + "-report", "zip");
            if (string.IsNullOrEmpty(target)) return;
            File.WriteAllBytes(target, Zip());
            EditorUtility.RevealInFinder(target);
            status.text = Loc.Get("ui.send.saved", target);
        }

        void Send()
        {
            if (pending != null) return;
            var settings = SupportSettings.Load();
            if (settings == null || string.IsNullOrEmpty(settings.webhookUrl)) return;
            // At most one report a minute from this project.
            try
            {
                if (File.Exists(LastSentFile) && DateTime.TryParse(JsonUtility.FromJson<LastSent>(File.ReadAllText(LastSentFile)).Utc, out var last)
                    && DateTime.UtcNow - last.ToUniversalTime() < TimeSpan.FromMinutes(1))
                { status.text = Loc.Get("ui.send.tooSoon"); return; }
            }
            catch (Exception) { }
            var zip = Zip();
            if (zip.Length > settings.maxAttachmentMB * 1024 * 1024) { status.text = Loc.Get("ui.send.tooLarge", (zip.Length / 1048576f).ToString("0.0", UiKit.Inv), settings.maxAttachmentMB); return; }
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("content", "Urpify report: " + Path.GetFileName(shaderPath)),
                new MultipartFormFileSection("file", zip, Path.GetFileNameWithoutExtension(shaderPath) + "-report.zip", "application/zip"),
            };
            var request = UnityWebRequest.Post(settings.webhookUrl, form);
            pending = request.SendWebRequest();
            send.SetEnabled(false);
            status.text = Loc.Get("ui.send.sending");
            pending.completed += _ =>
            {
                bool ok = request.result == UnityWebRequest.Result.Success;
                status.text = ok ? Loc.Get("ui.send.sent") : Loc.Get("ui.send.failed", request.error);
                if (ok)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LastSentFile));
                    File.WriteAllText(LastSentFile, JsonUtility.ToJson(new LastSent { Utc = DateTime.UtcNow.ToString("o") }));
                }
                send.SetEnabled(true);
                request.Dispose();
                pending = null;
            };
        }
    }
}
