using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class ShopChest : NetworkBehaviour, IInteractable
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private InteractionOutline outline;
    [SerializeField] private GameObject panelPrefab;
    [SerializeField] private ShopCatalog catalog;
    public ShopCatalog Catalog => catalog;

    public string InteractionVerb => "Open";
    public string InteractionName => "shop";
    public Transform InteractionPromptTarget => transform;

    private void Awake()
    {
        if (outline != null && visual != null)
            outline.Configure(visual);
    }

    public bool CanInteract(PlayerNetwork player) => IsSpawned &&
        player != null && player.IsSpawned;

    public bool IsPlayerInInteractionRange(PlayerNetwork player)
    {
        InteractionDetector detector = player != null
            ? player.GetComponent<InteractionDetector>() : null;
        return IsSpawned && detector != null &&
            detector.IsWithinInteractionRange(NetworkObject, this);
    }

    public void SetHighlighted(bool highlighted) => outline?.SetHighlighted(highlighted);

    public void Interact(PlayerNetwork player)
    {
        if (!IsServer || !CanInteract(player) ||
            !IsPlayerInInteractionRange(player))
            return;

        OpenClientRpc(ShopProgress.Snapshot(player.OwnerClientId), new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { player.OwnerClientId } }
        });
    }

    [ClientRpc]
    private void OpenClientRpc(int progress, ClientRpcParams clientRpcParams = default)
    {
        ShopUI.Open(this, panelPrefab, progress);
    }

    [ServerRpc(RequireOwnership = false)]
    public void BuyServerRpc(ShopCategory category, int tier, int position,
        ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!IsServer || !IsSpawned || catalog == null ||
            !ShopCatalog.ValidPosition(category, tier, position) ||
            tier > ShopProgress.UnlockedTier(ShopProgress.Snapshot(clientId), category) ||
            NetworkManager == null ||
            !NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
            client.PlayerObject == null ||
            !client.PlayerObject.TryGetComponent(out PlayerNetwork player) ||
            !IsPlayerInInteractionRange(player) ||
            !player.TryGetComponent(out Health health) || !health.IsAlive() || health.IsStunned ||
            !player.TryGetComponent(out Inventory inventory))
            return;

        ShopOffer offer = catalog.Get(category, tier, position);
        EconomyManager economy = EconomyManager.Instance;
        if (offer == null || offer.item == null || offer.price < 0 ||
            economy == null || !economy.IsSpawned ||
            economy.GetGold(clientId) < offer.price ||
            !inventory.CanAdd(offer.item, 1))
            return;

        if (offer.price > 0 && !economy.TrySpendGold(clientId, offer.price))
            return;
        if (!inventory.TryAdd(offer.item, 1))
        {
            if (offer.price > 0) economy.AddGold(clientId, offer.price);
            return;
        }

        int progress = ShopProgress.RecordPurchase(clientId, category, tier);
        PurchasedClientRpc(progress, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
    }

    [ClientRpc]
    private void PurchasedClientRpc(int progress, ClientRpcParams clientRpcParams = default)
    {
        ShopUI.Purchased(this, progress);
    }

    public override void OnNetworkDespawn()
    {
        outline?.SetHighlighted(false);
        ShopUI.CloseIfCurrent(this);
    }

    private void OnDisable()
    {
        outline?.SetHighlighted(false);
        ShopUI.CloseIfCurrent(this);
    }
}
