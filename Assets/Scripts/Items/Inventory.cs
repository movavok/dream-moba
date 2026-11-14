using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Inventory : NetworkBehaviour
{
    public const int SlotCount = 4;

    [SerializeField] private ItemDatabase database;
    [SerializeField] private StatController statController;

    private readonly NetworkList<InventorySlot> slots = new();
    private readonly NetworkList<double> cooldownEnds = new();
    private readonly NetworkVariable<int> selectedSlot = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private readonly object[] slotKeys = new object[SlotCount];
    private readonly int[] cooldownItemIndices = new int[SlotCount];
    private Health health;

    public NetworkList<InventorySlot> Slots => slots;
    public NetworkVariable<int> SelectedSlot => selectedSlot;
    public ItemDatabase Database => database;
    public StatController Stats => statController;

    public event System.Action InventoryChanged;
    public event System.Action SelectionChanged;
    public event System.Action<int> ItemUsed;

    private void Awake()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            slotKeys[i] = new object();
            cooldownItemIndices[i] = -1;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (statController == null)
            statController = GetComponent<StatController>();

        if (IsServer)
        {
            health = GetComponent<Health>();
            if (health != null)
                health.Died += DropItemsOnDeath;
        }

        if (IsServer && slots.Count == 0)
        {
            for (int i = 0; i < SlotCount; i++)
                slots.Add(InventorySlot.Empty);
        }

        if (IsServer && cooldownEnds.Count == 0)
            for (int i = 0; i < SlotCount; i++)
                cooldownEnds.Add(0d);

        slots.OnListChanged += OnSlotsChanged;
        selectedSlot.OnValueChanged += OnSelectionChanged;

        RebuildModifiers();
        InventoryChanged?.Invoke();
        SelectionChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        if (health != null)
        {
            health.Died -= DropItemsOnDeath;
            health = null;
        }

        if (IsServer && statController != null)
            foreach (object key in slotKeys)
                statController.RemoveSource(key);

        slots.OnListChanged -= OnSlotsChanged;
        selectedSlot.OnValueChanged -= OnSelectionChanged;

        base.OnNetworkDespawn();
    }

    private void OnSlotsChanged(NetworkListEvent<InventorySlot> change)
    {
        if (IsServer)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                int index = slots[i].IsEmpty ? -1 : slots[i].itemIndex;
                if (index == cooldownItemIndices[i])
                    continue;

                cooldownItemIndices[i] = index;
                if (i < cooldownEnds.Count)
                    cooldownEnds[i] = 0d;
            }

            RebuildModifiers();
        }

        InventoryChanged?.Invoke();
    }

    private void OnSelectionChanged(int previous, int current)
    {
        if (IsServer)
            RebuildModifiers();

        SelectionChanged?.Invoke();
    }

    public int SlotCountValue => slots.Count;

    public float GetCooldownRemaining(int slot)
    {
        if (slot < 0 || slot >= cooldownEnds.Count || NetworkManager == null)
            return 0f;

        return Mathf.Max(0f, (float)(cooldownEnds[slot] - NetworkManager.ServerTime.Time));
    }

    public InventorySlot GetSlot(int slot)
    {
        if (slot < 0 || slot >= slots.Count)
            return InventorySlot.Empty;

        return slots[slot];
    }

    public ItemDefinition GetItem(int slot)
    {
        if (database == null)
            return null;

        return database.GetByIndex(GetSlot(slot).itemIndex);
    }

    public bool ContainsItem(ItemDefinition definition)
    {
        return CountItem(definition) > 0;
    }

    // Snapshot is taken on the server before the old player NetworkObject despawns.
    public InventorySlot[] CapturePersistentSlots()
    {
        if (!IsServer)
            return null;

        InventorySlot[] snapshot = new InventorySlot[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            ItemDefinition item = GetItem(i);
            snapshot[i] = item != null && !item.dropOnDeath
                ? GetSlot(i) : InventorySlot.Empty;
        }

        return snapshot;
    }

    public void RestoreSlots(InventorySlot[] snapshot, int selection)
    {
        if (!IsServer || snapshot == null || snapshot.Length != SlotCount)
            return;

        for (int i = 0; i < SlotCount; i++)
        {
            InventorySlot slot = snapshot[i];
            ItemDefinition item = database != null
                ? database.GetByIndex(slot.itemIndex) : null;
            slots[i] = item != null && slot.count > 0 &&
                slot.count <= Mathf.Max(1, item.maxStack)
                ? slot : InventorySlot.Empty;
        }

        // The server may also write owner-writable NetworkVariables.
        selectedSlot.Value = selection >= 0 && selection < SlotCount
            ? selection : 0;
    }

    private void DropItemsOnDeath()
    {
        // Items with their own world lifecycle (the flag) remove themselves
        // on death. Regular items use their configured WorldItem prefab.
        for (int i = 0; i < slots.Count; i++)
        {
            ItemDefinition item = GetItem(i);
            if (item == null || !item.dropOnDeath || item.worldPrefab == null)
                continue;

            int count = slots[i].count;
            NetworkObject prefabObject = item.worldPrefab.GetComponent<NetworkObject>();
            WorldItem prefabItem = item.worldPrefab.GetComponent<WorldItem>();
            if (prefabObject == null || prefabItem == null ||
                prefabItem.Definition != item || prefabItem.Count != 1)
            {
                Debug.LogError($"[Inventory] Invalid death drop prefab for '{item.id}'.");
                continue;
            }

            for (int n = 0; n < count; n++)
            {
                GameObject dropped = Instantiate(item.worldPrefab,
                    transform.position, Quaternion.identity);
                dropped.GetComponent<NetworkObject>().Spawn();
            }

            TryRemove(i, count);
        }
    }

    public int CountItem(ItemDefinition definition)
    {
        if (definition == null || database == null)
            return 0;

        int index = database.GetIndex(definition);
        int total = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].itemIndex == index)
                total += slots[i].count;
        }

        return total;
    }

    public void SelectSlot(int slot)
    {
        if (!IsOwner)
            return;

        if (slot < 0 || slot >= SlotCount)
            return;

        selectedSlot.Value = slot;
    }

    [ServerRpc]
    public void RequestUseServerRpc()
    {
        TryUse(selectedSlot.Value);
    }

    public bool TryAdd(ItemDefinition definition, int amount)
    {
        if (!IsServer || definition == null || amount <= 0)
            return false;

        if (database == null)
            return false;

        int index = database.GetIndex(definition);

        if (index < 0)
            return false;

        int remaining = amount;
        int maxStack = Mathf.Max(1, definition.maxStack);

        // Reserve the whole pickup before changing any slot. WorldItem remains
        // in the scene on failure, so a partial addition would duplicate it.
        long capacity = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot.IsEmpty)
                capacity += maxStack;
            else if (slot.itemIndex == index)
                capacity += Mathf.Max(0, maxStack - slot.count);
        }

        if (capacity < amount)
            return false;

        // Prefer the player's selected slot, even if earlier slots are empty.
        int preferred = selectedSlot.Value;
        if (preferred >= 0 && preferred < slots.Count)
        {
            InventorySlot slot = slots[preferred];
            if (slot.IsEmpty || slot.itemIndex == index)
            {
                int currentCount = slot.IsEmpty ? 0 : slot.count;
                int add = Mathf.Min(Mathf.Max(0, maxStack - currentCount), remaining);
                if (add > 0)
                {
                    slots[preferred] = new InventorySlot(index, currentCount + add);
                    remaining -= add;
                }
            }
        }

        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            InventorySlot slot = slots[i];

            if (slot.itemIndex != index)
                continue;

            int space = maxStack - slot.count;

            if (space <= 0)
                continue;

            int add = Mathf.Min(space, remaining);
            slots[i] = new InventorySlot(index, slot.count + add);
            remaining -= add;
        }

        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (!slots[i].IsEmpty)
                continue;

            int add = Mathf.Min(maxStack, remaining);
            slots[i] = new InventorySlot(index, add);
            remaining -= add;
        }

        return remaining == 0;
    }

    public bool TryRemove(int slot, int amount)
    {
        if (!IsServer || amount <= 0)
            return false;

        if (slot < 0 || slot >= slots.Count)
            return false;

        InventorySlot current = slots[slot];

        if (current.IsEmpty)
            return false;

        if (amount > current.count)
            return false;

        int remove = amount;
        int left = current.count - remove;

        slots[slot] = left > 0
            ? new InventorySlot(current.itemIndex, left)
            : InventorySlot.Empty;

        return true;
    }

    public bool RemoveItem(ItemDefinition definition, int amount)
    {
        if (!IsServer || definition == null || amount <= 0)
            return false;

        if (database == null)
            return false;

        int index = database.GetIndex(definition);

        if (index < 0)
            return false;

        if (CountItem(definition) < amount)
            return false;

        int remaining = amount;

        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            InventorySlot slot = slots[i];

            if (slot.itemIndex != index)
                continue;

            int remove = Mathf.Min(slot.count, remaining);
            int left = slot.count - remove;

            slots[i] = left > 0
                ? new InventorySlot(index, left)
                : InventorySlot.Empty;

            remaining -= remove;
        }

        return remaining == 0;
    }

    public bool TryUse(int slot)
    {
        if (!IsServer || health == null || !health.IsAlive())
            return false;

        if (slot < 0 || slot >= slots.Count || GetCooldownRemaining(slot) > 0f)
            return false;

        ItemDefinition definition = GetItem(slot);

        if (definition == null || definition.useAction == null)
            return false;

        PlayerNetwork player = GetComponent<PlayerNetwork>();

        if (!definition.useAction.CanUse(player, this, slot))
            return false;

        definition.useAction.Use(player, this, slot);
        if (definition.useCooldown > 0f && slot < cooldownEnds.Count &&
            GetItem(slot) == definition)
            cooldownEnds[slot] = NetworkManager.ServerTime.Time + definition.useCooldown;

        ItemUsedClientRpc(slot);
        return true;
    }

    [ClientRpc]
    private void ItemUsedClientRpc(int slot)
    {
        if (IsOwner)
            ItemUsed?.Invoke(slot);
    }

    public bool TryThrow(int slot, Vector2 direction, bool strongThrow)
    {
        if (!IsServer || health == null || !health.IsAlive())
            return false;

        ItemDefinition definition = GetItem(slot);

        if (definition == null ||
            !definition.throwable ||
            definition.worldPrefab == null)
            return false;

        if (direction.sqrMagnitude < 0.01f)
            direction = Vector2.down;

        direction = direction.normalized;

        GameObject worldObject = Instantiate(
            definition.worldPrefab,
            transform.position,
            Quaternion.identity
        );

        NetworkObject networkObject =
            worldObject.GetComponent<NetworkObject>();
        WorldItem worldItem = worldObject.GetComponent<WorldItem>();

        if (networkObject == null || worldItem == null ||
            worldItem.Definition != definition || worldItem.Count != 1 ||
            worldObject.GetComponent<ItemThrowMotion>() == null)
        {
            Debug.LogError(
                $"[Inventory] Throwable item " +
                $"'{definition.id}' needs a single matching WorldItem, " +
                $"NetworkObject and ItemThrowMotion."
            );

            Destroy(worldObject);
            return false;
        }

        networkObject.Spawn();

        if (!TryRemove(slot, 1))
        {
            networkObject.Despawn(true);
            return false;
        }

        float multiplier = strongThrow
            ? definition.strongThrowMultiplier
            : definition.normalThrowMultiplier;

        worldItem.ThrowFrom(transform, direction, multiplier);
        return true;
    }

    private void RebuildModifiers()
    {
        if (!IsServer || statController == null)
            return;

        for (int i = 0; i < SlotCount; i++)
        {
            statController.RemoveSource(slotKeys[i]);

            ItemDefinition definition = GetItem(i);

            if (definition == null)
                continue;

            List<StatModifier> modifiers = new();

            if (definition.passiveModifiers != null)
            {
                foreach (StatModifier modifier in definition.passiveModifiers)
                {
                    StatModifier scaled = modifier;
                    scaled.value *= GetSlot(i).count;
                    modifiers.Add(scaled);
                }
            }

            if (selectedSlot.Value == i &&
                definition.selectedModifiers != null)
            {
                foreach (StatModifier modifier in definition.selectedModifiers)
                {
                    StatModifier scaled = modifier;
                    scaled.value *= GetSlot(i).count;
                    modifiers.Add(scaled);
                }
            }

            if (modifiers.Count > 0)
                statController.AddSource(slotKeys[i], modifiers);
        }
    }
}
