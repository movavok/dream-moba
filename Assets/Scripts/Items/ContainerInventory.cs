using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// One server-owned stock per world container. Clients never write either inventory.
[RequireComponent(typeof(NetworkObject), typeof(ContainerInteractable))]
public class ContainerInventory : NetworkBehaviour
{
    [System.Serializable]
    private struct StartingSlot
    {
        public ItemDefinition item;
        [Min(0)] public int count;
    }

    [SerializeField] private ItemDatabase database;
    [SerializeField] private ContainerPanelView panelPrefab;
    [SerializeField, Min(1)] private int capacity = 4;
    [SerializeField] private StartingSlot[] initialSlots;

    private readonly NetworkList<InventorySlot> slots = new();
    private static readonly Dictionary<ulong, ContainerInventory> OpenByClient = new();

    public NetworkList<InventorySlot> Slots => slots;
    public ItemDatabase Database => database;
    public ContainerPanelView PanelPrefab => panelPrefab;
    public bool IsPlayerInInteractionRange(PlayerNetwork player)
    {
        return player != null &&
            player.GetComponent<InteractionDetector>() is InteractionDetector detector &&
            GetComponent<ContainerInteractable>() is ContainerInteractable interactable &&
            detector.IsWithinInteractionRange(NetworkObject, interactable);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer || slots.Count != 0)
            return;

        for (int i = 0; i < capacity; i++)
        {
            ItemDefinition item = initialSlots != null && i < initialSlots.Length
                ? initialSlots[i].item : null;
            int count = item != null && initialSlots != null && i < initialSlots.Length
                ? initialSlots[i].count : 0;
            int index = database != null ? database.GetIndex(item) : -1;
            slots.Add(index >= 0 && count > 0
                ? new InventorySlot(index, Mathf.Min(count,
                    Mathf.Max(1, item.maxStack)), item.maxCharges)
                : InventorySlot.Empty);
        }
    }

    public override void OnNetworkDespawn()
    {
        ContainerUI.Instance?.CloseIfCurrent(this);
        if (IsServer)
        {
            List<ulong> closing = new();
            foreach (var entry in OpenByClient)
                if (entry.Value == this)
                    closing.Add(entry.Key);
            foreach (ulong id in closing)
                OpenByClient.Remove(id);
        }

        base.OnNetworkDespawn();
    }

    public void Open(PlayerNetwork player)
    {
        if (!IsServer || !CanAccess(player) || panelPrefab == null ||
            panelPrefab.SlotCount != slots.Count)
            return;

        ulong id = player.OwnerClientId;
        OpenByClient[id] = this;
        OpenClientRpc(new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { id } }
        });
    }

    public void RequestTransfer(bool fromContainer, int sourceSlot, bool wholeStack,
        int selectedPlayerSlot)
    {
        if (IsSpawned)
            TransferRpc(fromContainer, sourceSlot, wholeStack, selectedPlayerSlot);
    }

    public void CloseForLocalPlayer()
    {
        if (IsSpawned)
            CloseRpc();
    }

    [ClientRpc]
    private void OpenClientRpc(ClientRpcParams rpc = default)
    {
        ContainerUI.Instance?.Open(this);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CloseRpc(RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        if (OpenByClient.TryGetValue(sender, out ContainerInventory current) && current == this)
            OpenByClient.Remove(sender);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TransferRpc(bool fromContainer, int sourceIndex, bool wholeStack,
        int selectedPlayerSlot, RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        if (!OpenByClient.TryGetValue(sender, out ContainerInventory current) || current != this ||
            NetworkManager == null || !NetworkManager.ConnectedClients.TryGetValue(sender, out NetworkClient client) ||
            client.PlayerObject == null || !CanAccess(client.PlayerObject.GetComponent<PlayerNetwork>()))
            return;

        Inventory inventory = client.PlayerObject.GetComponent<Inventory>();
        if (inventory == null || !inventory.IsSpawned || inventory.Database != database ||
            slots.Count != capacity || inventory.Slots.Count != Inventory.SlotCount)
            return;

        InventorySlot[] playerCopy = new InventorySlot[Inventory.SlotCount];
        InventorySlot[] containerCopy = new InventorySlot[slots.Count];
        for (int i = 0; i < playerCopy.Length; i++) playerCopy[i] = inventory.Slots[i];
        for (int i = 0; i < containerCopy.Length; i++) containerCopy[i] = slots[i];

        InventorySlot[] source = fromContainer ? containerCopy : playerCopy;
        InventorySlot[] target = fromContainer ? playerCopy : containerCopy;
        if (sourceIndex < 0 || sourceIndex >= source.Length || source[sourceIndex].IsEmpty)
            return;

        InventorySlot taken = source[sourceIndex];
        ItemDefinition item = database.GetByIndex(taken.itemIndex);
        if (item == null || !item.allowInContainers ||
            !fromContainer && inventory.GetCooldownRemaining(sourceIndex) > 0f)
            return;

        int amount = wholeStack ? taken.count : 1;
        int preferred = fromContainer && selectedPlayerSlot >= 0 &&
            selectedPlayerSlot < Inventory.SlotCount ? selectedPlayerSlot : -1;
        if (!Inventory.TryFit(target, taken.itemIndex, amount,
            item, preferred, taken.charges))
            return;

        source[sourceIndex] = taken.count > amount
            ? new InventorySlot(taken.itemIndex, taken.count - amount,
                item.maxCharges > 0 ? item.maxCharges : taken.charges) : InventorySlot.Empty;

        // Both copies have passed validation. No await/yield occurs between these writes.
        inventory.ApplyContainerTransfer(playerCopy);
        for (int i = 0; i < containerCopy.Length; i++)
            if (!slots[i].Equals(containerCopy[i]))
                slots[i] = containerCopy[i];
    }

    public bool TryAdd(ItemDefinition definition, int amount)
    {
        if (!IsServer || !IsSpawned || definition == null || !definition.allowInContainers ||
            amount <= 0 || database == null || slots.Count != capacity)
            return false;

        int index = database.GetIndex(definition);
        if (index < 0)
            return false;
        InventorySlot[] copy = new InventorySlot[slots.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = slots[i];

        if (!Inventory.TryFit(copy, index, amount, definition, -1, definition.maxCharges))
            return false;

        for (int i = 0; i < copy.Length; i++)
            if (!slots[i].Equals(copy[i]))
                slots[i] = copy[i];

        return true;
    }

    private bool CanAccess(PlayerNetwork player)
    {
        return IsSpawned && player != null && player.IsSpawned &&
            player.GetComponent<Health>() is Health health && health.IsAlive() &&
            !health.IsStunned &&
            IsPlayerInInteractionRange(player);
    }

}
