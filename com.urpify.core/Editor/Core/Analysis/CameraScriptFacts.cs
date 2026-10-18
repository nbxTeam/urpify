using System;
using System.Collections.Generic;
using System.Linq;
using Urpify.Parsing;
using Urpify.Generation;

namespace Urpify.Analysis
{
    /// <summary>What the scanner and the Pro script converter both need to know about camera scripts (TZ 6.7, 6.8).</summary>
    public static class CameraScriptFacts
    {
        /// <summary>Camera events of Built-in's deferred path: URP's forward renderer has no such point.</summary>
        public static readonly HashSet<string> DeferredEvents = new HashSet<string>(StringComparer.Ordinal)
        {
            "BeforeGBuffer", "AfterGBuffer", "BeforeLighting", "AfterLighting", "BeforeFinalPass", "AfterFinalPass", "BeforeReflections", "AfterReflections",
        };

        /// <summary>Events that run around URP's depth prepass, which URP renders only when something needs it.</summary>
        public static readonly HashSet<string> DepthEvents = new HashSet<string>(StringComparer.Ordinal)
        {
            "BeforeDepthTexture", "AfterDepthTexture", "BeforeDepthNormalsTexture", "AfterDepthNormalsTexture",
        };

        /// <summary>
        /// The first immediate-mode call in the method Urpify cannot record into URP's frame, or null: GL members other
        /// than those of <see cref="RecordedGL"/>, Graphics.DrawTexture / CopyTexture / ConvertTexture.
        /// </summary>
        public static string ImmediateDrawing(CSharpFile code, CSharpFile.Method m) => Find(code, m, OwnImmediateDrawing, new HashSet<CSharpFile.Method>());

        /// <summary>
        /// The first immediate-mode call in the method (or the methods it calls) that Urpify records into URP's frame
        /// (GL quads and matrices, RenderTexture.active, Graphics.SetRenderTarget / DrawMeshNow, Material.SetPass), or
        /// null. Recorded drawing converts with a close but not guaranteed identical result.
        /// </summary>
        public static string RecordedImmediate(CSharpFile code, CSharpFile.Method m) => Find(code, m, OwnRecordedImmediate, new HashSet<CSharpFile.Method>());

        /// <summary>
        /// "Camera.Render (line N)" when the method (or a method of the same file it calls) renders a camera, or null. Under
        /// URP an image effect runs inside the frame of its own camera, where no other camera can render: a portal that
        /// renders the other side in OnRenderImage and blits it through the camera's stencil came out black (shaders-portal
        /// BasicPortals, independent test of 2026-10-12).
        /// </summary>
        public static string RendersCamera(CSharpFile code, CSharpFile.Method m)
        {
            var calls = CameraRenderCalls(code);
            if (calls.Count == 0) return null;
            return Find(code, m, (c, own) =>
            {
                foreach (var i in calls)
                    if (i > own.BodyStart && i < own.BodyEnd) return "Camera.Render (line " + c.Tokens[i].Line + ")";
                return null;
            }, new HashSet<CSharpFile.Method>());
        }

        /// <summary>
        /// Also looks into the methods of the same file the body calls, at any depth: Standard Assets' GlobalFog draws
        /// with GL in its own CustomGraphicsBlit, and run through Urpify its frame came out black (ShaderTutorials
        /// NatureStarterKit2 Demo, M6).
        /// </summary>
        static string Find(CSharpFile code, CSharpFile.Method m, Func<CSharpFile, CSharpFile.Method, string> ownCheck, HashSet<CSharpFile.Method> seen)
        {
            if (m.BodyStart < 0 || !seen.Add(m)) return null;
            var own = ownCheck(code, m);
            if (own != null) return own;
            for (int k = m.BodyStart; k + 1 < m.BodyEnd; k++)
            {
                var t = code.Tokens[k];
                if (t.Kind != CSharpFile.TokKind.Ident || !code.Tokens[k + 1].Is("(")) continue;
                if (k > 0 && code.Tokens[k - 1].Is(".") && !(k > 1 && code.Tokens[k - 2].Text == "this")) continue;
                foreach (var callee in code.Methods.Where(x => x.Name == t.Text && x.BodyStart >= 0))
                {
                    var found = Find(code, callee, ownCheck, seen);
                    if (found != null) return found + " (in " + callee.Name + ")";
                }
            }
            return null;
        }

