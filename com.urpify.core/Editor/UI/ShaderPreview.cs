using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Urpify.Verify;

namespace Urpify.UI
{
    /// <summary>
    /// Preview tab of the shader panel (TZ 4.2): the Built-in original and the URP result on a sphere, cube, plane or
    /// a mesh of your choice, with a movable light; Built-in, URP, curtain or difference; material properties change in
    /// both at once. The Built-in side renders through <see cref="PipelineSwap"/>; with the Built-in preview set to
    /// From captures, or after a failed swap, the scenes' before/after captures stand in for it.
    /// </summary>
    public sealed class ShaderPreview : VisualElement
    {
        const int Size = 512;
        static readonly string[] Modes = { "BuiltIn", "Urp", "Curtain", "Difference" };
        static readonly string[] Meshes = { "Sphere", "Cube", "Plane", "Custom" };
        static int modeIndex = 2, meshIndex;
        static float lightYaw = -30f;
        static Mesh customMesh;

        readonly ShaderPanelModel model;
        readonly IWindowContext ctx;
        readonly VisualElement view = new VisualElement();
        readonly VisualElement status = new VisualElement();
        readonly VisualElement captures = new VisualElement();
        readonly ObjectField customField;
        readonly List<UnityEngine.Object> textures = new List<UnityEngine.Object>();
        Shader originalShader, convertedShader, projectShader;
        bool ownsOriginal;
        /// <summary>The backup this file's Built-in original came from, when the file in the project is already converted; else null.</summary>
        string backupId;
        Material birpMat, urpMat;
        PreviewRenderUtility pru;
        IVisualElementScheduledItem pending;

        public ShaderPreview(ShaderPanelModel model, IWindowContext ctx)
        {
            this.model = model;
            this.ctx = ctx;
            AddToClassList("urpify-preview");
            var controls = new VisualElement();
            controls.AddToClassList("urpify-preview-controls");
            var mode = new PopupField<string>(Loc.Get("ui.shader.mode"), Modes.Select(m => Loc.Get("ui.shader.mode." + m)).ToList(), modeIndex);
            mode.RegisterValueChangedCallback(_ => { modeIndex = mode.index; Refresh(); });
            var mesh = new PopupField<string>(Loc.Get("ui.shader.mesh"), Meshes.Select(m => Loc.Get("ui.shader.mesh." + m)).ToList(), meshIndex);
            customField = new ObjectField(Loc.Get("ui.shader.mesh.Custom")) { objectType = typeof(Mesh), value = customMesh, allowSceneObjects = false };
            customField.style.display = meshIndex == 3 ? DisplayStyle.Flex : DisplayStyle.None;
            mesh.RegisterValueChangedCallback(_ => { meshIndex = mesh.index; customField.style.display = meshIndex == 3 ? DisplayStyle.Flex : DisplayStyle.None; Refresh(); });
            customField.RegisterValueChangedCallback(e => { customMesh = e.newValue as Mesh; Refresh(); });
            var light = new Slider(Loc.Get("ui.shader.light"), -180, 180) { value = lightYaw, tooltip = Loc.Get("ui.shader.lightTip") };
            light.RegisterValueChangedCallback(e => { lightYaw = e.newValue; RequestRefresh(); });
            var setting = new PopupField<string>(Loc.Get("ui.shader.builtInPreview"),
                new List<string> { Loc.Get("ui.shader.builtInPreview.Live"), Loc.Get("ui.shader.builtInPreview.FromCaptures") },
                (int)KitUserSettings.Current.BuiltInPreview) { tooltip = Loc.Get("ui.shader.builtInPreviewTip") };
            setting.RegisterValueChangedCallback(_ =>
            {
                var s = KitUserSettings.Current;
                s.BuiltInPreview = (BuiltInPreview)setting.index;
                s.Save();
                Refresh();
            });
            controls.Add(mode);
            controls.Add(mesh);
            controls.Add(customField);
            controls.Add(light);
            controls.Add(setting);
            Add(controls);
            Add(status);
            Add(view);
            Add(captures);
            if (model.Doc.Properties.Count > 0)
            {
                var props = new Foldout { text = Loc.Get("ui.shader.properties"), value = true };
                props.Add(PropertyEditor());
                Add(props);
            }
            model.Changed += OnModelChanged;
            RegisterCallback<DetachFromPanelEvent>(_ => { model.Changed -= OnModelChanged; Release(); });
            Refresh();
        }

