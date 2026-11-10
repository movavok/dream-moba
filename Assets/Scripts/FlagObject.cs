using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class FlagObject : NetworkBehaviour, IInteractable, ICarriableItem
{
    private const ulong NoCarrier = ulong.MaxValue;

    [SerializeField] private SpriteRenderer branchRenderer;
    [SerializeField] private SpriteRenderer clothRenderer;
    [SerializeField] private string interactionVerb = "Take";
    [SerializeField] private string interactionName = "flag";
    [SerializeField] private InteractionOutline interactionOutline;

    private readonly NetworkVariable<bool> pickedUp =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    private readonly NetworkVariable<ulong> carrierClientId =
        new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    private readonly NetworkVariable<Color> networkClothColor =
        new NetworkVariable<Color>(
            Color.white,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    private readonly NetworkVariable<bool> isFlying =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private Collider2D interactionCollider;
    private Coroutine carrierRefreshRoutine;
    private Coroutine throwRoutine;
    private Health carrierHealth;
    private Vector3 defaultScale;
    private Quaternion defaultRotation;

    public string InteractionVerb => interactionVerb;
    public string InteractionName => interactionName;
    public Transform InteractionPromptTarget => transform;

    public bool CanInteract(PlayerNetwork player)
    {
        return player != null &&
            player.IsSpawned &&
            !pickedUp.Value &&
            !isFlying.Value;
    }

    public bool IsCarriedBy(PlayerNetwork player)
    {
        return IsServer &&
            player != null &&
            pickedUp.Value &&
            carrierClientId.Value == player.OwnerClientId;
    }

    public void Throw(PlayerNetwork player, Vector2 direction)
    {
        if (!IsServer || !IsCarriedBy(player))
            return;

        UnsubscribeFromCarrierDeath();
        pickedUp.Value = false;
        carrierClientId.Value = NoCarrier;
        isFlying.Value = true;

        transform.position = player.transform.position +
            (Vector3)(direction * 0.25f);
        transform.rotation = Quaternion.identity;

        if (throwRoutine != null)
            StopCoroutine(throwRoutine);

        throwRoutine = StartCoroutine(
            ThrowRoutine(direction)
        );
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (branchRenderer == null ||
            clothRenderer == null)
            FindRenderers();

        interactionCollider = GetComponent<Collider2D>();
        defaultScale = transform.localScale;
        defaultRotation = transform.rotation;

        pickedUp.OnValueChanged += OnPickedUpChanged;
        carrierClientId.OnValueChanged += OnCarrierChanged;
        networkClothColor.OnValueChanged += OnClothColorChanged;
        isFlying.OnValueChanged += OnFlyingChanged;

        if (interactionOutline == null)
            interactionOutline = GetComponent<InteractionOutline>();

        if (interactionOutline == null)
            interactionOutline = gameObject.AddComponent<InteractionOutline>();

        interactionOutline.Configure(branchRenderer, clothRenderer);
        ApplyClothColor(networkClothColor.Value);
        ApplyPickedUpState(pickedUp.Value);
        ApplyFlyingState(isFlying.Value);
    }

    private void Update()
    {
        if (IsServer && pickedUp.Value)
            CheckCarrier();
    }

    public void SetHighlighted(bool highlighted)
    {
        if (pickedUp.Value)
            return;

        interactionOutline?.SetColor(networkClothColor.Value);
        interactionOutline?.SetHighlighted(highlighted);
    }

    public void Interact(PlayerNetwork player)
    {
        if (!IsServer || !CanInteract(player))
            return;

        Team team = player.GetComponent<Team>();

        if (team != null)
            networkClothColor.Value = team.TeamColor;

        carrierClientId.Value = player.OwnerClientId;
        SubscribeToCarrierDeath(player.GetComponent<Health>());
        pickedUp.Value = true;

        Debug.Log(
            $"[FlagObject] Picked up by client " +
            $"{player.OwnerClientId}."
        );
    }

    public override void OnNetworkDespawn()
    {
        HideCarrierVisual(carrierClientId.Value);

        if (carrierRefreshRoutine != null)
            StopCoroutine(carrierRefreshRoutine);
        if (throwRoutine != null)
            StopCoroutine(throwRoutine);

        pickedUp.OnValueChanged -= OnPickedUpChanged;
        carrierClientId.OnValueChanged -= OnCarrierChanged;
        networkClothColor.OnValueChanged -= OnClothColorChanged;
        isFlying.OnValueChanged -= OnFlyingChanged;
        UnsubscribeFromCarrierDeath();
        base.OnNetworkDespawn();
    }

    private void CheckCarrier()
    {
        if (carrierClientId.Value == ulong.MaxValue)
            return;

        NetworkObject playerObject =
            FindPlayerObject(carrierClientId.Value);

        if (playerObject == null)
        {
            Drop();
            return;
        }

        Health health = playerObject.GetComponent<Health>();
        if (health != null && !health.IsAlive())
            Drop();
    }

    private void OnPickedUpChanged(bool oldValue, bool newValue)
    {
        ApplyPickedUpState(newValue);
    }

    private void OnCarrierChanged(ulong oldValue, ulong newValue)
    {
        HideCarrierVisual(oldValue);
        ApplyCarrierVisual(newValue);
        StartCarrierRefresh();
    }

    private void OnClothColorChanged(Color oldValue, Color newValue)
    {
        ApplyClothColor(newValue);
        interactionOutline?.SetColor(newValue);
        ApplyCarrierClothColor(newValue);
    }

    private void OnFlyingChanged(bool oldValue, bool newValue)
    {
        ApplyFlyingState(newValue);
    }

    private void ApplyFlyingState(bool flying)
    {
        if (interactionCollider != null)
            interactionCollider.enabled = !flying && !pickedUp.Value;

        transform.localScale = flying
            ? defaultScale * 0.8f
            : defaultScale;
    }

    private void ApplyPickedUpState(bool isPickedUp)
    {
        if (branchRenderer != null)
            branchRenderer.enabled = !isPickedUp;

        if (clothRenderer != null)
            clothRenderer.enabled = !isPickedUp;

        if (interactionCollider != null)
            interactionCollider.enabled = !isPickedUp && !isFlying.Value;

        if (isPickedUp)
        {
            interactionOutline?.SetHighlighted(false);
            ApplyCarrierVisual(carrierClientId.Value);
            StartCarrierRefresh();
        }
        else
        {
            HideCarrierVisual(carrierClientId.Value);
            UnsubscribeFromCarrierDeath();
        }
    }

    private void ApplyClothColor(Color color)
    {
        if (clothRenderer != null)
            clothRenderer.color = color;
    }

    private void ApplyCarrierVisual(ulong clientId)
    {
        if (!pickedUp.Value || clientId == ulong.MaxValue)
            return;

        NetworkObject playerObject = FindPlayerObject(clientId);

        if (playerObject == null)
            return;

        playerObject.GetComponent<FlagCarryVisual>()?.Show(
            branchRenderer != null ? branchRenderer.sprite : null,
            clothRenderer != null ? clothRenderer.sprite : null,
            networkClothColor.Value
        );
    }

    private void ApplyCarrierClothColor(Color color)
    {
        if (!pickedUp.Value ||
            carrierClientId.Value == ulong.MaxValue)
            return;

        NetworkObject playerObject =
            FindPlayerObject(carrierClientId.Value);

        playerObject?.GetComponent<FlagCarryVisual>()?
            .SetClothColor(color);
    }

    private void StartCarrierRefresh()
    {
        if (carrierRefreshRoutine != null)
            StopCoroutine(carrierRefreshRoutine);

        carrierRefreshRoutine = StartCoroutine(
            RefreshCarrierVisual()
        );
    }

    private IEnumerator RefreshCarrierVisual()
    {
        for (int attempt = 0; attempt < 120; attempt++)
        {
            if (!pickedUp.Value ||
                carrierClientId.Value == ulong.MaxValue)
                yield break;

            ApplyCarrierVisual(carrierClientId.Value);
            ApplyCarrierClothColor(networkClothColor.Value);

            NetworkObject playerObject =
                FindPlayerObject(carrierClientId.Value);

            if (playerObject?.GetComponent<FlagCarryVisual>() != null)
                yield break;

            yield return null;
        }

        carrierRefreshRoutine = null;
    }

    private void HideCarrierVisual(ulong clientId)
    {
        if (clientId == ulong.MaxValue)
            return;

        NetworkObject playerObject = FindPlayerObject(clientId);

        if (playerObject == null)
            return;

        playerObject.GetComponent<FlagCarryVisual>()?.Hide();
    }

    private NetworkObject FindPlayerObject(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
            return null;

        foreach (NetworkObject networkObject in
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
        {
            if (networkObject.OwnerClientId == clientId &&
                networkObject.IsPlayerObject)
                return networkObject;
        }

        return null;
    }

    private void SubscribeToCarrierDeath(Health health)
    {
        UnsubscribeFromCarrierDeath();
        carrierHealth = health;
        if (carrierHealth != null)
            carrierHealth.Died += Drop;
    }

    private void UnsubscribeFromCarrierDeath()
    {
        if (carrierHealth == null)
            return;

        carrierHealth.Died -= Drop;
        carrierHealth = null;
    }

    private void Drop()
    {
        Vector3 deathPosition = transform.position;

        if (carrierHealth != null)
            deathPosition = carrierHealth.transform.position;

        transform.position = deathPosition;
        pickedUp.Value = false;
        carrierClientId.Value = NoCarrier;
        isFlying.Value = false;
        transform.localScale = defaultScale;
        transform.rotation = defaultRotation;
        UnsubscribeFromCarrierDeath();
    }

    private IEnumerator ThrowRoutine(Vector2 direction)
    {
        float distance =
            Mathf.Abs(direction.x) > 0.25f &&
            Mathf.Abs(direction.y) > 0.25f
                ? 1.35f
                : 0.65f;
        float duration = distance > 1f ? 0.42f : 0.28f;
        Vector3 start = transform.position;
        Vector3 end = start + (Vector3)(direction * distance);
        float elapsed = 0f;
        float rotationSpeed = Random.Range(-540f, 540f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float arc = Mathf.Sin(progress * Mathf.PI) * 0.35f;
            transform.position = Vector3.Lerp(start, end, progress) +
                Vector3.up * arc;
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = end;
        yield return BounceRoutine(end);

        transform.localScale = defaultScale;
        transform.rotation = defaultRotation;
        isFlying.Value = false;
        throwRoutine = null;
    }

    private IEnumerator BounceRoutine(Vector3 position)
    {
        float duration = 0.16f;
        float elapsed = 0f;
        float height = 0.12f;
        Quaternion rotation = transform.rotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.position = position +
                Vector3.up * Mathf.Sin(progress * Mathf.PI) * height;
            transform.rotation = Quaternion.Lerp(
                rotation,
                defaultRotation,
                progress
            );
            yield return null;
        }

        transform.position = position;
    }

    private void FindRenderers()
    {
        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        if (renderers.Length > 0)
            branchRenderer = renderers[0];

        if (renderers.Length > 1)
            clothRenderer = renderers[1];
    }
}
