using Unity.Netcode.Components;
using UnityEngine;

public static class GameRuntimeSettings
{
    public const int TargetFrameRate = 120;
    public const int VSyncCount = 1;
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
        // On desktop VSync takes precedence over targetFrameRate: a 144 Hz
        // display may still render at 144 FPS. These are not two combined caps.
        QualitySettings.vSyncCount = VSyncCount;
#endif
    }
}