        /// <summary>Coalesces slider drags: one render (and one pipeline swap) per pause, not one per event.</summary>
        void RequestRefresh()
        {
            if (pending != null) return;
            pending = schedule.Execute(() => { pending = null; Refresh(); });
            pending.ExecuteLater(150);
        }

        void OnModelChanged()
        {
            DestroyConverted();
            Refresh();
        }

        void Refresh()
        {
            view.Clear();
            status.Clear();
            captures.Clear();
            ReleaseTextures();
            try { EnsureMaterials(); }
            catch (Exception e) { status.Add(Message(Loc.Get("ui.shader.previewFailed", e.Message))); return; }
            var mode = Modes[modeIndex];
            Texture2D birp = null, urp = null;
            // After the migration the file is the URP version: Built-in draws none of it, and an empty half explained nothing
            // (window check 2026-10-11, FurShells in a migrated project). The Built-in half then draws the original from the
            // backup; without a backup, URP alone and a note.
            if (mode != "Urp" && UrpOnly && backupId == null) status.Add(UiKit.Note(Loc.Get("ui.shader.previewUrpOnly")));
            else if (mode != "Urp")
            {
                if (backupId != null) status.Add(UiKit.Note(Loc.Get("ui.shader.previewFromBackup", backupId)));
                if (!PipelineSwap.CanSwap(out var reason)) { status.Add(Message(reason)); ShowCaptures(); }
                else if (!PipelineSwap.RenderBuiltIn(() => birp = Render(birpMat))) { status.Add(Message(PipelineSwap.Failure)); ShowCaptures(); }
            }
            if (mode != "BuiltIn")
            {
                if (urpMat != null) urp = Render(urpMat);
                else status.Add(Message(model.HasResult ? Loc.Get("ui.shader.previewNoShader") : Loc.Get("ui.shader.previewNoResult")));
                if (!model.HasResult && !model.ProInstalled)
                {
                    var row = new VisualElement();
                    row.AddToClassList("urpify-pro-row");
                    row.Add(UiKit.ProBadge(Loc.Get("ui.shader.previewProTip")));
                    row.Add(new Label(Loc.Get("ui.shader.previewAsIs")));
                    status.Add(row);
                }
            }
            Texture2D diff = null;
            if (birp != null && urp != null)
            {
                var map = new Color32[birp.width * birp.height];
                var r = ImageCompare.Compare(birp.GetPixels32(), urp.GetPixels32(), birp.width, birp.height, new CompareThresholds(), map);
                diff = Keep(new Texture2D(birp.width, birp.height, TextureFormat.RGBA32, false));
                diff.SetPixels32(map);
                diff.Apply();
                status.Add(UiKit.Note(Loc.Get("ui.shader.metrics", r.ChangedPixelsPct.ToString("0.##", CultureInfo.InvariantCulture),
                    r.MeanDiff.ToString("0.#", CultureInfo.InvariantCulture))));
            }
            switch (mode)
            {
                case "BuiltIn": if (birp != null) view.Add(Picture(birp)); break;
                case "Urp": if (urp != null) view.Add(Picture(urp)); break;
                case "Difference": if (diff != null) view.Add(Picture(diff)); break;
                default:
                    if (birp != null && urp != null) view.Add(new CompareView(birp, urp, diff, Loc.Get("ui.shader.compareLabels")));
                    else if (urp != null) view.Add(Picture(urp));
                    else if (birp != null) view.Add(Picture(birp));
                    break;
            }
        }

        static Label Message(string text)
        {
            var l = new Label(text);
            l.AddToClassList("urpify-manual");
            return l;
        }

        /// <summary>From captures: the before/after frames of scanned scenes that use this shader.</summary>
        void ShowCaptures()
        {
            var report = ctx.Report;
            var asset = report?.Assets.FirstOrDefault(a => a.Path == model.Path);
            var scenes = report == null ? new List<SceneVerdict>() : report.Scenes
                .Where(s => s.Viewpoints.Count > 0 && ((asset != null && asset.UsedBy.Contains(s.Path)) || s.Causes.Contains(model.Path))).ToList();
            if (scenes.Count == 0) { captures.Add(UiKit.Note(Loc.Get("ui.shader.noCaptures"))); return; }
            foreach (var s in scenes.Take(3))
            {
                var v = s.Viewpoints[0];
                captures.Add(UiKit.H2(Loc.Get("ui.shader.captureOf", System.IO.Path.GetFileNameWithoutExtension(s.Path), v.Name)));
                captures.Add(new CompareView(v.Before, v.After, v.Diff));
            }
        }

