using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Urpify.Verify
{
    /// <summary>
    /// Self-check of a converted shader (QA-004, architect decision 4 on the QA report): a sphere with the project's own
    /// materials of the shader is rendered in Built-in before the conversion (pipeline switched off for the call, see
    /// <see cref="BuiltInRender"/>) and in URP after it, and the two are compared over the sphere. A large difference
    /// means the conversion looks different from the original, whatever the converter thought: such a shader is not
    /// written as Auto. The tester's worst cases had more than half of the sphere changed (an invisible object, a texture
    /// turned black); a faithful conversion differs by lighting only (median 1.2 % of the sphere).
    /// </summary>
    public static class ShaderSelfCheck
    {
        public const int Size = 96;
        /// <summary>Largest channel difference (0..255) above which a pixel of the sphere counts as changed.</summary>
        public static int PixelThreshold = 48;
        /// <summary>Share of the sphere's pixels changed above which the conversion "looks different", percent.</summary>
        /// <summary>
        /// Calibrated on Standard -> URP Lit, the allowed difference of TZ section 10 (2026-10-11): Unity's own converter
        /// turning a Standard material into URP Lit shifts a dark, saturated albedo as much as Urpify's conversion of a
        /// surface shader with Standard lighting does (measured side by side in one scene), and on the sphere that reaches
        /// 19.9 % (Big6P, 249 such shaders, 75 of them over the former 15 %). Real faults measured 48 to 98 % (Standard-Metallic
        /// of GLTFUtility 74 %; the tester's QA-004 cases 30 to 98 %), and pink or a hole fails on its own.
        /// </summary>
        public static double LooksDifferentPct = 25.0;
        /// <summary>Materials of one shader that are rendered (the first ones, by path).</summary>
        public const int MaxMaterials = 2;

        /// <summary>The plain background of renders compared without a backdrop.</summary>
        static readonly Color Background = new Color(0.19f, 0.19f, 0.19f, 1f);
        /// <summary>The two tones of the backdrop's checker (dark and light), so blending is seen over both.</summary>
        static readonly Color32 BackdropDark = new Color32(26, 30, 38, 255), BackdropLight = new Color32(140, 148, 158, 255);
        /// <summary>
        /// What a camera that clears to the skybox leaves where nothing draws colour: Built-in clears it to black, URP to the
        /// camera's background colour (Unity's default camera: 0.192, 0.302, 0.475). Measured with Urpify's own captures
        /// of a depth-only sphere in front of the sky (6000.5, 2026-10-11): Built-in (0, 0, 0), URP (49, 77, 121). The
        /// backdrop plays the skybox (drawn after the opaque objects in both pipelines), so the self-check sees the same.
        /// </summary>
        static readonly Color BuiltInClear = Color.black, UrpClear = new Color(0.19215687f, 0.3019608f, 0.4745098f, 1f);

        public struct Result
        {
            /// <summary>Share of the sphere's pixels whose largest channel difference is above the threshold, percent.</summary>
            public double ChangedPct;
            /// <summary>Pixels the sphere covers in either render.</summary>
            public int Covered;
            /// <summary>Share of the sphere that is the error shader's pink in URP, percent.</summary>
            public double PinkPct;
            /// <summary>
            /// Share of the sphere where both renders show their pipeline's clear colour: the object writes depth and no colour,
            /// and what shows through it differs between the pipelines, not the shader's own output. Percent.
            /// </summary>
            public double HolePct;
            /// <summary>
            /// One render of the sphere is near black where the other is not (mean brightness of the covered pixels under a
            /// quarter of the other's): a lost texture or lost lighting, whatever the moment of an animation.
            /// </summary>
            public bool WentDark;
            public bool LooksDifferent;
        }

        /// <summary>The renders of one pipeline: the backdrop alone, and the sphere over it for each material.</summary>
        public sealed class Shots
        {
            public Color32[] Reference;
            public List<Color32[]> Images = new List<Color32[]>();
        }

        /// <summary>
        /// Renders each material on the preview sphere with the active pipeline, over a checker backdrop that both pipelines
        /// draw alike (unlit, after the opaque objects, as the skybox is), with each pipeline's clear colour behind it. The
        /// backdrop shows what a flat background hides: an object that writes depth but no colour hides it and shows the clear
        /// colour, black in Built-in and the camera's background in URP, as in a scene with a sky (Valve's vr_cast_shadows,
        /// QA-004); blending is seen over a dark and a light tone; glass refracts it. Every image is rendered twice and the
        /// first one is dropped: the first preview render of a shader in URP can come out black (or red) while its variants
        /// load, which marked faithful conversions as different (Big6P, every surface shader with addshadow); captures warm up
        /// the same way (TZ 10).
        /// </summary>
        /// <param name="gameCamera">Render with a camera of type Game instead of Preview (the preview scene's camera, hidden):
        /// URP 14 makes no Opaque Texture for a preview camera (UniversalRenderer.cs, URP 14.0.12: "copyColorPass &amp;=
        /// !isPreviewCamera", isPreviewCamera being cameraType == Preview), so a converted GrabPass read nothing there. A
        /// game camera gets it from the URP Asset's setting like the preview camera of URP 17, nothing in the project changes.
        /// It runs the renderer's features too: Urpify's Built-in Camera Hooks feature then draws passes 3 and later itself.</param>
        public static Shots Render(IList<Material> materials, bool singleLight = false, bool gameCamera = false)
        {
            var shots = new Shots();
            var pru = new PreviewRenderUtility();
            Texture2D checker = null;
            Material backdrop = null, primer = null;
            Action putBack = null;
            try
            {
                if (gameCamera) pru.camera.cameraType = CameraType.Game;
                bool featureDrawsExtraPasses = gameCamera && KitFeatureDrawsExtraPasses();
                pru.camera.fieldOfView = 30f;
                pru.camera.nearClipPlane = 0.05f;
                pru.camera.clearFlags = CameraClearFlags.SolidColor;
                pru.camera.backgroundColor = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? UrpClear : BuiltInClear;
                pru.ambientColor = new Color(0.22f, 0.24f, 0.27f, 1f);
                var mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
                var radius = mesh.bounds.extents.magnitude;
                var dist = radius / Mathf.Sin(0.5f * pru.camera.fieldOfView * Mathf.Deg2Rad) * 1.1f;
                pru.camera.transform.position = new Vector3(0, radius * 0.35f, -dist);
                pru.camera.transform.LookAt(Vector3.zero);
                pru.camera.farClipPlane = dist + radius * 4f;
                pru.lights[0].intensity = 1f;
                pru.lights[0].color = new Color(1f, 0.957f, 0.839f);
                pru.lights[0].transform.rotation = Quaternion.Euler(40f, -30f, 0f);
                // A fill light, as the threshold was calibrated with (M9b): with the key light alone, Standard -> URP Lit
                // differed by 5 to 20 % on the dark side (Big6P, 72 shaders over the threshold, scenes Same). One light only
                // for a surface shader with finalcolor: Built-in runs finalcolor in its ForwardAdd pass too and adds the
                // author's colour once per additional light, the converted shader applies it once (lab S11: white against
                // blue with two lights, Same in its scene).
                if (singleLight) { pru.lights[1].intensity = 0f; pru.lights[1].enabled = false; }
                else pru.lights[1].intensity = 0.25f;
                var m = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(10f, 25f, 0f), Vector3.one);

                putBack = RequestSceneTextures(pru.camera);

                // The backdrop: a quad facing the camera behind the sphere, filling the view.
                var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var unlit = BackdropShader();
                Matrix4x4 backdropMatrix = Matrix4x4.identity;
                if (quad != null && unlit != null)
                {
                    checker = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
                    checker.SetPixels32(new[] { BackdropDark, BackdropLight, BackdropLight, BackdropDark });
                    checker.Apply();
                    backdrop = new Material(unlit) { hideFlags = HideFlags.HideAndDontSave, mainTexture = checker, mainTextureScale = new Vector2(5, 5), renderQueue = 2450 };
                    var cam = pru.camera.transform;
                    var depth = dist + radius * 2f;
                    var side = 2f * depth * Mathf.Tan(0.5f * pru.camera.fieldOfView * Mathf.Deg2Rad) * 1.5f;
                    backdropMatrix = Matrix4x4.TRS(cam.position + cam.forward * depth, Quaternion.LookRotation(cam.forward, cam.up), new Vector3(side, side, 1f));
                }
                // Built-in sets the light's values (_LightColor0, _WorldSpaceLightPos0) only for ForwardBase passes; a pass
                // without LightMode that reads them gets what the last lit object left, which in a scene is the light (the
                // floor drew first), and in a preview with one sphere nothing: it rendered black (DebugNormalsTangents,
                // RaytraceSphere) while its scene was the same. A small lit quad hidden behind the backdrop, drawn first,
                // leaves the light's values as a scene does. URP sets them through Urpify's library and needs nothing.
                Matrix4x4 primerMatrix = Matrix4x4.identity;
                if (quad != null && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null && PrimerShader() != null)
                {
                    primer = new Material(PrimerShader()) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 1000 };
                    var cam = pru.camera.transform;
                    primerMatrix = Matrix4x4.TRS(cam.position + cam.forward * (dist + radius * 3f), Quaternion.LookRotation(cam.forward, cam.up), Vector3.one * radius);
                }

                Color32[] Shot(Material mat)
                {
                    pru.BeginStaticPreview(new Rect(0, 0, Size, Size));
                    if (primer != null) pru.DrawMesh(quad, primerMatrix, primer, 0);
                    if (backdrop != null) pru.DrawMesh(quad, backdropMatrix, backdrop, 0);
                    if (mat != null) pru.DrawMesh(mesh, m, mat, 0);
                    pru.Render(true);
                    if (mat != null && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null && !featureDrawsExtraPasses) DrawExtraPasses(pru.camera, mesh, m, mat);
                    var tex = pru.EndStaticPreview();
                    var px = tex.GetPixels32();
                    UnityEngine.Object.DestroyImmediate(tex);
                    return px;
                }

                Shot(null);
                shots.Reference = Shot(null);
                foreach (var mat in materials)
                {
                    Shot(mat);
                    shots.Images.Add(Shot(mat));
                }
            }
            finally
            {
                try { putBack?.Invoke(); } catch (Exception e) { Debug.LogWarning("[Urpify] The self-check could not put the URP Asset's Depth and Opaque Texture back: " + e.Message); }
                pru.Cleanup();
                if (backdrop != null) UnityEngine.Object.DestroyImmediate(backdrop);
                if (primer != null) UnityEngine.Object.DestroyImmediate(primer);
                if (checker != null) UnityEngine.Object.DestroyImmediate(checker);
            }
            return shots;
        }

        /// <summary>
        /// The scene textures shaders read: the depth texture and, in URP, the Opaque Texture (GrabPass becomes it). Glass in
        /// Evil6P read an Opaque Texture that was not there, rendered flat and was marked different while its scene was the
        /// same. Built-in: the camera's depthTextureMode (GrabPass needs nothing). URP gives a preview camera these textures
        /// only from the active URP Asset (UniversalRenderPipeline.RenderSingleCameraInternal reads the camera's own
        /// UniversalAdditionalCameraData for game cameras only, URP 17.5.0), and the Urpify converters step turns them on there
        /// right after the self-check. So for the self-check's renders both are on in the asset, then put back as they were;
        /// the asset is not left marked as changed. Returns how to put them back.
        /// </summary>
        static Action RequestSceneTextures(Camera cam)
        {
            var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (asset == null) { cam.depthTextureMode |= DepthTextureMode.Depth; return () => { }; }
            var wasDirty = EditorUtility.IsDirty(asset);
            var restore = new List<Action>();
            foreach (var name in new[] { "supportsCameraOpaqueTexture", "supportsCameraDepthTexture" })
            {
                var p = asset.GetType().GetProperty(name);
                if (p == null || !p.CanWrite || p.PropertyType != typeof(bool)) continue;
                var old = (bool)p.GetValue(asset);
                if (old) continue;
                p.SetValue(asset, true);
                restore.Add(() => p.SetValue(asset, old));
            }
            return () =>
            {
                foreach (var r in restore) r();
                if (restore.Count > 0 && !wasDirty) EditorUtility.ClearDirty(asset);
            };
        }

        /// <summary>
        /// The active URP Asset's default renderer has Urpify's Built-in Camera Hooks feature with its extra passes on: a
        /// game camera then gets passes 3 and later from it (the feature skips preview cameras).
        /// </summary>
        static bool KitFeatureDrawsExtraPasses()
        {
            try
            {
                var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                if (asset == null) return false;
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                var data = asset.GetType().GetProperty("scriptableRendererData", flags)?.GetValue(asset);
                if (!(data?.GetType().GetProperty("rendererFeatures", flags)?.GetValue(data) is System.Collections.IEnumerable features)) return false;
                foreach (var f in features)
                {
                    if (f == null || f.GetType().Name != "BuiltInCameraHooksFeature") continue;
                    if (f.GetType().GetProperty("isActive", flags)?.GetValue(f) is bool active && !active) continue;
                    if (f.GetType().GetField("extraPasses", flags)?.GetValue(f) is bool on && on) return true;
                }
            }
            catch { }
            return false;
        }

        static readonly System.Text.RegularExpressions.Regex FinalColor =
            new System.Text.RegularExpressions.Regex(@"#\s*pragma\s+surface\b[^\r\n]*\bfinalcolor\s*:");

        /// <summary>The shader is a surface shader with finalcolor: the self-check lights it with one light (see <see cref="Render"/>).</summary>
        public static bool HasFinalColor(string shaderText) => shaderText != null && FinalColor.IsMatch(shaderText);

        static Shader s_Backdrop, s_Primer;
        static bool s_PrimerTried;

        /// <summary>A ForwardBase pass that only makes Built-in set the light's values (see <see cref="Render"/>).</summary>
        static Shader PrimerShader()
        {
            if (s_Primer != null || s_PrimerTried) return s_Primer;
            s_PrimerTried = true;
            var source =
                "Shader \"Hidden/Urpify/Self-check light\" {\n" +
                " SubShader { Tags { \"RenderType\"=\"Opaque\" }\n" +
                "  Pass { Tags { \"LightMode\"=\"ForwardBase\" }\nCGPROGRAM\n#pragma vertex vert\n#pragma fragment frag\n#include \"UnityCG.cginc\"\n" +
                "float4 vert(float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }\n" +
                "fixed4 frag() : SV_Target { return fixed4(0, 0, 0, 1); }\nENDCG\n  }\n }\n}\n";
            try
            {
                var shader = ShaderUtil.CreateShaderAsset(source, false);
                if (shader != null && !ShaderUtil.ShaderHasError(shader)) { shader.hideFlags = HideFlags.HideAndDontSave; s_Primer = shader; }
                else if (shader != null) UnityEngine.Object.DestroyImmediate(shader);
            }
            catch { }
            return s_Primer;
        }

        static readonly System.Text.RegularExpressions.Regex TimeUse =
            new System.Text.RegularExpressions.Regex(@"\b(_Time|_SinTime|_CosTime|unity_DeltaTime)\b");
        static readonly System.Text.RegularExpressions.Regex IncludeLine =
            new System.Text.RegularExpressions.Regex(@"#\s*include\s+""([^""]+)""");

        /// <summary>
        /// The shader file, or a project file it includes, reads the time (_Time and the like): it animates, and its Built-in
        /// render (made before the conversion) and URP render (after) show different moments. Such a pair is only trusted
        /// for what no moment explains: pink, or one render going dark. Compare scenes captures with a fixed time (TZ 10).
        /// </summary>
        public static bool UsesTime(string shaderPath)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool Visit(string path, int depth)
            {
                if (depth > 8 || !seen.Add(path) || !System.IO.File.Exists(path)) return false;
                string text;
                try { text = System.IO.File.ReadAllText(path); } catch { return false; }
                if (TimeUse.IsMatch(text)) return true;
                var dir = System.IO.Path.GetDirectoryName(path) ?? "";
                foreach (System.Text.RegularExpressions.Match m in IncludeLine.Matches(text))
                {
                    var inc = m.Groups[1].Value;
                    var full = inc.StartsWith("Assets/", StringComparison.Ordinal) ? inc : System.IO.Path.Combine(dir, inc).Replace('\\', '/');
                    if (Visit(full, depth + 1)) return true;
                }
                return false;
            }
            return Visit(shaderPath, 0);
        }

        /// <summary>
        /// The backdrop's shader: unlit, with a ShadowCaster pass (Built-in renders the depth texture from it) and a
        /// DepthOnly pass (URP's depth prepass), so the backdrop is in the depth texture of both pipelines alike. Unlit/Texture
        /// when it cannot be made.
        /// </summary>
        static Shader BackdropShader()
        {
            if (s_Backdrop != null) return s_Backdrop;
            const string common = "#include \"UnityCG.cginc\"\nfloat4 vert(float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }\n";
            var source =
                "Shader \"Hidden/Urpify/Self-check backdrop\" {\n" +
                " Properties { _MainTex (\"Texture\", 2D) = \"white\" {} }\n" +
                " SubShader { Tags { \"RenderType\"=\"Opaque\" }\n" +
                "  Pass {\nCGPROGRAM\n#pragma vertex v\n#pragma fragment f\n#include \"UnityCG.cginc\"\nsampler2D _MainTex; float4 _MainTex_ST;\n" +
                "struct o { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };\n" +
                "o v(appdata_base i) { o r; r.pos = UnityObjectToClipPos(i.vertex); r.uv = TRANSFORM_TEX(i.texcoord, _MainTex); return r; }\n" +
                "fixed4 f(o i) : SV_Target { return tex2D(_MainTex, i.uv); }\nENDCG\n  }\n" +
                "  Pass { Tags { \"LightMode\"=\"ShadowCaster\" } ZWrite On ColorMask 0\nCGPROGRAM\n#pragma vertex vert\n#pragma fragment frag\n" + common +
                "fixed4 frag() : SV_Target { return 0; }\nENDCG\n  }\n" +
                "  Pass { Tags { \"LightMode\"=\"DepthOnly\" } ZWrite On ColorMask R\nCGPROGRAM\n#pragma vertex vert\n#pragma fragment frag\n" + common +
                "float4 frag(float4 p : SV_POSITION) : SV_Target { return p.z; }\nENDCG\n  }\n" +
                " }\n}\n";
            try
            {
                var shader = ShaderUtil.CreateShaderAsset(source, false);
                if (shader != null && !ShaderUtil.ShaderHasError(shader)) { shader.hideFlags = HideFlags.HideAndDontSave; s_Backdrop = shader; }
                else if (shader != null) UnityEngine.Object.DestroyImmediate(shader);
            }
            catch { }
            return s_Backdrop != null ? s_Backdrop : Shader.Find("Unlit/Texture");
        }

        /// <summary>
        /// Passes 3 and later of a converted shader (LightMode UrpifyPass3..): in a game Urpify's renderer feature draws them
        /// after URP's objects; a preview camera does not run renderer features, so they are drawn here, in order, over it.
        /// A command buffer with the camera's view and projection draws them where the camera drew the sphere (drawing with
        /// GL matrices put them off the sphere and marked the fur of Fur6P as different).
        /// </summary>
        const string ExtraPassTag = "UrpifyPass";

        static void DrawExtraPasses(Camera cam, Mesh mesh, Matrix4x4 model, Material mat)
        {
            var tag = new UnityEngine.Rendering.ShaderTagId("LightMode");
            var passes = new List<(int order, int index)>();
            for (int i = 0; i < mat.passCount; i++)
            {
                var lm = mat.shader.FindPassTagValue(i, tag).name ?? "";
                if (lm.StartsWith(ExtraPassTag, StringComparison.Ordinal) && int.TryParse(lm.Substring(ExtraPassTag.Length), out var n)) passes.Add((n, i));
            }
            if (passes.Count == 0 || cam.targetTexture == null) return;
            var cmd = new UnityEngine.Rendering.CommandBuffer { name = "Urpify self-check: passes 3 and later" };
            try
            {
                cmd.SetRenderTarget(cam.targetTexture);
                cmd.SetViewProjectionMatrices(cam.worldToCameraMatrix, cam.projectionMatrix);
                foreach (var (_, index) in passes.OrderBy(p => p.order)) cmd.DrawMesh(mesh, model, mat, 0, index);
                Graphics.ExecuteCommandBuffer(cmd);
            }
            finally { cmd.Release(); }
        }

        static Color32[] SolidBackground(int length)
        {
            var px = new Color32[length];
            for (int i = 0; i < length; i++) px[i] = (Color32)Background;
            return px;
        }

        /// <summary>Compares two renders made over the plain background (no backdrop).</summary>
        public static Result Compare(Color32[] birp, Color32[] urp) =>
            birp == null || urp == null ? new Result() : Compare(SolidBackground(birp.Length), birp, SolidBackground(urp.Length), urp);

        /// <summary>
        /// Compares a Built-in and a URP render of the same material over what the sphere covers in either one: a pixel is
        /// covered where a render differs from the backdrop alone in the same pipeline.
        /// </summary>
        public static Result Compare(Color32[] birpReference, Color32[] birp, Color32[] urpReference, Color32[] urp)
        {
            var r = new Result();
            if (birp == null || urp == null || birp.Length != urp.Length || birpReference == null || urpReference == null
                || birpReference.Length != birp.Length || urpReference.Length != urp.Length) return r;
            bool Covers(Color32 c, Color32 bg) => Math.Abs(c.r - bg.r) > 8 || Math.Abs(c.g - bg.g) > 8 || Math.Abs(c.b - bg.b) > 8;
            Color32 birpClear = BuiltInClear, urpClear = UrpClear;
            int changed = 0, pink = 0, hole = 0;
            double lumA = 0, lumB = 0;
            for (int i = 0; i < birp.Length; i++)
            {
                var a = birp[i]; var b = urp[i];
                if (!Covers(a, birpReference[i]) && !Covers(b, urpReference[i])) continue;
                r.Covered++;
                lumA += a.r + a.g + a.b; lumB += b.r + b.g + b.b;
                if (ImageCompare.IsPink(b) && !ImageCompare.IsPink(a)) pink++;
                if (!Covers(a, birpClear) && !Covers(b, urpClear)) hole++;
                int d = Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b)));
                if (d > PixelThreshold) changed++;
            }
            r.ChangedPct = r.Covered == 0 ? 0 : 100.0 * changed / r.Covered;
            r.PinkPct = r.Covered == 0 ? 0 : 100.0 * pink / r.Covered;
            r.HolePct = r.Covered == 0 ? 0 : 100.0 * hole / r.Covered;
            if (r.Covered > 0)
            {
                lumA /= 3.0 * r.Covered; lumB /= 3.0 * r.Covered;
                r.WentDark = Math.Max(lumA, lumB) > 40 && Math.Min(lumA, lumB) < 0.25 * Math.Max(lumA, lumB);
            }
            // A converted shader that renders pink never passes, however small the sphere's share (Mochie Underwater Visuals).
            r.LooksDifferent = r.ChangedPct > LooksDifferentPct || r.PinkPct > 0.5;
            return r;
        }

        /// <summary>The worst of several material comparisons.</summary>
        public static Result Worst(IEnumerable<Result> results) =>
            results.DefaultIfEmpty(new Result()).OrderByDescending(x => x.ChangedPct).First();

        /// <summary>Saves a Built-in | URP pair side by side (for the report and for people), PNG.</summary>
        public static void SavePair(Color32[] birp, Color32[] urp, string path)
        {
            var tex = new Texture2D(Size * 2, Size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            try
            {
                tex.SetPixels32(0, 0, Size, Size, birp);
                tex.SetPixels32(Size, 0, Size, Size, urp);
                tex.Apply();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }
    }
}
