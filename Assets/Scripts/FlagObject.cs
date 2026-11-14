using Unity.Netcode;
using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

public class FlagObject : NetworkBehaviour, IInteractable, ICarriableItem
{
    private const ulong NoCarrier = ulong.MaxValue;

    [SerializeField] private SpriteRenderer branchRenderer;
    [SerializeField] private SpriteRenderer clothRenderer;
    [SerializeField] private string interactionVerb = "Take";
    [SerializeField] private string interactionName = "flag";
    [SerializeField] private InteractionOutline interactionOutline;
    [SerializeField] private ItemThrowMotion throwMotion;
    [SerializeField] private ItemDefinition inventoryItem;
    [SerializeField] private GameObject flagTimerPrefab;
    [SerializeField] private Vector3 flagTimerOffset = new Vector3(0f, 0.75f, 0f);
    [SerializeField] private float flagTimerScale = 0.01f;
    [SerializeField] private Color urgentTimerColor = new Color(1f, 0.3f, 0.3f, 1f);

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
    private readonly NetworkVariable<double> returnAtServerTime =
        new NetworkVariable<double>(
            -1d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private Collider2D interactionCollider;
    private Coroutine carrierRefreshRoutine;
    private Coroutine throwRoutine;
    private Health carrierHealth;
    private FlagGameManager flagGameManager;
    private Vector3 defaultScale;
    private Quaternion defaultRotation;
    private Vector3 homePosition;
    private GameObject flagTimerObject;
    private TMP_Text flagTimerText;
    private Color regularTimerColor;
    private int displayedSeconds = -1;
    private bool displayedUrgent;
    private ulong lastThrowerClientId = NoCarrier;
    private short lastThrowerTeamId;
    private Color lastThrowerTeamColor;
    private Vector3 lastDeliveryCheckPosition;
    [SerializeField] private float returnAfterSeconds = 30f;

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
            carrierClientId.Value == player.OwnerClientId &&
            inventoryItem != null &&
            player.GetComponent<Inventory>() is Inventory inventory &&
            inventory.ContainsItem(inventoryItem);
    }

    public ItemDefinition InventoryItem => inventoryItem;
    public bool HasThrower => IsServer && lastThrowerClientId != NoCarrier;
    public ulong LastThrowerClientId => lastThrowerClientId;
    public short LastThrowerTeamId => lastThrowerTeamId;
    public Color LastThrowerTeamColor => lastThrowerTeamColor;

    public void SetHomePosition(Vector3 position)
    {
        if (!IsServer)
            return;

        homePosition = position;
    }

    public void Throw(
        PlayerNetwork player,
        Vector2 direction,
        float distanceMultiplier)
    {
        if (!IsServer || !IsCarriedBy(player))
            return;

        if (throwMotion == null)
            return;

        if (!player.GetComponent<Inventory>().RemoveItem(inventoryItem, 1))
            return;

        Team throwerTeam = player.GetComponent<Team>();
        lastThrowerClientId = player.OwnerClientId;
        lastThrowerTeamId = throwerTeam != null ? throwerTeam.TeamId.Value : (short)0;
        lastThrowerTeamColor = throwerTeam != null ? throwerTeam.TeamColor : Color.white;
        lastDeliveryCheckPosition = player.transform.position;

        UnsubscribeFromCarrierDeath();

        pickedUp.Value = false;
        carrierClientId.Value = NoCarrier;
        isFlying.Value = true;
        returnAtServerTime.Value = -1d;

        if (throwRoutine != null)
            StopCoroutine(throwRoutine);

        throwRoutine = StartCoroutine(
            ThrowRoutine(
                player.transform.position,
                direction,
                distanceMultiplier,
                player.transform
            )
        );
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (branchRenderer == null ||
            clothRenderer == null)
            FindRenderers();

        interactionCollider = GetComponent<Collider2D>();

        if (throwMotion == null)
            throwMotion = GetComponent<ItemThrowMotion>();

        if (throwMotion == null)
        {
            Debug.LogError(
                "FlagObject requires ItemThrowMotion component!",
                this
            );
        }

        defaultScale = transform.localScale;
        defaultRotation = transform.rotation;

        if (IsServer)
        {
            homePosition = transform.position;
            returnAtServerTime.Value = -1d;
            lastDeliveryCheckPosition = transform.position;
            flagGameManager = FindAnyObjectByType<FlagGameManager>();
        }

        EnsureItemSorting();
        CreateFlagTimer();

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
        UpdateFlagTimer();

        if (IsServer && pickedUp.Value)
            CheckCarrier();

        if (IsServer && HasThrower && !pickedUp.Value)
        {
            if (flagGameManager == null)
                flagGameManager = FindAnyObjectByType<FlagGameManager>();

            flagGameManager?.TryCompleteThrownDelivery(
                this, lastDeliveryCheckPosition, transform.position);
            lastDeliveryCheckPosition = transform.position;
        }

        if (IsServer && !pickedUp.Value && !isFlying.Value &&
            returnAtServerTime.Value >= 0d &&
            NetworkManager.ServerTime.Time >= returnAtServerTime.Value)
            ReturnHome();
    }

