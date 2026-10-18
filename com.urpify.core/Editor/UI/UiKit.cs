using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Urpify.UI
{
    /// <summary>What the panels need from the window: the current report and navigation.</summary>
    public interface IWindowContext
    {
        MigrationReport Report { get; }
        /// <summary>Selects an asset or scene in the list and shows it on the right.</summary>
        void Select(string path);
        void StartScan();
        /// <summary>The migration's result (what was fixed, what is left); null before anything was migrated.</summary>
        Pipeline.MigrationSummary Summary { get; }
        /// <summary>The id of the strip's filter that is on, or null.</summary>
        string ActiveFilter { get; }
        /// <summary>Turns a filter of the strip on, or off when it is on.</summary>
        void ToggleFilter(QuickFilter filter);
    }

    /// <summary>Small building blocks shared by the window's panels.</summary>
    public static class UiKit
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// The Output setting (TZ 4.3) as a dropdown: Replace original / Write copy / Keep Built-in support. Changes the project setting
        /// and calls <paramref name="changed"/>.
        /// </summary>
        public static DropdownField OutputChooser(Action changed)
        {
            var values = new[] { ShaderOutput.ReplaceOriginal, ShaderOutput.WriteCopy, ShaderOutput.KeepBuiltInSupport };
            var names = values.Select(v => Loc.Get("ui.output." + v)).ToList();
            var field = new DropdownField(Loc.Get("ui.output.label"), names, Array.IndexOf(values, KitProjectSettings.instance.Output) is var i && i >= 0 ? i : 0)
            {
                tooltip = Loc.Get("ui.output.tip"),
            };
            field.AddToClassList("urpify-output");
            field.RegisterValueChangedCallback(e =>
            {
                var k = names.IndexOf(e.newValue);
                if (k < 0 || values[k] == KitProjectSettings.instance.Output) return;
                KitProjectSettings.instance.Output = values[k];
                changed?.Invoke();
            });
            return field;
        }

        public static Label H2(string text) { var l = new Label(text); l.AddToClassList("urpify-h2"); return l; }
        public static Label Note(string text) { var l = new Label(text); l.AddToClassList("urpify-note"); return l; }

        public static VisualElement Kv(string k, int v)
        {
            var row = new VisualElement();
            row.AddToClassList("urpify-kv");
            var key = new Label(k);
            key.AddToClassList("urpify-kv-key");
            row.Add(key);
            row.Add(new Label(v.ToString()));
            return row;
        }

        public static Button Link(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("urpify-link");
            return b;
        }

        /// <summary>
        /// A link that is a project path: on one line, shortened in the middle so the file name stays visible, the whole path
        /// in the tooltip (design review 13.10.2026, item 5: long paths were cut off at the panel's edge without a sign).
        /// </summary>
        public static Button PathLink(string path, Action onClick)
        {
            var b = Link(path, onClick);
            b.tooltip = path;
            Shorten(b);
            return b;
        }

        /// <summary>A path under a header, shortened in the middle like <see cref="PathLink"/>.</summary>
        public static Label PathSubtitle(string path)
        {
            var l = Subtitle(path);
            l.tooltip = path;
            Shorten(l);
            return l;
        }

        static void Shorten(TextElement e)
        {
            e.style.whiteSpace = WhiteSpace.NoWrap;
            e.style.overflow = Overflow.Hidden;
            e.style.textOverflow = TextOverflow.Ellipsis;
            e.style.unityTextOverflowPosition = TextOverflowPosition.Middle;
            e.style.flexShrink = 1;
            e.style.minWidth = 0;
        }

        /// <summary>Header row: severity icon, file name, optional fix-tier badge.</summary>
        public static VisualElement Header(Severity severity, string title, string badge = null)
        {
            var header = new VisualElement();
            header.AddToClassList("urpify-header");
            header.Add(new SeverityIcon(severity) { tooltip = Loc.Get("severity." + severity) });
            var t = new Label(title) { tooltip = title };
            t.AddToClassList("urpify-title");
            header.Add(t);
            if (!string.IsNullOrEmpty(badge))
            {
                var b = new Label(badge);
                b.AddToClassList("urpify-header-tier");
                header.Add(b);
            }
            return header;
        }

        static Font monospace;

        /// <summary>
        /// Monospace text for code (inherited by children): the Editor's own RobotoMono (Editor/Data/Resources/Fonts, loaded
        /// by this path in 6000.5.9f1 and 2022.3.62f3: Spikes/M4/font-probe.txt), an OS font only if that ever fails.
        /// </summary>
        public static void UseMonospace(VisualElement e)
        {
            if (monospace == null)
            {
                try { monospace = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font; } catch { monospace = null; }
                if (monospace == null) monospace = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Menlo", "DejaVu Sans Mono", "Courier New" }, 11);
            }
            if (monospace != null) e.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(monospace));
        }

        /// <summary>Code text for a Label: spaces that do not collapse (white-space: pre does not exist in 2022.3), tabs as 4 spaces.</summary>
        public static string CodeText(string line) => line.Replace("\t", "    ").Replace(' ', '\u00A0');

        public static Label Subtitle(string text) { var l = new Label(text); l.AddToClassList("urpify-subtitle"); return l; }

        static Texture s_Lock;

        /// <summary>The Editor's own lock icon (the Inspector's lock), for what only Urpify Pro fixes.</summary>
        public static Texture LockIcon
        {
            get
            {
                if (s_Lock != null) return s_Lock;
                var c = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_InspectorLock" : "InspectorLock");
                return s_Lock = c?.image;
            }
        }

        /// <summary>A "Pro" badge with the lock icon and a tooltip saying what Pro does here.</summary>
        public static VisualElement ProBadge(string tooltip)
        {
            var badge = new VisualElement { tooltip = tooltip };
            badge.AddToClassList("urpify-pro-badge");
            badge.Add(new Image { image = LockIcon, scaleMode = ScaleMode.ScaleToFit });
            badge.Add(new Label(Loc.Get("ui.list.pro")));
            return badge;
        }

        /// <summary>
        /// The buttons that work for this file (owner's check 19.10.2026, item 12: Open and Show in Project did nothing for
        /// Packages/manifest.json). An asset: Open and Show in Project. A file outside Assets: Open (in the external editor) and
        /// Show in Explorer; a ProjectSettings file opens its Project Settings page instead.
        /// </summary>
        public static VisualElement AssetButtons(string path)
        {
            var buttons = new VisualElement();
            buttons.AddToClassList("urpify-buttons");
            foreach (var (text, tip, action) in FileActions(path))
                buttons.Add(new Button(action) { text = text, tooltip = tip });
            return buttons;
        }

        /// <summary>What can be done with a file from the window, as button text, tooltip and action.</summary>
        public static System.Collections.Generic.List<(string text, string tip, Action action)> FileActions(string path)
        {
            var list = new System.Collections.Generic.List<(string, string, Action)>();
            if (string.IsNullOrEmpty(path)) return list;
            if (IsAsset(path))
            {
                list.Add((Loc.Get("ui.details.open"), "", () => OpenAsset(path, 0)));
                list.Add((Loc.Get("ui.details.ping"), "", () => Ping(path)));
                return list;
            }
            var page = SettingsPage(path);
            if (page != null) list.Add((Loc.Get("ui.details.openSettings"), Loc.Get("ui.details.openSettingsTip"), () => SettingsService.OpenProjectSettings(page)));
            else if (System.IO.File.Exists(path)) list.Add((Loc.Get("ui.details.open"), Loc.Get("ui.details.openExternalTip"), () => OpenExternal(path, 0)));
            if (System.IO.File.Exists(path) || System.IO.Directory.Exists(path)) list.Add((Loc.Get(Application.platform == RuntimePlatform.OSXEditor ? "ui.details.revealMac" : "ui.details.reveal"), "", () => EditorUtility.RevealInFinder(path)));
            return list;
        }

        /// <summary>A path the asset database knows: under Assets, or inside a package.</summary>
        public static bool IsAsset(string path) =>
            !string.IsNullOrEmpty(path) && (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal) || IsPackageAsset(path));

        static bool IsPackageAsset(string path)
        {
            if (!path.StartsWith("Packages/", StringComparison.Ordinal)) return false;
            // Packages/manifest.json and packages-lock.json sit in the Packages folder itself: they are not assets.
            if (path.IndexOf('/', "Packages/".Length) < 0) return false;
            return !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path));
        }

        /// <summary>The Project Settings page of a ProjectSettings file, or null.</summary>
        public static string SettingsPage(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("ProjectSettings/", StringComparison.Ordinal)) return null;
            switch (System.IO.Path.GetFileName(path))
            {
                case "GraphicsSettings.asset": return "Project/Graphics";
                case "QualitySettings.asset": return "Project/Quality";
                case "ProjectSettings.asset": return "Project/Player";
                case "EditorSettings.asset": return "Project/Editor";
                case "TagManager.asset": return "Project/Tags and Layers";
                case "InputManager.asset": return "Project/Input Manager";
                case "TimeManager.asset": return "Project/Time";
                case "PhysicsManager.asset": return "Project/Physics";
                case "AudioManager.asset": return "Project/Audio";
                case "UrpifySettings.asset": return KitSettingsProvider.Path;
                default: return null;
            }
        }

        /// <summary>Opens a file that is not an asset in the external script editor (falls back to the system's program).</summary>
        public static void OpenExternal(string path, int line)
        {
            var full = System.IO.Path.GetFullPath(path);
            if (!UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(full, Math.Max(1, line))) EditorUtility.OpenWithDefaultApp(full);
        }

        /// <summary>Who fixes it, for the list's column: Unity, Urpify, Pro, By hand (owner's check 19.10.2026, item 7).</summary>
        public static string OwnerShort(FixOwner o) => o == FixOwner.None ? "" : Loc.Get("owner.short." + o);

        /// <summary>Who fixes it, as a sentence for a card or a tooltip; how close the result is follows it, less loudly.</summary>
        public static string OwnerLong(FixOwner o) => Loc.Get("owner." + o);

        /// <summary>The second line under who fixes it: how close the result is, or why a person has to do it. "" when nothing to add.</summary>
        public static string Accuracy(FixTier t)
        {
            switch (t)
            {
                case FixTier.Auto: return Loc.Get("ui.fix.exact");
                case FixTier.AutoApprox: return Loc.Get("ui.fix.approx");
                case FixTier.Review: return Loc.Get("ui.fix.review");
                case FixTier.NotSupportedYet: return Loc.Get("ui.fix.notYet");
                default: return "";
            }
        }

        public static string LocalTime(string utc) =>
            DateTime.TryParse(utc, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)
                ? t.ToLocalTime().ToString("HH:mm") : utc;

        public static Severity ObservedIcon(ObservedVerdict v) =>
            v == ObservedVerdict.Pink ? Severity.Pink : v == ObservedVerdict.Changed ? Severity.Silent : Severity.Info;

        public static void OpenAsset(string path, int line)
        {
            var obj = IsAsset(path) ? AssetDatabase.LoadMainAssetAtPath(path) : null;
            if (obj == null)
            {
                // Not an asset (Packages/manifest.json, a ProjectSettings file): its settings page, or the file in an editor.
                var page = SettingsPage(path);
                if (page != null) SettingsService.OpenProjectSettings(page);
                else if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path)) OpenExternal(path, line);
                return;
            }
            if (line > 0) AssetDatabase.OpenAsset(obj, line);
            else AssetDatabase.OpenAsset(obj);
        }

        public static void Ping(string path)
        {
            var obj = IsAsset(path) ? AssetDatabase.LoadMainAssetAtPath(path) : null;
            if (obj == null) { if (!string.IsNullOrEmpty(path) && (System.IO.File.Exists(path) || System.IO.Directory.Exists(path))) EditorUtility.RevealInFinder(path); return; }
            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;
        }
    }
}
