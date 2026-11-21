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

    // Called on every client after a successful use.
    // Sounds should follow the hero who used the item.
    public virtual void PlayUseSound(Transform user, string group)
    {
    }
}
