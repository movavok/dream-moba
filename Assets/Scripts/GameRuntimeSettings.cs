using Unity.Netcode.Components;
using UnityEngine;

public static class GameRuntimeSettings
{
    public const int TargetFrameRate = 120;
    public const int VSyncCount = 0;
    public const int InterpolationBufferTicks = 2;

    // Runs after NGO resets its statics, including when domain reload is off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        // Leave two additional snapshots of headroom for uneven delivery.
        // This applies consistently to all interpolated world NetworkTransforms.
        NetworkTransform.InterpolationBufferTickOffset = InterpolationBufferTicks;

#if !UNITY_SERVER
        Application.targetFrameRate = TargetFrameRate;
        // Keep network/input processing off the display's VSync wait. The FPS
        // target provides the cap instead of requesting uncapped rendering.
        QualitySettings.vSyncCount = VSyncCount;
#endif
    }
}
