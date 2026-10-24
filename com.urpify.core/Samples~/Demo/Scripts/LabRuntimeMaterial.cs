using UnityEngine;

// Material created from code by shader name. No converter sees this: it is not an asset.
namespace Urpify.Demo
{
    [ExecuteAlways, RequireComponent(typeof(Renderer))]
    public class LabRuntimeMaterial : MonoBehaviour
    {
        public Texture albedo;
        public Color tint = new Color(0.4f, 0.9f, 0.5f);

        void OnEnable()
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.name = "Runtime Standard";
            mat.color = tint;
            mat.SetTexture("_MainTex", albedo);
            mat.SetFloat("_Glossiness", 0.7f);
            mat.SetFloat("_Metallic", 0.3f);
            mat.hideFlags = HideFlags.DontSave;
            GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