        /// <summary>
        /// GL members Built-in's image effects draw with that Urpify records into URP's frame (ImageEffectGL), plus
        /// GetGPUProjectionMatrix, which only computes a matrix.
        /// </summary>
        public static readonly HashSet<string> RecordedGL = new HashSet<string>(StringComparer.Ordinal)
        {
            "PushMatrix", "PopMatrix", "LoadOrtho", "LoadIdentity", "LoadProjectionMatrix", "MultMatrix", "Clear", "ClearWithSkybox",
            "Begin", "End", "Color", "TexCoord", "TexCoord2", "TexCoord3", "MultiTexCoord", "MultiTexCoord2", "MultiTexCoord3", "Vertex", "Vertex3",
            "QUADS", "TRIANGLES", "TRIANGLE_STRIP", "LINES", "LINE_STRIP", "GetGPUProjectionMatrix",
        };

        /// <summary>Graphics members drawing at once that Urpify records (ImageEffectGraphics).</summary>
        public static readonly HashSet<string> RecordedGraphics = new HashSet<string>(StringComparer.Ordinal) { "DrawMeshNow", "SetRenderTarget" };

        /// <summary>The first immediate call Urpify cannot record (M8: GL quads, RenderTexture.active, SetPass ... are recorded).</summary>
        static string OwnImmediateDrawing(CSharpFile code, CSharpFile.Method m)
        {
            for (int k = m.BodyStart; k + 2 < m.BodyEnd; k++)
            {
                var t = code.Tokens[k];
                if (t.Kind != CSharpFile.TokKind.Ident || !code.Tokens[k + 1].Is(".")) continue;
                var member = code.Tokens[k + 2].Text;
                if (t.Text == "GL" && !RecordedGL.Contains(member)) return "GL." + member;
                if (t.Text == "Graphics" && (member == "DrawTexture" || member == "CopyTexture" || member == "ConvertTexture"))
                    return "Graphics." + member;
            }
            return null;
        }

        static string OwnRecordedImmediate(CSharpFile code, CSharpFile.Method m)
        {
            for (int k = m.BodyStart; k + 2 < m.BodyEnd; k++)
            {
                var t = code.Tokens[k];
                if (t.Kind == CSharpFile.TokKind.Ident && code.Tokens[k + 1].Is("."))
                {
                    var member = code.Tokens[k + 2].Text;
                    if (t.Text == "GL" && RecordedGL.Contains(member) && member != "GetGPUProjectionMatrix") return "GL." + member;
                    if (t.Text == "RenderTexture" && member == "active") return "RenderTexture.active";
                    if (t.Text == "Graphics" && RecordedGraphics.Contains(member)) return "Graphics." + member;
                }
                if (t.Text == "SetPass" && k > 0 && code.Tokens[k - 1].Is(".") && code.Tokens[k + 1].Is("(")) return "Material.SetPass";
            }
            return null;
        }

        /// <summary>Camera messages URP never sends (UrpFacts.DeadCameraCallbacks, Spikes/M1/Results).</summary>
        public static readonly HashSet<string> CameraCallbacks = new HashSet<string>(StringComparer.Ordinal) { "OnPreCull", "OnPreRender", "OnPostRender" };