        static VisualElement Picture(Texture2D t)
        {
            var img = new Image { image = t, scaleMode = ScaleMode.ScaleToFit };
            img.AddToClassList("urpify-preview-image");
            return img;
        }

        Texture2D Keep(Texture2D t)
        {
            t.hideFlags = HideFlags.HideAndDontSave;
            textures.Add(t);
            return t;
        }

        Mesh CurrentMesh()
        {
            switch (meshIndex)
            {
                case 1: return Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                case 2: return Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                case 3: if (customMesh != null) return customMesh; goto default;
                default: return Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            }
        }

        Texture2D Render(Material mat)
        {
            if (pru == null)
            {
                pru = new PreviewRenderUtility();
                pru.camera.fieldOfView = 30f;
                pru.camera.nearClipPlane = 0.05f;
                pru.camera.farClipPlane = 100f;
                pru.camera.clearFlags = CameraClearFlags.SolidColor;
                pru.camera.backgroundColor = new Color(0.19f, 0.19f, 0.19f, 1f);
                pru.ambientColor = new Color(0.22f, 0.24f, 0.27f, 1f);
            }
            var mesh = CurrentMesh();
            var b = mesh.bounds;
            var radius = Mathf.Max(b.extents.magnitude, 0.01f);
            var dist = radius / Mathf.Sin(0.5f * pru.camera.fieldOfView * Mathf.Deg2Rad) * 1.1f;
            pru.camera.transform.position = new Vector3(0, radius * 0.35f, -dist);
            pru.camera.transform.LookAt(Vector3.zero);
            pru.camera.farClipPlane = dist + radius * 4f;
            pru.lights[0].intensity = 1f;
            pru.lights[0].color = new Color(1f, 0.957f, 0.839f);
            pru.lights[0].transform.rotation = Quaternion.Euler(40f, lightYaw, 0f);
            pru.lights[1].intensity = 0.25f;
            var rot = meshIndex == 2 ? Quaternion.Euler(-60f, 0f, 0f) : Quaternion.Euler(10f, 25f, 0f);
            var m = Matrix4x4.TRS(Vector3.zero, rot, Vector3.one) * Matrix4x4.Translate(-b.center);
            // The first preview render of a shader in URP can come out empty (black) while its variants load; the self-check
            // met the same (ShaderSelfCheck.Render). A material's first render is drawn once more and the first is dropped.
            for (int pass = warmed.Add(mat) ? 0 : 1; pass < 2; pass++)
            {
                pru.BeginStaticPreview(new Rect(0, 0, Size, Size));
                if (NeedsBackdrop(mat)) DrawBackdrop(dist, radius);
                for (int i = 0; i < mesh.subMeshCount; i++) pru.DrawMesh(mesh, m, mat, i);
                pru.Render(true);
                var tex = pru.EndStaticPreview();
                if (pass == 1) return Keep(tex);
                UnityEngine.Object.DestroyImmediate(tex);
            }
            return null;
        }

        readonly HashSet<Material> warmed = new HashSet<Material>();

        bool UrpOnly => model.UrpOnly;

