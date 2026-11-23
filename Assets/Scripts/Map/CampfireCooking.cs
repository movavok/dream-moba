using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class CampfireCooking : NetworkBehaviour, IInteractable, IDynamicInteractionPrompt
{
    [Header("Interaction")]
    [SerializeField] private InteractionOutline outline;
    [SerializeField] private SpriteRenderer fireVisual;

    [Header("Cooking visual (child)")]
    [SerializeField] private GameObject cookingVisual;
    [SerializeField] private SpriteRenderer rawRenderer;
    [SerializeField] private SpriteRenderer resultRenderer;
    [SerializeField] private float fillBottom = 0f;
    [SerializeField] private float fillTop = 1f;

    [Header("Output")]
    [SerializeField] private Transform dropPoint;
    [Min(0f)] [SerializeField] private float weakThrowMultiplier = 0.6f;

    // -1 is idle. The database index is shared by every player's inventory.
    private readonly NetworkVariable<int> cookingIndex = new(-1);
    private readonly NetworkVariable<double> startedAt = new(0);
    private readonly NetworkVariable<double> finishesAt = new(0);
    private Inventory localInventory;
    private MaterialPropertyBlock fillProperties;
    private float uvBottom;
    private float uvTop;

    public string InteractionVerb => "Cook";
    public string InteractionName => "item";
    public Transform InteractionPromptTarget => transform;

    private void Awake()
    {
        if (outline != null && fireVisual != null)
            outline.Configure(fireVisual);
    }

    public override void OnNetworkSpawn()
    {
        cookingIndex.OnValueChanged += OnCookingChanged;
        RefreshVisual();
    }

    public override void OnNetworkDespawn()
    {
        cookingIndex.OnValueChanged -= OnCookingChanged;
        outline?.SetHighlighted(false);
        if (cookingVisual != null)
            cookingVisual.SetActive(false);
    }

    private void OnDisable() => outline?.SetHighlighted(false);

    public bool CanInteract(PlayerNetwork player) => IsSpawned &&
        player != null && player.IsSpawned;

    public void SetHighlighted(bool highlighted) => outline?.SetHighlighted(highlighted);

    public string GetPromptText(string binding)
    {
        if (cookingIndex.Value >= 0)
            return "Cooking...";

        PlayerNetwork player = PlayerNetwork.LocalPlayer;
        if (player == null)
            return "Choose item";

        if (localInventory == null || localInventory.gameObject != player.gameObject)
            localInventory = player.GetComponent<Inventory>();

        if (localInventory == null)
            return "Choose item";

        int slot = localInventory.SelectedSlot.Value;
        ItemDefinition item = localInventory.GetItem(slot);
        if (!IsValidRecipe(item, localInventory.Database) ||
            localInventory.GetSlot(slot).IsEmpty)
            return "Choose item";

        WorldItem worldItem = item.worldPrefab != null
            ? item.worldPrefab.GetComponent<WorldItem>() : null;
        string name = worldItem != null ? worldItem.InteractionName : item.displayName;
        return $"[{binding}] - Cook {name}";
    }

    public void Interact(PlayerNetwork player)
    {
        if (!IsServer || !CanInteract(player) || cookingIndex.Value >= 0)
            return;

        Health health = player.GetComponent<Health>();
        Inventory inventory = player.GetComponent<Inventory>();
        if (health == null || !health.IsAlive() || inventory == null)
            return;

        int slot = inventory.SelectedSlot.Value;
        InventorySlot stack = inventory.GetSlot(slot);
        ItemDefinition item = inventory.GetItem(slot);
        if (stack.IsEmpty || !IsValidRecipe(item, inventory.Database) ||
            stack.itemIndex != inventory.Database.GetIndex(item))
            return;

        // All checks and the debit run on the server before publishing the state.
        if (!inventory.TryRemove(slot, 1))
            return;

        startedAt.Value = NetworkManager.ServerTime.Time;
        finishesAt.Value = startedAt.Value + item.cookingDuration;
        cookingIndex.Value = stack.itemIndex;
    }

    private static bool IsValidRecipe(ItemDefinition item, ItemDatabase database)
    {
        if (item == null || database == null || !item.canBeCooked ||
            item.cookedResult == null || item.cookingDuration <= 0f ||
            item.cookingRawSprite == null || item.cookingResultSprite == null ||
            database.GetIndex(item) < 0 ||
            database.GetIndex(item.cookedResult) < 0)
            return false;

        GameObject prefab = item.cookedResult.worldPrefab;
        WorldItem world = prefab != null ? prefab.GetComponent<WorldItem>() : null;
        return prefab != null && prefab.GetComponent<NetworkObject>() != null &&
            world != null && world.Definition == item.cookedResult && world.Count == 1 &&
            prefab.GetComponent<ItemThrowMotion>() != null;
    }

    private ItemDefinition GetCookingItem()
    {
        // The database is the same asset used by inventories on every peer.
        return itemDatabase != null ? itemDatabase.GetByIndex(cookingIndex.Value) : null;
    }

    [Header("Items")]
    [SerializeField] private ItemDatabase itemDatabase;

    private void OnCookingChanged(int previous, int current) => RefreshVisual();

    private void RefreshVisual()
    {
        ItemDefinition item = GetCookingItem();
        bool visible = item != null && cookingIndex.Value >= 0 &&
            cookingVisual != null && rawRenderer != null && resultRenderer != null;
        if (cookingVisual != null)
            cookingVisual.SetActive(visible);
        if (!visible)
            return;

        rawRenderer.sprite = item.cookingRawSprite;
        resultRenderer.sprite = item.cookingResultSprite;
        Vector2[] uv = resultRenderer.sprite.uv;
        uvBottom = uvTop = uv[0].y;
        foreach (Vector2 coordinate in uv)
        {
            uvBottom = Mathf.Min(uvBottom, coordinate.y);
            uvTop = Mathf.Max(uvTop, coordinate.y);
        }
    }

    private void Update()
    {
        if (!IsSpawned || cookingIndex.Value < 0)
            return;

        if (IsServer && NetworkManager.ServerTime.Time >= finishesAt.Value)
            FinishCooking();

        if (cookingIndex.Value < 0 || resultRenderer == null ||
            resultRenderer.sprite == null)
            return;

        float progress = Mathf.Clamp01((float)(
            (NetworkManager.ServerTime.Time - startedAt.Value) /
            (finishesAt.Value - startedAt.Value)));
        fillProperties ??= new MaterialPropertyBlock();
        resultRenderer.GetPropertyBlock(fillProperties);
        fillProperties.SetFloat("_CookFill", Mathf.Lerp(fillBottom, fillTop, progress));
        fillProperties.SetFloat("_CookUVBottom", uvBottom);
        fillProperties.SetFloat("_CookUVTop", uvTop);
        resultRenderer.SetPropertyBlock(fillProperties);
    }

    private void FinishCooking()
    {
        ItemDefinition item = GetCookingItem();
        if (item == null || item.cookedResult == null ||
            item.cookedResult.worldPrefab == null)
            return;

        Transform origin = dropPoint != null ? dropPoint : transform;
        GameObject output = Instantiate(item.cookedResult.worldPrefab,
            origin.position, Quaternion.identity);
        NetworkObject networkObject = output.GetComponent<NetworkObject>();
        WorldItem worldItem = output.GetComponent<WorldItem>();
        if (networkObject == null || worldItem == null ||
            output.GetComponent<ItemThrowMotion>() == null)
        {
            Destroy(output);
            return;
        }

        networkObject.Spawn();
        worldItem.ThrowFrom(origin, ItemThrowMotion.RandomDropDirection(),
            weakThrowMultiplier);
        cookingIndex.Value = -1;
    }
}