        /// <summary>
        /// Token indexes of "Render" in calls x.Render() whose receiver is a Camera, as far as the file tells: a variable,
        /// field or parameter declared as Camera (or Camera[] for x[i].Render()), Camera.main / Camera.current, or
        /// GetComponent&lt;Camera&gt;() / AddComponent&lt;Camera&gt;(). A Render() of anything else (a portal, a user type) is left alone.
        /// </summary>
        public static List<int> CameraRenderCalls(CSharpFile code)
        {
            var cameras = new HashSet<string>(StringComparer.Ordinal);
            var arrays = new HashSet<string>(StringComparer.Ordinal);
            var t = code.Tokens;
            for (int k = 0; k + 1 < t.Count; k++)
            {
                if (t[k].Kind != CSharpFile.TokKind.Ident || t[k].Text != "Camera") continue;
                if (k > 0 && t[k - 1].Is(".") && !(k > 1 && t[k - 2].Text == "UnityEngine")) continue;
                int n = k + 1;
                bool array = false;
                if (n + 1 < t.Count && t[n].Is("[") && t[n + 1].Is("]")) { array = true; n += 2; }
                if (n < t.Count && t[n].Is("?")) n++;
                // Camera a, b; / Camera a = ..., b; (the declarators of one declaration)
                while (n < t.Count && t[n].Kind == CSharpFile.TokKind.Ident && !(n + 1 < t.Count && t[n + 1].Is("(")))
                {
                    (array ? arrays : cameras).Add(t[n].Text);
                    int d = 0, e = n + 1;
                    for (; e < t.Count; e++)
                    {
                        if (t[e].Is("(") || t[e].Is("[") || t[e].Is("{")) d++;
                        else if (t[e].Is(")") || t[e].Is("]") || t[e].Is("}")) { if (d == 0) break; d--; }
                        else if (d == 0 && (t[e].Is(",") || t[e].Is(";"))) break;
                    }
                    if (e < t.Count && t[e].Is(",") && e + 1 < t.Count && t[e + 1].Kind == CSharpFile.TokKind.Ident
                        && !(e + 2 < t.Count && t[e + 2].Kind == CSharpFile.TokKind.Ident)) { n = e + 1; continue; }
                    break;
                }
            }
            // var x = GetComponent<Camera>(); / var x = go.AddComponent<Camera>();
            for (int k = 0; k + 6 < t.Count; k++)
                if (t[k].Text == "var" && t[k + 1].Kind == CSharpFile.TokKind.Ident && t[k + 2].Is("="))
                {
                    int e = k + 3;
                    while (e < t.Count && !t[e].Is(";") && !(t[e].Is("<") && e + 2 < t.Count && t[e + 1].Text == "Camera" && t[e + 2].Is(">"))) e++;
                    if (e < t.Count && t[e].Is("<") && e > 0 && (t[e - 1].Text == "GetComponent" || t[e - 1].Text == "AddComponent")) cameras.Add(t[k + 1].Text);
                }

            var calls = new List<int>();
            for (int i = 2; i + 2 < t.Count; i++)
            {
                if (t[i].Text != "Render" || t[i].Kind != CSharpFile.TokKind.Ident || !t[i - 1].Is(".") || !t[i + 1].Is("(") || !t[i + 2].Is(")")) continue;
                var r = t[i - 2];
                bool camera = false;
                if (r.Kind == CSharpFile.TokKind.Ident)
                    camera = cameras.Contains(r.Text) // also other.portalCam.Render(): a field this file declares as Camera
                             || (i > 3 && t[i - 3].Is(".") && t[i - 4].Text == "Camera" && (r.Text == "main" || r.Text == "current"));
                else if (r.Is("]"))
                {
                    int d = 0, j = i - 2;
                    for (; j >= 0; j--) { if (t[j].Is("]")) d++; else if (t[j].Is("[")) { d--; if (d == 0) break; } }
                    camera = j > 0 && t[j - 1].Kind == CSharpFile.TokKind.Ident && arrays.Contains(t[j - 1].Text);
                }
                else if (r.Is(")") && i > 6 && t[i - 3].Is("(") && t[i - 4].Is(">") && t[i - 5].Text == "Camera" && t[i - 6].Is("<"))
                    camera = t[i - 7].Text == "GetComponent" || t[i - 7].Text == "AddComponent";
                if (camera) calls.Add(i);
            }
            return calls;
        }

        /// <summary>Calls named <paramref name="name"/> inside the method's body.</summary>
        public static int CountCalls(CSharpFile code, CSharpFile.Method m, string name)
        {
            int n = 0;
            for (int k = m.BodyStart; k + 1 < m.BodyEnd; k++)
                if (code.Tokens[k].Kind == CSharpFile.TokKind.Ident && code.Tokens[k].Text == name && code.Tokens[k + 1].Is("(")) n++;
            return n;
        }
    }
}
