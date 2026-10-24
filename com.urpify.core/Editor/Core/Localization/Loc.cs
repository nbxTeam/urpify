using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Urpify
{
    /// <summary>
    /// Interface and finding strings from Localization/&lt;lang&gt;.json (a flat object of string keys and values).
    /// Placeholders are {0}, {1}... as in string.Format. A missing key returns the key itself so it is visible.
    /// </summary>
    public static class Loc
    {
        static Dictionary<string, string> table, english;
        static string loadedFrom = "";

        /// <summary>The window's languages (file names under Localization/), English first.</summary>
        public static readonly string[] Languages = { "en", "zh-Hans", "ja", "ko", "ru", "es", "pt-BR", "de" };
        /// <summary>Each language's name in itself, as the language setting lists it.</summary>
        public static readonly string[] LanguageNames = { "English", "简体中文", "日本語", "한국어", "Русский", "Español", "Português (Brasil)", "Deutsch" };

        static string language;

        /// <summary>
        /// The language strings come in: the personal setting (UserSettings), or with no choice the Editor's own language
        /// when the window has it, else English. Setting it only changes this session (tests); <see cref="Choose"/> saves.
        /// </summary>
        public static string Language
        {
            get => language ?? (language = Resolve(KitUserSettings.Current.Language, EditorLanguage()));
            set { language = value; table = null; }
        }

        /// <summary>Raised after <see cref="Choose"/>: open windows build their contents again.</summary>
        public static event Action LanguageChanged;

        /// <summary>Saves the personal language setting ("" = the Editor's language) and switches to it.</summary>
        public static void Choose(string chosen)
        {
            var u = KitUserSettings.Current;
            u.Language = chosen ?? "";
            u.Save();
            language = null;
            table = null;
            LanguageChanged?.Invoke();
        }

        public static string Resolve(string chosen, UnityEngine.SystemLanguage editor) =>
            !string.IsNullOrEmpty(chosen) && Array.IndexOf(Languages, chosen) >= 0 ? chosen : FromSystemLanguage(editor);

        /// <summary>The window's language for an Editor language; English when the window does not have it.</summary>
        public static string FromSystemLanguage(UnityEngine.SystemLanguage l)
        {
            switch (l)
            {
                case UnityEngine.SystemLanguage.ChineseSimplified:
                case UnityEngine.SystemLanguage.Chinese: return "zh-Hans";
                case UnityEngine.SystemLanguage.Japanese: return "ja";
                case UnityEngine.SystemLanguage.Korean: return "ko";
                case UnityEngine.SystemLanguage.Russian: return "ru";
                case UnityEngine.SystemLanguage.Spanish: return "es";
                case UnityEngine.SystemLanguage.Portuguese: return "pt-BR";
                case UnityEngine.SystemLanguage.German: return "de";
                default: return "en";
            }
        }

        /// <summary>
        /// The Editor's interface language (Preferences > Languages). Unity has no public property for it:
        /// UnityEditor.LocalizationDatabase.currentEditorLanguage is internal on 2022.3 and 6000.x, so it is read by
        /// reflection, and anything unexpected means English. The Asset Store build uses no internal Editor API (Submission
        /// Guidelines 2.5.g): there the window is in English until a language is chosen in Urpify's settings.
        /// </summary>
        public static UnityEngine.SystemLanguage EditorLanguage()
        {
            if (BuildInfo.AssetStore) return UnityEngine.SystemLanguage.English;
#if !URPIFY_ASSET_STORE
            try
            {
                var t = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LocalizationDatabase");
                var p = t?.GetProperty("currentEditorLanguage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (p?.GetValue(null) is UnityEngine.SystemLanguage l) return l;
            }
            catch { }
#endif
            return UnityEngine.SystemLanguage.English;
        }

        /// <summary>Absolute path of the package root, or null outside a package context (tests set it explicitly).</summary>
        public static string PackageRoot
        {
            get
            {
                if (packageRoot != null) return packageRoot;
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Loc).Assembly);
                packageRoot = info?.resolvedPath;
                // Imported from a .unitypackage Urpify is a folder of Assets, not a package (QA-006).
                if (packageRoot == null)
                {
                    var root = KitPaths.Root(KitPaths.Core);
                    if (root.StartsWith("Assets/", System.StringComparison.Ordinal) && System.IO.Directory.Exists(root)) packageRoot = System.IO.Path.GetFullPath(root);
                }
                return packageRoot;
            }
            set { packageRoot = value; table = null; english = null; }
        }
        static string packageRoot;

        /// <summary>The string in the current language; a key the language lacks falls back to English, then to the key.</summary>
        public static string Get(string key)
        {
            EnsureLoaded();
            return table.TryGetValue(key, out var v) ? v : english.TryGetValue(key, out v) ? v : key;
        }

        public static string Get(string key, params object[] args)
        {
            var fmt = Get(key);
            if (args == null || args.Length == 0) return fmt;
            try { return string.Format(fmt, args); }
            catch (FormatException) { return fmt; }
        }

        public static bool Has(string key)
        {
            EnsureLoaded();
            return table.ContainsKey(key) || english.ContainsKey(key);
        }

        /// <summary>Every key of the English table, which is complete by definition.</summary>
        public static IReadOnlyCollection<string> Keys
        {
            get { EnsureLoaded(); return english.Keys; }
        }

        public static void Reload() { table = null; english = null; }

        /// <summary>One language's table as it is in its file (no English fallback); empty when there is no file.</summary>
        public static Dictionary<string, string> Table(string lang)
        {
            var root = PackageRoot;
            var path = root == null ? null : Path.Combine(root, "Localization", lang + ".json");
            return path != null && File.Exists(path) ? ParseFlatObject(File.ReadAllText(path)) : new Dictionary<string, string>(StringComparer.Ordinal);
        }

        static void EnsureLoaded()
        {
            if (english == null) english = Table("en");
            if (table != null) return;
            var lang = Language;
            table = lang == "en" ? english : Table(lang);
            loadedFrom = lang;
        }

        /// <summary>Parses {"k":"v",...} with standard JSON string escapes. Anything else is an error.</summary>
        public static Dictionary<string, string> ParseFlatObject(string json)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            int i = 0;
            Skip(json, ref i);
            Expect(json, ref i, '{');
            Skip(json, ref i);
            if (i < json.Length && json[i] == '}') return d;
            while (true)
            {
                Skip(json, ref i);
                var k = ReadString(json, ref i);
                Skip(json, ref i);
                Expect(json, ref i, ':');
                Skip(json, ref i);
                var v = ReadString(json, ref i);
                d[k] = v;
                Skip(json, ref i);
                if (i < json.Length && json[i] == ',') { i++; continue; }
                Expect(json, ref i, '}');
                return d;
            }
        }

        static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static void Expect(string s, ref int i, char c)
        {
            if (i >= s.Length || s[i] != c) throw new FormatException($"Expected '{c}' at {i}");
            i++;
        }

        static string ReadString(string s, ref int i)
        {
            Expect(s, ref i, '"');
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            Expect(s, ref i, '"');
            return sb.ToString();
        }
    }
}