    private void StartReturnCountdown()
    {
        returnAtServerTime.Value =
            NetworkManager.ServerTime.Time + Mathf.Max(0f, returnAfterSeconds);
    }

    private void CreateFlagTimer()
    {
        if (flagTimerPrefab == null || flagTimerObject != null)
            return;

        flagTimerObject = Instantiate(flagTimerPrefab, transform);
        flagTimerObject.name = "FlagReturnTimer";
        RectTransform rect = flagTimerObject.GetComponent<RectTransform>();
        if (rect == null)
        {
            Debug.LogError("Flag timer prefab needs a RectTransform.", this);
            Destroy(flagTimerObject);
            flagTimerObject = null;
            return;
        }

        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * flagTimerScale;
        rect.anchoredPosition = Vector2.zero;
        rect.localPosition = flagTimerOffset;

        Canvas canvas = flagTimerObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = flagTimerObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = "ProjectileBehind";
        canvas.sortingOrder = 10;

        foreach (Graphic graphic in flagTimerObject.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        flagTimerText = flagTimerObject.GetComponentInChildren<TMP_Text>(true);
        if (flagTimerText != null)
            regularTimerColor = flagTimerText.color;

        flagTimerObject.SetActive(false);
    }

    private void UpdateFlagTimer()
    {
        if (flagTimerObject == null || flagTimerText == null)
            return;

        bool visible = IsSpawned && !pickedUp.Value && !isFlying.Value &&
            returnAtServerTime.Value >= 0d;
        if (flagTimerObject.activeSelf != visible)
            flagTimerObject.SetActive(visible);
        if (!visible)
            return;

        double remaining = Mathf.Max(0f, (float)(returnAtServerTime.Value -
            NetworkManager.ServerTime.Time));
        int seconds = Mathf.CeilToInt((float)remaining);
        if (seconds != displayedSeconds)
        {
            flagTimerText.text = seconds.ToString();
            displayedSeconds = seconds;
        }

        bool urgent = remaining <= 5d;
        if (urgent != displayedUrgent)
        {
            flagTimerText.color = urgent ? urgentTimerColor : regularTimerColor;
            displayedUrgent = urgent;
        }
    }

    private void EnsureItemSorting()
    {
        if (GetComponent<ItemSorting>() == null)
            gameObject.AddComponent<ItemSorting>();
    }

    private void ReturnHome()
    {
        transform.position = homePosition;
        transform.rotation = defaultRotation;
        transform.localScale = defaultScale;
        returnAtServerTime.Value = -1d;
        ClearThrower();
        GetComponent<ItemSorting>()?.SetBehind(false);
    }

    private void ClearThrower()
    {
        lastThrowerClientId = NoCarrier;
        lastThrowerTeamId = 0;
        lastDeliveryCheckPosition = transform.position;
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

        Inventory inventory = player.GetComponent<Inventory>();
        if (inventoryItem == null || inventory == null ||
            !inventory.TryAdd(inventoryItem, 1))
            return;

        ClearThrower();
        returnAtServerTime.Value = -1d;
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
        if (flagTimerObject != null)
            flagTimerObject.SetActive(false);

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
            interactionCollider.enabled = !isPickedUp;

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
        if (!IsServer || !pickedUp.Value)
            return;

        Vector3 deathPosition = transform.position;

        NetworkObject carrier = FindPlayerObject(carrierClientId.Value);
        carrier?.GetComponent<Inventory>()?.RemoveItem(inventoryItem, 1);

        if (carrierHealth != null)
            deathPosition = carrierHealth.transform.position;

        transform.position = deathPosition;
        ClearThrower();
        pickedUp.Value = false;
        carrierClientId.Value = NoCarrier;
        isFlying.Value = false;
        StartReturnCountdown();
        transform.localScale = defaultScale;
        transform.rotation = defaultRotation;
        UnsubscribeFromCarrierDeath();
    }

    private IEnumerator ThrowRoutine(
        Vector3 origin,
        Vector2 direction,
        float distanceMultiplier,
        Transform thrower = null)
    {
        yield return throwMotion.Play(
            origin,
            direction,
            distanceMultiplier,
            thrower
        );

        transform.localScale = defaultScale;
        transform.rotation = defaultRotation;

        isFlying.Value = false;
        StartReturnCountdown();
        throwRoutine = null;
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
