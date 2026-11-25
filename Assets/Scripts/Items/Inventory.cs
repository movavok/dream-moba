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
                slot.count <= Mathf.Max(1, item.maxStack) &&
                (item.maxCharges <= 0 || slot.charges > 0 && slot.charges <= item.maxCharges)
                ? slot : InventorySlot.Empty;
        }

        // The server may also write owner-writable NetworkVariables.
        selectedSlot.Value = selection >= 0 && selection < SlotCount
            ? selection : 0;
    }

    // Called only after ContainerInventory has validated both sides on the server.
    public void ApplyContainerTransfer(InventorySlot[] result)
    {
        if (!IsServer || result == null || result.Length != SlotCount ||
            slots.Count != SlotCount)
            return;

        for (int i = 0; i < SlotCount; i++)
            if (!slots[i].Equals(result[i]))
                slots[i] = result[i];
    }

    private void DropItemsOnDeath()
    {
        // Items with their own world lifecycle (the flag) remove themselves
        // on death. Regular items use their configured WorldItem prefab.
        for (int i = 0; i < slots.Count; i++)
        {
            ItemDefinition item = GetItem(i);
            if (item == null || !item.dropOnDeath || item.worldPrefab == null ||
                item.worldPrefab.GetComponent<WorldItem>() == null)
                continue;

            int dropped = WorldItem.DropAndThrow(
                item,
                slots[i].count,
                transform,
                slots[i].charges
            );

            if (dropped > 0)
                TryRemove(i, dropped);
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
    public void RequestUseServerRpc(int slot)
    {
        TryUse(slot);
    }

    public bool TryAdd(ItemDefinition definition, int amount, int existingCharges = -1)
    {
        if (!CanAdd(definition, amount, existingCharges))
            return false;

        int index = database.GetIndex(definition);
        int charges = definition.maxCharges > 0
            ? (existingCharges > 0 ? existingCharges : definition.maxCharges) : 0;
        InventorySlot[] copy = new InventorySlot[slots.Count];
        for (int i = 0; i < copy.Length; i++) copy[i] = slots[i];
        if (!TryFit(copy, index, amount, definition, selectedSlot.Value, charges))
            return false;
        for (int i = 0; i < copy.Length; i++)
            if (!slots[i].Equals(copy[i])) slots[i] = copy[i];
        return true;
    }

    public bool CanAdd(ItemDefinition definition, int amount, int existingCharges = -1)
    {
        if (!IsServer || definition == null || amount <= 0 || database == null ||
            slots.Count != SlotCount)
            return false;

        int index = database.GetIndex(definition);
        if (index < 0) return false;
        InventorySlot[] copy = new InventorySlot[slots.Count];
        for (int i = 0; i < copy.Length; i++) copy[i] = slots[i];
        int charges = definition.maxCharges > 0
            ? (existingCharges > 0 ? existingCharges : definition.maxCharges) : 0;
        return TryFit(copy, index, amount, definition, selectedSlot.Value, charges);
    }

    // Charged stacks store the current item's remaining charges; all units
    // behind it are full. Never merge two partly used items into one slot.
    public static bool TryFit(InventorySlot[] target, int itemIndex, int amount,
        ItemDefinition item, int preferred, int charges)
    {
        if (target == null || item == null || amount <= 0 ||
            item.maxCharges > 0 && (charges <= 0 || charges > item.maxCharges))
            return false;

        if (item.maxCharges > 0 && charges < item.maxCharges)
        {
            // Place the one partial unit first, then distribute the fully
            // charged remainder across every slot (including earlier ones).
            if (preferred >= 0 && preferred < target.Length)
                AddTo(preferred, 1);
            for (int i = 0; i < target.Length && charges < item.maxCharges; i++)
                if (i != preferred && !target[i].IsEmpty &&
                    target[i].itemIndex == itemIndex)
                    AddTo(i, 1);
            for (int i = 0; i < target.Length && charges < item.maxCharges; i++)
                if (i != preferred && target[i].IsEmpty)
                    AddTo(i, 1);
            if (charges < item.maxCharges) return false;
        }

        if (preferred >= 0 && preferred < target.Length) AddTo(preferred, amount);
        for (int i = 0; i < target.Length && amount > 0; i++)
            if (i != preferred && !target[i].IsEmpty && target[i].itemIndex == itemIndex)
                AddTo(i, amount);
        for (int i = 0; i < target.Length && amount > 0; i++)
            if (i != preferred && target[i].IsEmpty)
                AddTo(i, amount);
        return amount == 0;

        void AddTo(int index, int maximum)
        {
            InventorySlot slot = target[index];
            if (!slot.IsEmpty && slot.itemIndex != itemIndex) return;
            if (!slot.IsEmpty && item.maxCharges > 0 &&
                charges < item.maxCharges && slot.charges < item.maxCharges) return;

            int added = Mathf.Min(Mathf.Min(amount, maximum), Mathf.Max(0,
                Mathf.Max(1, item.maxStack) - (slot.IsEmpty ? 0 : slot.count)));
            if (added == 0) return;
            target[index] = new InventorySlot(itemIndex,
                (slot.IsEmpty ? 0 : slot.count) + added,
                item.maxCharges <= 0 ? 0 : slot.IsEmpty || charges < item.maxCharges
                    ? charges : slot.charges);
            amount -= added;
            if (item.maxCharges > 0) charges = item.maxCharges;
        }
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

        ItemDefinition item = GetItem(slot);
        slots[slot] = left > 0
            ? new InventorySlot(current.itemIndex, left,
                item != null && item.maxCharges > 0 ? item.maxCharges : current.charges)
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
                ? new InventorySlot(index, left,
                    definition.maxCharges > 0 ? definition.maxCharges : slot.charges)
                : InventorySlot.Empty;

            remaining -= remove;
        }

        return remaining == 0;
    }

    public bool TryUse(int slot)
    {
        if (!IsServer || health == null || !health.IsAlive() || health.IsStunned)
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

        int itemIndex = database != null ? database.GetIndex(definition) : -1;
        if (itemIndex >= 0)
            PlayItemUseSoundClientRpc(itemIndex);

        return true;
    }

    [ClientRpc]
    private void ItemUsedClientRpc(int slot)
    {
        if (IsOwner)
            ItemUsed?.Invoke(slot);
    }

    [ClientRpc]
    private void PlayItemUseSoundClientRpc(int itemIndex)
    {
        ItemDefinition definition = database != null
            ? database.GetByIndex(itemIndex)
            : null;

        if (definition == null || definition.useAction == null)
            return;

        definition.useAction.PlayUseSound(
            transform,
            definition.AudioGroup
        );
    }

    public bool TryThrow(int slot, Vector2 direction, bool strongThrow)
    {
        if (!IsServer || health == null || !health.IsAlive() || health.IsStunned)
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

        if (definition.maxCharges > 0)
            worldItem.SetCharges(GetSlot(slot).charges);

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

    public bool TryBlockBasicAttack(out int itemIndex)
    {
        itemIndex = -1;
        if (!IsServer || !IsSpawned) return false;
        int selected = selectedSlot.Value;
        InventorySlot slot = GetSlot(selected);
        ItemDefinition item = GetItem(selected);
        if (item == null || !item.blocksBasicAttacks || item.maxCharges <= 0 ||
            slot.charges <= 0) return false;
        itemIndex = slot.itemIndex;
        ConsumeCharge(selected, slot, item);
        return true;
    }

    public bool TryConsumeSelectedIgnition()
    {
        if (!IsServer || !IsSpawned) return false;
        int selected = selectedSlot.Value;
        InventorySlot slot = GetSlot(selected);
        ItemDefinition item = GetItem(selected);
        if (item == null || !item.ignitesBasicAttacks || item.maxCharges <= 0 ||
            slot.charges <= 0) return false;
        ConsumeCharge(selected, slot, item);
        return true;
    }

    private void ConsumeCharge(int index, InventorySlot slot, ItemDefinition item)
    {
        slots[index] = slot.charges > 1
            ? new InventorySlot(slot.itemIndex, slot.count, slot.charges - 1)
            : slot.count > 1
                ? new InventorySlot(slot.itemIndex, slot.count - 1, item.maxCharges)
                : InventorySlot.Empty;
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
