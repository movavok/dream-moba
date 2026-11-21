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

    [Header("Damage Screen Feedback")]
    [SerializeField, Min(1f)] private float damageForFullFeedback = 100f;
    [SerializeField, Range(0f, 0.25f)] private float maxDamageZoom = 0.065f;
    [SerializeField, Min(0f)] private float maxDamageShake = 0.15f;
    [SerializeField, Range(0f, 1f)] private float maxDamageVignette = 0.4f;
    [SerializeField, Min(0.01f)] private float damageFeedbackDuration = 0.55f;
    [SerializeField] private Color damageVignetteColor = new(0.65f, 0f, 0f, 1f);

    private Transform followedTarget;
    private Collider2D currentBounds;
    private VolumeProfile deathProfile;
    private ColorAdjustments deathColor;
    private Coroutine deathColorRoutine;
    private AudioListener cameraListener;
    private AudioListener playerListener;
    private DamageScreenVignette damageVignette;
    private DamageCameraPulse damageCameraPulse;
    private float damagePeak;
    private float damageStartedAt;

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
        // Keep the mutable profile as the Volume's actual runtime instance.
        // Accessing Volume.profile must not clone a stale shared-profile copy.
        volume.profile = deathProfile;
        deathColor = deathProfile.Add<ColorAdjustments>();
        deathColor.saturation.Override(0f);
        if (virtualCamera != null)
        {
            damageVignette = GetComponent<DamageScreenVignette>();
            if (damageVignette == null)
                damageVignette = gameObject.AddComponent<DamageScreenVignette>();
        }

        // This is an effect on the output camera, not a virtual-camera lens
        // override: the latter is overwritten by PixelPerfectCamera at render.
        damageCameraPulse = GetComponent<DamageCameraPulse>();
        if (damageCameraPulse == null)
            damageCameraPulse = gameObject.AddComponent<DamageCameraPulse>();

        if (virtualCamera != null)
        {
            Camera output = GetComponent<Camera>();
            if (output != null)
            {
                var cameraData = output.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                cameraData.volumeLayerMask |= 1 << gameObject.layer;
                output.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.EveryFrame);
            }

            if (virtualCamera.GetComponent<CinemachineImpulseListener>() == null)
            {
                var listener = virtualCamera.gameObject.AddComponent<CinemachineImpulseListener>();
                listener.Use2DDistance = true;
            }
        }

        Debug.Log(
            $"PlayerCamera Awake: {gameObject.name}, " +
            $"VirtualCamera = {virtualCamera}"
        );
    }

    public void Shake(float damage)
    {
        if (impulseSource == null && PlayerNetwork.LocalPlayer != null)
            impulseSource = PlayerNetwork.LocalPlayer.GetComponentInChildren<CinemachineImpulseSource>();
        if (impulseSource == null)
            return;

        float strength = Mathf.Clamp01(damage / Mathf.Max(1f, damageForFullFeedback)) *
            maxDamageShake;
        if (strength <= 0f)
            return;

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
        UpdateDamageScreen();
        if (followedTarget != null)
        {
            RefreshBounds(followedTarget.position);
            UpdateListenerPosition(followedTarget.position);
        }
    }

    public void ShowDamageFeedback(float damage)
    {
        if (damage <= 0f)
            return;

        Shake(damage);
        damageCameraPulse?.SubscribeAfterPixelPerfect();
        float strength = Mathf.Clamp01(damage / Mathf.Max(1f, damageForFullFeedback));
        // New hits refresh the fade without letting a weak hit cancel a strong one.
        damagePeak = Mathf.Max(CurrentDamageStrength(), strength);
        damageStartedAt = Time.unscaledTime;
        UpdateDamageScreen();
    }

    [ContextMenu("Test Damage Feedback/10 Damage (Play Mode)")]
    private void TestSmallDamageFeedback()
    {
        if (Application.isPlaying)
            ShowDamageFeedback(10f);
    }

    [ContextMenu("Test Damage Feedback/100 Damage (Play Mode)")]
    private void TestFullDamageFeedback()
    {
        if (Application.isPlaying)
            ShowDamageFeedback(100f);
    }

    private float CurrentDamageStrength()
    {
        float t = Mathf.Clamp01((Time.unscaledTime - damageStartedAt) /
            Mathf.Max(0.01f, damageFeedbackDuration));
        return damagePeak * (1f - Mathf.SmoothStep(0f, 1f, t));
    }

    private void UpdateDamageScreen()
    {
        float strength = CurrentDamageStrength();
        if (damageCameraPulse != null)
            damageCameraPulse.ZoomFraction = strength * maxDamageZoom;
        if (damageVignette != null)
            damageVignette.SetIntensity(strength * maxDamageVignette, damageVignetteColor);
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
        if (damageCameraPulse != null)
            damageCameraPulse.ZoomFraction = 0f;
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
