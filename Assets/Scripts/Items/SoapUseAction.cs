using UnityEngine;

[CreateAssetMenu(menuName = "Game/Items/Soap Use")]
public sealed class SoapUseAction : ItemUseAction
{
    [SerializeField, Min(0.01f)] private float immunityDuration = 3f;
    public override bool CanUse(PlayerNetwork user, Inventory inventory, int slotIndex) =>
        user != null && user.IsServer && user.IsSpawned && inventory != null &&
        inventory.GetItem(slotIndex)?.useAction == this &&
        !inventory.GetSlot(slotIndex).IsEmpty &&
        user.GetComponent<Health>() is Health health && health.IsAlive();

    public override void Use(PlayerNetwork user, Inventory inventory, int slotIndex)
    {
        if (!CanUse(user, inventory, slotIndex) || !inventory.TryRemove(slotIndex, 1))
            return;
        user.GetComponent<Health>().CleanseAndProtect(this, immunityDuration);
    }
}
