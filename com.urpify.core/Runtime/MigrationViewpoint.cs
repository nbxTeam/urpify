using UnityEngine;

namespace Urpify
{
    /// <summary>
    /// An extra point of view for the before/after check. The main camera renders one more frame from this transform.
    /// The object is tagged EditorOnly, so it is stripped from player builds.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/Migration Viewpoint")]
    public sealed class MigrationViewpoint : MonoBehaviour
    {
        [Tooltip("Vertical field of view for this viewpoint. 0 keeps the main camera's field of view.")]
        [Range(0f, 179f)]
        public float fieldOfView;

        [Tooltip("Far clip plane for this viewpoint, to leave out distant objects that belong to other views. 0 keeps the main camera's.")]
        [Min(0f)]
        public float farClipPlane;

        void Reset() => MarkEditorOnly();
        void OnValidate() => MarkEditorOnly();

        void MarkEditorOnly()
        {
            if (!gameObject.CompareTag("EditorOnly")) gameObject.tag = "EditorOnly";
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawFrustum(Vector3.zero, fieldOfView > 0 ? fieldOfView : 60f, 1.5f, 0.1f, 16f / 9f);
        }
    }
}
