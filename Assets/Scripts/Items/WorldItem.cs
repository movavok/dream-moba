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

    private readonly NetworkVariable<bool> isFlying = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Collider2D interactionCollider;
    private Coroutine throwRoutine;
    private Vector3 defaultScale;
    private Quaternion defaultRotation;

    public ItemDefinition Definition => definition;
    public int Count => count;
    public bool IsFlying => isFlying.Value;
    public bool IsLanded => !isFlying.Value;

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
            !isFlying.Value;
    }

    public void SetHighlighted(bool highlighted)
    {
        if (isFlying.Value)
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

        if (!inventory.TryAdd(definition, count))
            return;

        NetworkObject.Despawn(true);
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
            ignoreRoot
        );

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
