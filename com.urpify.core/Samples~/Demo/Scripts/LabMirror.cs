using System.Collections.Generic;
using UnityEngine;

// Lab booth M01 (Urpify tests): a planar mirror written the way MirrorReflection (Unify wiki) and Standard
// Assets Water do it: OnWillRenderObject, Camera.current, a reflection camera with an oblique near plane, Camera.Render.
[ExecuteInEditMode]
public class LabMirror : MonoBehaviour
{
    public int textureSize = 512;
    public float clipPlaneOffset = 0.07f;
    public LayerMask reflectLayers = -1;

    readonly Dictionary<Camera, Camera> reflectionCameras = new Dictionary<Camera, Camera>();
    RenderTexture reflectionTexture;
    static bool insideRendering;

    public void OnWillRenderObject()
    {
        var rend = GetComponent<Renderer>();
        if (!enabled || !rend || !rend.sharedMaterial || !rend.enabled) return;
        Camera cam = Camera.current;
        if (!cam || insideRendering) return;
        insideRendering = true;

        Camera reflectionCamera = GetReflectionCamera(cam);
        Vector3 pos = transform.position;
        Vector3 normal = transform.up;
        float d = -Vector3.Dot(normal, pos) - clipPlaneOffset;
        Vector4 reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);
        Matrix4x4 reflection = Reflection(reflectionPlane);
        Vector3 oldpos = cam.transform.position;
        reflectionCamera.worldToCameraMatrix = cam.worldToCameraMatrix * reflection;
        Vector4 clipPlane = CameraSpacePlane(reflectionCamera, pos, normal, 1.0f);
        reflectionCamera.projectionMatrix = cam.CalculateObliqueMatrix(clipPlane);
        reflectionCamera.cullingMask = ~(1 << 4) & reflectLayers.value;
        reflectionCamera.targetTexture = reflectionTexture;
        GL.invertCulling = true;
        reflectionCamera.transform.position = reflection.MultiplyPoint(oldpos);
        Vector3 euler = cam.transform.eulerAngles;
        reflectionCamera.transform.eulerAngles = new Vector3(-euler.x, euler.y, euler.z);
        reflectionCamera.Render();
        GL.invertCulling = false;
        rend.sharedMaterial.SetTexture("_ReflectionTex", reflectionTexture);
        insideRendering = false;
    }

    void OnDisable()
    {
        if (reflectionTexture) { DestroyImmediate(reflectionTexture); reflectionTexture = null; }
        foreach (var kv in reflectionCameras) if (kv.Value) DestroyImmediate(kv.Value.gameObject);
        reflectionCameras.Clear();
    }

    Camera GetReflectionCamera(Camera current)
    {
        if (!reflectionTexture)
        {
            reflectionTexture = new RenderTexture(textureSize, textureSize, 16) { isPowerOfTwo = true, hideFlags = HideFlags.DontSave };
        }
        if (!reflectionCameras.TryGetValue(current, out var reflectionCamera) || !reflectionCamera)
        {
            var go = new GameObject("LabMirror camera for " + current.name, typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
            reflectionCamera = go.GetComponent<Camera>();
            reflectionCamera.enabled = false;
            reflectionCameras[current] = reflectionCamera;
        }
        reflectionCamera.clearFlags = current.clearFlags;
        reflectionCamera.backgroundColor = current.backgroundColor;
        reflectionCamera.farClipPlane = current.farClipPlane;
        reflectionCamera.nearClipPlane = current.nearClipPlane;
        reflectionCamera.orthographic = current.orthographic;
        reflectionCamera.fieldOfView = current.fieldOfView;
        reflectionCamera.aspect = current.aspect;
        return reflectionCamera;
    }

    Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
    {
        Vector3 offsetPos = pos + normal * clipPlaneOffset;
        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cpos = m.MultiplyPoint(offsetPos);
        Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;
        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
    }

    static Matrix4x4 Reflection(Vector4 p)
    {
        var m = Matrix4x4.identity;
        m.m00 = 1F - 2F * p[0] * p[0]; m.m01 = -2F * p[0] * p[1]; m.m02 = -2F * p[0] * p[2]; m.m03 = -2F * p[3] * p[0];
        m.m10 = -2F * p[1] * p[0]; m.m11 = 1F - 2F * p[1] * p[1]; m.m12 = -2F * p[1] * p[2]; m.m13 = -2F * p[3] * p[1];
        m.m20 = -2F * p[2] * p[0]; m.m21 = -2F * p[2] * p[1]; m.m22 = 1F - 2F * p[2] * p[2]; m.m23 = -2F * p[3] * p[2];
        m.m30 = 0F; m.m31 = 0F; m.m32 = 0F; m.m33 = 1F;
        return m;
    }
}
