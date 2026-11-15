using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlayerCamera : MonoBehaviour
{
    public static PlayerCamera Instance { get; private set; }

    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [SerializeField] private CinemachineConfiner2D confiner;
    [SerializeField] private Collider2D mapBounds;
    [SerializeField, Min(0f)] private float deathColorFadeDuration = 0.5f;

    private Transform followedTarget;
    private Collider2D currentBounds;
    private VolumeProfile deathProfile;
    private ColorAdjustments deathColor;
    private Coroutine deathColorRoutine;
    private AudioListener cameraListener;
    private AudioListener playerListener;

    public bool IsReady => virtualCamera != null;
    public bool IsInBuilding => followedTarget != null &&
        CameraZone.BoundsAt(followedTarget.position) != null;

    public bool TryGetWorldViewRect(out Rect view)
    {
        view = default;
        Camera output = GetComponent<Camera>();
        if (output == null || !output.isActiveAndEnabled)
            return false;

        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        for (int i = 0; i < 4; i++)
        {
            Ray ray = output.ViewportPointToRay(new Vector3(i & 1, i >> 1, 0));
            if (!plane.Raycast(ray, out float distance))
                return false;

            Vector3 point = ray.GetPoint(distance);
            minX = Mathf.Min(minX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxX = Mathf.Max(maxX, point.x);
            maxY = Mathf.Max(maxY, point.y);
        }

        view = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    private void Awake()
    {
        Instance = this;
        cameraListener = GetComponent<AudioListener>();

        Volume volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        deathProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = deathProfile;
        deathColor = deathProfile.Add<ColorAdjustments>();
        deathColor.saturation.Override(0f);

        Debug.Log(
            $"PlayerCamera Awake: {gameObject.name}, " +
            $"VirtualCamera = {virtualCamera}"
        );
    }

    public void Shake(float damage)
    {
        if (impulseSource == null)
            return;

        float strength = Mathf.Clamp(damage / 100f, 0.005f, 0.025f);

        float angle = Random.Range(0f, Mathf.PI * 2f);

        Vector3 direction = new Vector3(
            Mathf.Cos(angle),
            Mathf.Sin(angle),
            0f
        );

        impulseSource.GenerateImpulse(direction * strength);
    }

    public void SetImpulseSource(CinemachineImpulseSource source)
    {
        impulseSource = source;
    }

    public void SetMapBounds()
    {
        SetCameraBounds(mapBounds);
    }

    public void SetCameraBounds(Collider2D bounds)
    {
        if (confiner == null || currentBounds == bounds)
            return;

        confiner.BoundingShape2D = bounds;
        confiner.InvalidateBoundingShapeCache();
        currentBounds = bounds;
    }

    private void LateUpdate()
    {
        if (followedTarget != null)
        {
            RefreshBounds(followedTarget.position);
            UpdateListenerPosition(followedTarget.position);
        }
    }

    private void UpdateListenerPosition(Vector3 position)
    {
        if (playerListener == null)
        {
            GameObject listenerObject = new GameObject("Player Audio Listener");
            listenerObject.transform.position = position;
            listenerObject.transform.rotation = transform.rotation;

            if (cameraListener != null)
                cameraListener.enabled = false;

            playerListener = listenerObject.AddComponent<AudioListener>();
        }

        playerListener.transform.SetPositionAndRotation(position, transform.rotation);
    }

    private void RefreshBounds(Vector3 position)
    {
        Collider2D bounds = CameraZone.BoundsAt(position);
        SetCameraBounds(bounds != null ? bounds : mapBounds);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (playerListener != null)
            Destroy(playerListener.gameObject);

        if (deathProfile != null)
            Destroy(deathProfile);
    }

    public void SetDeathGrayscale(bool dead)
    {
        if (deathColor == null)
            return;

        if (deathColorRoutine != null)
            StopCoroutine(deathColorRoutine);

        float target = dead ? -100f : 0f;
        if (Mathf.Approximately(deathColor.saturation.value, target))
        {
            deathColor.saturation.value = target;
            deathColorRoutine = null;
            return;
        }

        deathColorRoutine = StartCoroutine(FadeDeathColor(target));
    }

    private IEnumerator FadeDeathColor(float target)
    {
        float start = deathColor.saturation.value;
        float elapsed = 0f;
        float duration = Mathf.Max(0.001f, deathColorFadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            deathColor.saturation.value = Mathf.Lerp(start, target, progress);
            yield return null;
        }

        deathColor.saturation.value = target;
        deathColorRoutine = null;
    }

    public void FollowPlayer(Transform target)
    {
        TeleportToPlayer(target);
    }

    public void MoveToPosition(Vector3 position)
    {
        if (virtualCamera == null)
        {
            Debug.LogError(
                "PlayerCamera: CinemachineCamera is not assigned!"
            );
            return;
        }

        virtualCamera.Follow = null;
        followedTarget = null;
        RefreshBounds(position);
        UpdateListenerPosition(position);
        TeleportToPosition(position);
    }

    public void TeleportToPlayer(Transform target)
    {
        if (virtualCamera == null || target == null)
            return;

        followedTarget = target;
        virtualCamera.Follow = target;
        RefreshBounds(target.position);
        UpdateListenerPosition(target.position);

        CinemachineFollow follow = virtualCamera.GetComponent<CinemachineFollow>();
        Vector3 offset = follow != null
            ? follow.FollowOffset : new Vector3(0f, 0f, -10f);
        SnapCamera(target.position + offset);
    }

    public void TeleportToPosition(Vector3 position)
    {
        if (virtualCamera == null)
            return;

        SnapCamera(new Vector3(
            position.x, position.y, virtualCamera.transform.position.z));
    }

    private void SnapCamera(Vector3 position)
    {
        virtualCamera.ForceCameraPosition(position, virtualCamera.transform.rotation);
        virtualCamera.PreviousStateIsValid = false;

        // Don't show a frame travelling from the old camera location while the
        // CinemachineBrain picks up the new target after a network teleport.
        Camera output = Camera.main;
        if (output != null)
            output.transform.position = position;
    }
}
