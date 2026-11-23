using UnityEngine;

[CreateAssetMenu(fileName = "FriedMeatUse", menuName = "Game/Items/Fried Meat Use")]
public class FriedMeatUseAction : ItemUseAction
{
    [SerializeField, Min(0)] private int healAmount = 50;
    [SerializeField, Min(0)] private int permanentMaxHealth = 25;

    public override bool CanUse(PlayerNetwork user, Inventory inventory, int slotIndex)
    {
        return user != null && user.IsServer && user.IsSpawned &&
            inventory != null && inventory.GetItem(slotIndex)?.useAction == this &&
            !inventory.GetSlot(slotIndex).IsEmpty &&
            user.GetComponent<Health>() is Health health && health.IsAlive();
    }

    public override void Use(PlayerNetwork user, Inventory inventory, int slotIndex)
    {
        if (!CanUse(user, inventory, slotIndex) || !inventory.TryRemove(slotIndex, 1))
            return;

        Health health = user.GetComponent<Health>();
        health.AddPermanentMaxHealth(permanentMaxHealth);
        health.Heal(healAmount, true);
    }
}
