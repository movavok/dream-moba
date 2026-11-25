using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class WorldItem : NetworkBehaviour, IInteractable
{
    [SerializeField] private ItemDefinition definition;
    [SerializeField] private int count = 1;
    [SerializeField] private string interactionVerb = "Take";
    [SerializeField] private string interactionName = "item";
    [SerializeField] private InteractionOutline interactionOutline;
    [SerializeField] private ItemThrowMotion throwMotion;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private bool consumeOnThrow;

    private readonly NetworkVariable<bool> isFlying = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<int> instanceCharges = new(0);
    private readonly NetworkVariable<bool> spentOnThrow = new(false);
    public void SetCharges(int value)
    {
        if (IsServer && IsSpawned) instanceCharges.Value = Mathf.Max(0, value);
    }

    private Collider2D interactionCollider;
    private Coroutine throwRoutine;
    private Vector3 defaultScale;
    private Quaternion defaultRotation;

    public ItemDefinition Definition => definition;
    public Sprite WorldSprite => visual != null ? visual.sprite : null;
    public int Count => count;
    public bool IsFlying => isFlying.Value;
    public bool IsLanded => !isFlying.Value;
    public bool WasConsumedByThrow => spentOnThrow.Value;

    public string InteractionVerb => interactionVerb;
    public string InteractionName => interactionName;
    public Transform InteractionPromptTarget => transform;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        interactionCollider = GetComponent<Collider2D>();

        if (throwMotion == null)
            throwMotion = GetComponent<ItemThrowMotion>();

        if (interactionOutline == null)
            interactionOutline = GetComponent<InteractionOutline>();

        if (interactionOutline != null && visual != null)
            interactionOutline.Configure(visual);

        defaultScale = transform.localScale;
        defaultRotation = transform.rotation;

        if (GetComponent<ItemSorting>() == null)
            gameObject.AddComponent<ItemSorting>();

        isFlying.OnValueChanged += OnFlyingChanged;
        ApplyFlyingState(isFlying.Value);
    }

    public override void OnNetworkDespawn()
    {
        isFlying.OnValueChanged -= OnFlyingChanged;

        if (throwRoutine != null)
            StopCoroutine(throwRoutine);

        base.OnNetworkDespawn();
    }

    public bool CanInteract(PlayerNetwork player)
    {
        return player != null &&
            player.IsSpawned &&
            !isFlying.Value && !spentOnThrow.Value;
    }

    public void SetHighlighted(bool highlighted)
    {
        if (isFlying.Value || spentOnThrow.Value)
            return;

        interactionOutline?.SetHighlighted(highlighted);
    }

    public void Interact(PlayerNetwork player)
    {
        if (!IsServer || !CanInteract(player))
            return;

        Inventory inventory = player.GetComponent<Inventory>();

        if (inventory == null)
            return;

        if (!inventory.TryAdd(definition, count, instanceCharges.Value))
            return;

        NetworkObject.Despawn(true);
    }

    public static int DropAndThrow(
        ItemDefinition item,
        int count,
        Transform origin,
        int charges = 0)
    {
        if (item == null || count <= 0 || origin == null ||
            item.worldPrefab == null ||
            NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
            return 0;

        GameObject prefab = item.worldPrefab;
        WorldItem prefabItem = prefab.GetComponent<WorldItem>();

        if (prefab.GetComponent<NetworkObject>() == null ||
            prefabItem == null || prefabItem.Definition != item ||
            prefabItem.Count != 1)
        {
            Debug.LogError(
                $"[WorldItem] Invalid drop prefab for '{item.id}'.",
                prefab
            );

            return 0;
        }

        int dropped = 0;

        for (int i = 0; i < count; i++)
        {
            GameObject worldObject = Instantiate(
                prefab,
                origin.position,
                Quaternion.identity
            );

            if (worldObject.GetComponent<ItemThrowMotion>() == null)
                worldObject.AddComponent<ItemThrowMotion>();

            worldObject.GetComponent<NetworkObject>().Spawn();
            if (item.maxCharges > 0)
                worldObject.GetComponent<WorldItem>().SetCharges(
                    i == 0 && charges > 0 ? charges : item.maxCharges);

            Vector2 direction = ItemThrowMotion.RandomDropDirection();

            worldObject.GetComponent<WorldItem>().ThrowFrom(
                origin,
                direction,
                item.normalThrowMultiplier
            );

            dropped++;
        }

        return dropped;
    }

    public void ThrowFrom(
        Transform origin,
        Vector2 direction,
        float distanceMultiplier)
    {
        if (!IsServer || origin == null)
            return;

        if (throwMotion == null)
        {
            Debug.LogError(
                $"[WorldItem] '{name}' has no ItemThrowMotion.",
                this
            );

            return;
        }

        if (throwRoutine != null)
            StopCoroutine(throwRoutine);

        isFlying.Value = true;
        IThrownItemImpact impact = GetComponent<IThrownItemImpact>();
        if (impact != null || consumeOnThrow)
            spentOnThrow.Value = true;
        if (impact != null)
        {
            impact.OnThrowStart(origin);
        }

        throwRoutine = StartCoroutine(
            ThrowRoutine(
                origin.position,
                direction,
                distanceMultiplier,
                origin
            )
        );
    }

    private IEnumerator ThrowRoutine(
        Vector3 origin,
        Vector2 direction,
        float distanceMultiplier,
        Transform ignoreRoot)
    {
        yield return throwMotion.Play(
            origin,
            direction,
            distanceMultiplier,
            ignoreRoot,
            GetComponent<IThrownItemImpact>()
        );

        if (this == null || !IsSpawned) yield break;

        transform.localScale = defaultScale;
        transform.rotation = defaultRotation;

        isFlying.Value = false;
        throwRoutine = null;
    }

    private void OnFlyingChanged(bool previous, bool current)
    {
        ApplyFlyingState(current);
    }

    private void ApplyFlyingState(bool flying)
    {
        transform.localScale = flying
            ? defaultScale * 0.8f
            : defaultScale;

        if (flying)
            interactionOutline?.SetHighlighted(false);
    }
}
