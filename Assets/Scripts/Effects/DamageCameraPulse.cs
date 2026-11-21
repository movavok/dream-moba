using UnityEngine;
using UnityEngine.Rendering;

// PixelPerfectCamera resets orthographicSize at beginCameraRendering, after
// Cinemachine's pipeline. Apply the transient zoom after that reset, then restore
// the lens so repeated renders/camera cuts never accumulate a scale change.
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class DamageCameraPulse : MonoBehaviour
{
    public float ZoomFraction { get; set; }

    private Camera output;
    private float originalOrthoSize;
    private float originalFieldOfView;
    private bool applied;

    private void Awake()
    {
        output = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        SubscribeAfterPixelPerfect();
    }

    private void Start()
    {
        // All scene OnEnable callbacks (including PixelPerfectCamera) have run.
        SubscribeAfterPixelPerfect();
    }

    public void SubscribeAfterPixelPerfect()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
    }

    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != output || ZoomFraction <= 0f)
            return;

        originalOrthoSize = camera.orthographicSize;
        originalFieldOfView = camera.fieldOfView;
        float scale = 1f - Mathf.Clamp(ZoomFraction, 0f, 0.25f);
        if (camera.orthographic)
            camera.orthographicSize = Mathf.Max(0.01f, originalOrthoSize * scale);
        else
            camera.fieldOfView = Mathf.Clamp(originalFieldOfView * scale, 1f, 179f);
        applied = true;
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == output)
            RestoreLens();
    }

    private void RestoreLens()
    {
        if (!applied || output == null)
            return;
        output.orthographicSize = originalOrthoSize;
        output.fieldOfView = originalFieldOfView;
        applied = false;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        RestoreLens();
    }
}
