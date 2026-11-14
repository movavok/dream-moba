using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class FlagDeliveryZone : NetworkBehaviour
{
    [SerializeField] private float radius = 1.25f;
    [SerializeField] private FlagDeliveryParticles deliveryParticlesPrefab;
    [SerializeField] private SpriteRenderer indicatorRenderer;
    [SerializeField] private SpriteRenderer zoneRenderer;
    [SerializeField] private FlagBlinkSettings blinkSettings = new();
    [SerializeField] private float zoneAnimationDuration = 0.8f;

    private CircleCollider2D deliveryCollider;
    private Vector3 zoneInitialScale;
    private Color indicatorInitialColor;
    private Color zoneInitialColor;
    private float blinkPhase;
    private Coroutine zoneAnimationRoutine;
    private bool deliveryAnimating;

    public float Radius => radius;
    public Transform DeliveryPoint => transform;
    public FlagBlinkSettings BlinkSettings => blinkSettings;
    public float AnimationDuration => Mathf.Max(0.01f, zoneAnimationDuration);

    private void Awake()
    {
        deliveryCollider = GetComponent<CircleCollider2D>();
        ResolveRenderers();
        ConfigureCollider();

        if (zoneRenderer != null)
        {
            zoneInitialScale = zoneRenderer.transform.localScale;
            zoneInitialColor = zoneRenderer.color;
        }

        if (indicatorRenderer != null)
            indicatorInitialColor = indicatorRenderer.color;
    }

    private void Update()
    {
        UpdateIndicatorBlink();
    }

    private void OnValidate()
    {
        if (deliveryCollider == null)
            deliveryCollider = GetComponent<CircleCollider2D>();

        ConfigureCollider();
    }

    public void PlayDeliveryEffect(Vector3 position, Color color)
    {
        if (!IsServer)
            return;

        PlayDeliveryEffectClientRpc(position, color);
    }

    [ClientRpc]
    private void PlayDeliveryEffectClientRpc(Vector3 position, Color color)
    {
        if (deliveryParticlesPrefab == null)
        {
            Debug.LogError(
                "[FlagDeliveryZone] Delivery particles prefab is not assigned."
            );
        }
        else
        {
            FlagDeliveryParticles particles = Instantiate(
                deliveryParticlesPrefab,
                position,
                Quaternion.identity
            );
            particles.Play(position, color);
        }

        if (zoneAnimationRoutine != null)
            StopCoroutine(zoneAnimationRoutine);

        zoneAnimationRoutine = StartCoroutine(PlayZoneAnimation());
    }

    private IEnumerator PlayZoneAnimation()
    {
        float elapsed = 0f;
        float duration = AnimationDuration;
        float indicatorAlpha = indicatorRenderer != null
            ? indicatorRenderer.color.a : 0f;
        deliveryAnimating = true;

        if (zoneRenderer != null)
            zoneRenderer.enabled = true;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            if (zoneRenderer != null)
            {
                zoneRenderer.transform.localScale =
                    zoneInitialScale * (1f - progress);
                Color color = zoneInitialColor;
                color.a = zoneInitialColor.a * (1f - progress);
                zoneRenderer.color = color;
                zoneRenderer.transform.Rotate(
                    0f, 0f, 360f * Time.deltaTime / duration);
            }

            if (indicatorRenderer != null)
                SetIndicatorAlpha(indicatorAlpha * (1f - progress));

            yield return null;
        }

        if (zoneRenderer != null)
            zoneRenderer.enabled = false;
        if (indicatorRenderer != null)
            indicatorRenderer.enabled = false;
        zoneAnimationRoutine = null;
    }

    private void UpdateIndicatorBlink()
    {
        if (indicatorRenderer == null || deliveryAnimating)
            return;

        FlagCarryVisual[] carryVisuals =
            FindObjectsByType<FlagCarryVisual>();
        float nearestDistance = float.PositiveInfinity;
        bool hasCarrier = false;

        foreach (FlagCarryVisual carryVisual in carryVisuals)
        {
            if (carryVisual == null || !carryVisual.IsCarrying)
                continue;

            float distance = Vector2.Distance(
                carryVisual.transform.position,
                transform.position
            );
            if (distance < nearestDistance)
                nearestDistance = distance;

            hasCarrier = true;
        }

        if (!hasCarrier)
        {
            SetIndicatorAlpha(indicatorInitialColor.a);
            blinkPhase = 0f;
            return;
        }

        float currentBlinkSpeed =
            blinkSettings.CalculateBlinkSpeed(nearestDistance);
        blinkPhase = blinkSettings.UpdatePhase(
            blinkPhase,
            currentBlinkSpeed,
            Time.deltaTime
        );

        float alpha = blinkSettings.CalculateAlpha(blinkPhase);
        SetIndicatorAlpha(alpha);
    }

    private void SetIndicatorAlpha(float alpha)
    {
        Color color = indicatorInitialColor;
        color.a = alpha;
        indicatorRenderer.color = color;
    }

    private void ResolveRenderers()
    {
        if (indicatorRenderer == null)
        {
            Transform indicator = transform.Find("Indicator");
            if (indicator != null)
                indicatorRenderer = indicator.GetComponent<SpriteRenderer>();
        }

        if (zoneRenderer == null)
        {
            Transform zoneVisual = transform.Find("ZoneVisual");
            if (zoneVisual != null)
                zoneRenderer = zoneVisual.GetComponent<SpriteRenderer>();
        }
    }

    private void ConfigureCollider()
    {
        if (deliveryCollider == null)
            return;

        deliveryCollider.isTrigger = true;
        deliveryCollider.radius = radius;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDeliverPlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDeliverPlayer(other);
    }

    private void TryDeliverPlayer(Collider2D other)
    {
        if (!IsServer)
            return;

        FlagObject flag = other.GetComponentInParent<FlagObject>();
        if (flag != null)
        {
            FindAnyObjectByType<FlagGameManager>()?.TryCompleteThrownDelivery(
                flag, flag.transform.position, flag.transform.position);
            return;
        }

        PlayerNetwork player = other.GetComponentInParent<PlayerNetwork>();
        if (player == null)
            return;

        FlagGameManager manager = FindAnyObjectByType<FlagGameManager>();
        if (manager == null)
        {
            Debug.LogError(
                "[FlagDeliveryZone] FlagGameManager was not found."
            );
            return;
        }

        manager.TryCompleteDelivery(player);
    }
}
