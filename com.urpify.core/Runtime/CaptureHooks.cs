#if UNITY_EDITOR
using UnityEngine;

namespace Urpify
{
    /// <summary>
    /// Editor-only support for Urpify's Play Mode captures. The frame time and the random seed must be fixed before the
    /// first scene of a Play Mode session loads, which only a runtime hook can do. The capture runner sets the values in
    /// SessionState for the duration of a capture; without them this does nothing.
    /// </summary>
    public static class CaptureHooks
    {
        public const string DeltaTimeKey = "Urpify.Capture.DeltaTime";
        public const string SeedKey = "Urpify.Capture.Seed";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeSceneLoad()
        {
            float dt = UnityEditor.SessionState.GetFloat(DeltaTimeKey, 0f);
            if (dt <= 0f) return;
            Time.captureDeltaTime = dt;
            Random.InitState(UnityEditor.SessionState.GetInt(SeedKey, 0));
        }
    }
}
#endif