        void EnsureMaterials()
        {
            if (birpMat == null)
            {
                projectShader = model.InProject ? AssetDatabase.LoadAssetAtPath<Shader>(model.Path) : null;
                originalShader = projectShader;
                string text = model.OriginalText;
                if (model.BackupOriginalText != null) { text = model.BackupOriginalText; backupId = model.BackupId; originalShader = null; }
                if (originalShader == null)
                {
                    originalShader = ShaderUtil.CreateShaderAsset(backupId != null ? RootedIncludes(text, model.Path) : text, false);
                    originalShader.hideFlags = HideFlags.HideAndDontSave;
                    ownsOriginal = true;
                }
                birpMat = new Material(originalShader) { hideFlags = HideFlags.HideAndDontSave };
                // Start from a real material of the project, so textures and colours are the ones the shader is used with.
                if (projectShader != null)
                {
                    var sample = AssetDatabase.FindAssets("t:Material", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                        .Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault(x => x != null && x.shader == projectShader);
                    if (sample != null) birpMat.CopyPropertiesFromMaterial(sample);
                }
            }
            if (urpMat == null && model.HasResult)
            {
                convertedShader = ShaderUtil.CreateShaderAsset(model.PreviewText, false);
                if (convertedShader == null) return;
                convertedShader.hideFlags = HideFlags.HideAndDontSave;
                urpMat = new Material(convertedShader) { hideFlags = HideFlags.HideAndDontSave };
                urpMat.CopyPropertiesFromMaterial(birpMat);
            }
            // No converted result (the Free package, or nothing to convert): URP shows the shader as it is, pink included
            // (TZ section 2: the Free package previews Built-in and URP as they are).
            var asIs = projectShader != null ? projectShader : originalShader;
            if (urpMat == null && !model.HasResult && asIs != null)
            {
                urpMat = new Material(asIs) { hideFlags = HideFlags.HideAndDontSave };
                urpMat.CopyPropertiesFromMaterial(birpMat);
            }
        }

        static readonly System.Text.RegularExpressions.Regex IncludeLine =
            new System.Text.RegularExpressions.Regex("(#\\s*include\\s*\")([^\"]+)(\")");

        /// <summary>
        /// A shader made from text has no folder, so its relative #include lines point nowhere: each one that names a file next
        /// to <paramref name="path"/> is written from the project root instead (the files it includes resolve their own).
        /// </summary>
        public static string RootedIncludes(string text, string path)
        {
            var folder = System.IO.Path.GetDirectoryName(path ?? "")?.Replace('\\', '/') ?? "";
            if (folder.Length == 0) return text;
            return IncludeLine.Replace(text, m =>
            {
                var written = m.Groups[2].Value.Replace('\\', '/');
                if (written.StartsWith("Assets/", StringComparison.Ordinal) || written.StartsWith("Packages/", StringComparison.Ordinal)) return m.Value;
                var rooted = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, written));
                var root = System.IO.Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
                rooted = rooted.Replace('\\', '/');
                if (!System.IO.File.Exists(rooted) || !rooted.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return m.Value;
                return m.Groups[1].Value + rooted.Substring(root.Length) + m.Groups[3].Value;
            });
        }

