using UnityEngine;

public abstract class ItemUseAction : ScriptableObject
{
    public abstract bool CanUse(
        PlayerNetwork user,
        Inventory inventory,
        int slotIndex);

    public abstract void Use(
        PlayerNetwork user,
        Inventory inventory,
        int slotIndex);
}
