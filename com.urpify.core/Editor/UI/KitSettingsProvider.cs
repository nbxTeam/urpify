using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Urpify.UI
{
    /// <summary>
    /// Settings (TZ 4.3) in Project Settings > Urpify, where Unity keeps a package's project settings; the
    /// window's Settings button opens it. Project settings live in ProjectSettings/UrpifySettings.asset (they go
    /// into version control and reach the team), personal ones in UserSettings/Urpify.json.
    /// </summary>
    static class KitSettingsProvider
    {
        public const string Path = "Project/Urpify";

        [SettingsProvider]
        static SettingsProvider Create() => new SettingsProvider(Path, SettingsScope.Project)
        {
            label = "Urpify",
            keywords = new HashSet<string>(new[] { "URP", "migration", "Built-in", "shader", "convert", "compare" }),
            activateHandler = (_, root) => Build(root),
        };

        public static void Open() => SettingsService.OpenProjectSettings(Path);

        static void Build(VisualElement root)
        {
            var s = KitProjectSettings.instance;
            var u = KitUserSettings.Current;
            var page = new ScrollView();
            page.AddToClassList("urpify-settings");
            page.style.paddingLeft = 10;
            page.style.paddingTop = 2;
            root.Add(page);
            var title = new Label(Loc.Get("ui.settings.title"));
            title.style.fontSize = 19;
            title.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            title.style.marginBottom = 8;
            page.Add(title);

            page.Add(Section(Loc.Get("ui.settings.conversion")));
            page.Add(UiKit.OutputChooser(() => { }));
            page.Add(Choice(Loc.Get("ui.settings.grabPass"), Loc.Get("ui.settings.grabPassTip"),
                new[] { GrabPassSource.OpaqueTexture, GrabPassSource.AfterTransparents }, s.GrabPass, v => s.GrabPass = v));
            page.Add(Choice(Loc.Get("ui.settings.screenEffects"), Loc.Get("ui.settings.screenEffectsTip"),
                new[] { ScreenEffectsPoint.AfterPostProcessing, ScreenEffectsPoint.BeforePostProcessing, ScreenEffectsPoint.AfterOpaques }, s.ScreenEffects, v => s.ScreenEffects = v));

            page.Add(Choice(Loc.Get("ui.settings.shadows"), Loc.Get("ui.settings.shadowsTip"),
                new[] { ShadowsOption.SameAsOriginal, ShadowsOption.Always, ShadowsOption.Never }, s.Shadows, v => s.Shadows = v));
            var batcher = new Toggle(Loc.Get("ui.settings.srpBatcher")) { value = s.SrpBatcher, tooltip = Loc.Get("ui.settings.srpBatcherTip") };
            batcher.RegisterValueChangedCallback(e => s.SrpBatcher = e.newValue);
            page.Add(batcher);

            page.Add(Section(Loc.Get("ui.settings.compare")));
            var width = new IntegerField(Loc.Get("ui.settings.compareWidth")) { value = s.CompareWidth, isDelayed = true };
            width.RegisterValueChangedCallback(e => { s.CompareWidth = e.newValue; width.SetValueWithoutNotify(s.CompareWidth); });
            var height = new IntegerField(Loc.Get("ui.settings.compareHeight")) { value = s.CompareHeight, isDelayed = true };
            height.RegisterValueChangedCallback(e => { s.CompareHeight = e.newValue; height.SetValueWithoutNotify(s.CompareHeight); });
            var frames = new IntegerField(Loc.Get("ui.settings.compareFrames")) { value = s.CompareFrames, isDelayed = true, tooltip = Loc.Get("ui.settings.compareFramesTip") };
            frames.RegisterValueChangedCallback(e => { s.CompareFrames = e.newValue; frames.SetValueWithoutNotify(s.CompareFrames); });
            // The Game view's size by default (owner's check 19.10.2026, item 5); width and height only for a size of one's own.
            void SizeFields(CompareSize v) => width.style.display = height.style.display = v == CompareSize.Custom ? DisplayStyle.Flex : DisplayStyle.None;
            page.Add(Choice(Loc.Get("ui.settings.compareSize"), Loc.Get("ui.settings.compareSizeTip"),
                new[] { CompareSize.GameView, CompareSize.Custom }, s.CompareSize, v => { s.CompareSize = v; SizeFields(v); }));
            SizeFields(s.CompareSize);
            page.Add(width);
            page.Add(height);
            page.Add(frames);
            var selected = new TextField(Loc.Get("ui.settings.compareSelected")) { value = string.Join(";", s.CompareSelected), isDelayed = true, tooltip = Loc.Get("ui.settings.compareSelectedTip") };
            selected.RegisterValueChangedCallback(e => s.CompareSelected = (e.newValue ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray());
            page.Add(Choice(Loc.Get("ui.settings.compareScenes"), Loc.Get("ui.settings.compareScenesTip"),
                new[] { CompareScenes.BuildSettings, CompareScenes.All, CompareScenes.Selected }, s.CompareScenes,
                v => { s.CompareScenes = v; selected.style.display = v == CompareScenes.Selected ? DisplayStyle.Flex : DisplayStyle.None; }));
            selected.style.display = s.CompareScenes == CompareScenes.Selected ? DisplayStyle.Flex : DisplayStyle.None;
            page.Add(selected);
            page.Add(Note(Loc.Get("ui.settings.compareThresholds")));

            page.Add(Section(Loc.Get("ui.settings.personal")));
            page.Add(LanguageChoice(root));
            page.Add(Choice(Loc.Get("ui.shader.builtInPreview"), Loc.Get("ui.shader.builtInPreviewTip"),
                new[] { BuiltInPreview.Live, BuiltInPreview.FromCaptures }, u.BuiltInPreview, v => { u.BuiltInPreview = v; u.Save(); }));
            var overlays = new Toggle(Loc.Get("ui.settings.overlays")) { value = u.ProjectOverlays, tooltip = Loc.Get("ui.settings.overlaysTip") };
            overlays.RegisterValueChangedCallback(e => { u.ProjectOverlays = e.newValue; u.Save(); EditorApplication.RepaintProjectWindow(); });
            page.Add(overlays);
            // One label width, so a long label ("Screen effects inject at") does not push its field out of line.
            page.Query<Label>(className: "unity-base-field__label").ForEach(l => { l.style.minWidth = 180; l.style.width = 180; });
        }

        /// <summary>
        /// The window's language: "Editor language" (the Editor's own when Urpify has it, else English) or one of
        /// <see cref="Loc.Languages"/> by its own name. A change saves the personal setting and redraws the open windows.
        /// </summary>
        static DropdownField LanguageChoice(VisualElement root)
        {
            var u = KitUserSettings.Current;
            var editor = Loc.LanguageNames[Array.IndexOf(Loc.Languages, Loc.FromSystemLanguage(Loc.EditorLanguage()))];
            var names = new List<string> { Loc.Get("ui.settings.languageEditor", editor) };
            names.AddRange(Loc.LanguageNames);
            int current = string.IsNullOrEmpty(u.Language) ? 0 : Array.IndexOf(Loc.Languages, u.Language) + 1;
            var field = new DropdownField(Loc.Get("ui.settings.language"), names, Math.Max(0, current)) { tooltip = Loc.Get("ui.settings.languageTip") };
            field.RegisterValueChangedCallback(e =>
            {
                int k = names.IndexOf(e.newValue);
                Loc.Choose(k <= 0 ? "" : Loc.Languages[k - 1]);
                root.Clear();
                Build(root);
            });
            return field;
        }

        static Label Section(string text)
        {
            var l = new Label(text);
            l.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            l.style.marginTop = 10;
            l.style.marginBottom = 2;
            return l;
        }

        static Label Note(string text)
        {
            var l = new Label(text);
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.opacity = 0.8f;
            l.style.marginTop = 2;
            return l;
        }

        /// <summary>An enum setting as a dropdown; the names come from the string table ("ui.settings.value.Name").</summary>
        static DropdownField Choice<T>(string label, string tooltip, T[] values, T current, Action<T> set) where T : struct, Enum
        {
            var names = values.Select(v => Loc.Get("ui.settings.value." + v)).ToList();
            var field = new DropdownField(label, names, Math.Max(0, Array.IndexOf(values, current))) { tooltip = tooltip };
            field.RegisterValueChangedCallback(e =>
            {
                var k = names.IndexOf(e.newValue);
                if (k >= 0) set(values[k]);
            });
            return field;
        }
    }
}