        /// <summary>The shader's properties; a change goes to both materials at once.</summary>
        VisualElement PropertyEditor()
        {
            var box = new VisualElement();
            box.AddToClassList("urpify-properties");
            try { EnsureMaterials(); } catch { return box; }
            void Set(Action<Material> apply)
            {
                apply(birpMat);
                if (urpMat != null) apply(urpMat);
                RequestRefresh();
            }
            foreach (var p in model.Doc.Properties)
            {
                var name = p.Name;
                if (!birpMat.HasProperty(name) || p.Attributes.Any(a => a.Trim().StartsWith("HideInInspector", StringComparison.OrdinalIgnoreCase))) continue;
                var label = string.IsNullOrEmpty(p.DisplayName) ? name : p.DisplayName;
                var type = p.Type.Trim();
                if (type.StartsWith("Color", StringComparison.OrdinalIgnoreCase))
                {
                    var f = new ColorField(label) { value = birpMat.GetColor(name), hdr = p.Attributes.Any(a => a.Trim().Equals("HDR", StringComparison.OrdinalIgnoreCase)) };
                    f.RegisterValueChangedCallback(e => Set(mt => mt.SetColor(name, e.newValue)));
                    box.Add(f);
                }
                else if (type.StartsWith("Range", StringComparison.OrdinalIgnoreCase))
                {
                    var nums = type.Substring(type.IndexOf('(') + 1).TrimEnd(')').Split(',');
                    float.TryParse(nums[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lo);
                    float hi = 1f;
                    if (nums.Length > 1) float.TryParse(nums[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out hi);
                    var f = new Slider(label, lo, hi) { value = birpMat.GetFloat(name), showInputField = true };
                    f.RegisterValueChangedCallback(e => Set(mt => mt.SetFloat(name, e.newValue)));
                    box.Add(f);
                }
                else if (type.Equals("Float", StringComparison.OrdinalIgnoreCase) || type.Equals("Int", StringComparison.OrdinalIgnoreCase))
                {
                    var f = new FloatField(label) { value = birpMat.GetFloat(name) };
                    f.RegisterValueChangedCallback(e => Set(mt => mt.SetFloat(name, e.newValue)));
                    box.Add(f);
                }
                else if (type.Equals("Integer", StringComparison.OrdinalIgnoreCase))
                {
                    var f = new IntegerField(label) { value = birpMat.GetInteger(name) };
                    f.RegisterValueChangedCallback(e => Set(mt => mt.SetInteger(name, e.newValue)));
                    box.Add(f);
                }
                else if (type.Equals("Vector", StringComparison.OrdinalIgnoreCase))
                {
                    var f = new Vector4Field(label) { value = birpMat.GetVector(name) };
                    f.RegisterValueChangedCallback(e => Set(mt => mt.SetVector(name, e.newValue)));
                    box.Add(f);
                }
                else
                {
                    var f = new ObjectField(label) { objectType = typeof(Texture), value = birpMat.GetTexture(name), allowSceneObjects = false };
                    f.RegisterValueChangedCallback(e => Set(mt => mt.SetTexture(name, e.newValue as Texture)));
                    box.Add(f);
                }
            }
            return box;
        }

        void ReleaseTextures()
        {
            foreach (var t in textures) if (t != null) UnityEngine.Object.DestroyImmediate(t);
            textures.Clear();
        }

        void DestroyConverted()
        {
            if (urpMat != null) UnityEngine.Object.DestroyImmediate(urpMat);
            if (convertedShader != null) UnityEngine.Object.DestroyImmediate(convertedShader);
            urpMat = null;
            convertedShader = null;
        }

        /// <summary>Frees the preview's materials, shaders, textures and render utility (also runs when the tab closes).</summary>
        public void Release()
        {
            ReleaseTextures();
            DestroyConverted();
            if (birpMat != null) UnityEngine.Object.DestroyImmediate(birpMat);
            if (ownsOriginal && originalShader != null) UnityEngine.Object.DestroyImmediate(originalShader);
            birpMat = null;
            originalShader = null;
            projectShader = null;
            ownsOriginal = false;
            backupId = null;
            pru?.Cleanup();
            pru = null;
            if (backdropMat != null) UnityEngine.Object.DestroyImmediate(backdropMat);
            if (backdropTex != null) UnityEngine.Object.DestroyImmediate(backdropTex);
            backdropMat = null;
            backdropTex = null;
        }

        Material backdropMat;
        Texture2D backdropTex;

        /// <summary>
        /// Transparent and GrabPass shaders are drawn over a checkerboard: on the plain background a glass sphere was a dark disk
        /// that looked the same in both halves (design review 13.10.2026, item 6). Unity's Unlit/Texture draws it in Built-in
        /// and in URP alike, so both halves get the same backdrop and the difference map still shows only the shader.
        /// </summary>
        bool NeedsBackdrop(Material mat) =>
            mat.renderQueue > (int)UnityEngine.Rendering.RenderQueue.GeometryLast
            || model.Doc.SubShaders.Any(s => s.IsTransparentQueue || s.Passes.Any(p => p.Kind == Parsing.PassKind.GrabPass));

        void DrawBackdrop(float dist, float radius)
        {
            if (backdropMat == null)
            {
                var shader = Shader.Find("Unlit/Texture");
                if (shader == null) return;
                const int n = 128, cell = 16;
                backdropTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                        px[y * n + x] = ((x / cell + y / cell) & 1) == 0 ? new Color32(205, 205, 205, 255) : new Color32(120, 120, 120, 255);
                backdropTex.SetPixels32(px);
                backdropTex.Apply();
                backdropMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, mainTexture = backdropTex };
            }
            var cam = pru.camera.transform;
            var depth = dist + radius * 2f;
            var height = 2f * depth * Mathf.Tan(0.5f * pru.camera.fieldOfView * Mathf.Deg2Rad) * 1.2f;
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            pru.DrawMesh(quad, Matrix4x4.TRS(cam.position + cam.forward * depth, cam.rotation, new Vector3(height, height, 1f)), backdropMat, 0);
        }
    }
}
