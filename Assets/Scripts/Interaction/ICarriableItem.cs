using UnityEngine;

public interface ICarriableItem
{
    ItemDefinition InventoryItem { get; }
    bool IsCarriedBy(PlayerNetwork player);
    void Throw(PlayerNetwork player, Vector2 direction, float distanceMultiplier);
}
