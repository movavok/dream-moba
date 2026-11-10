using UnityEngine;

public interface ICarriableItem
{
    bool IsCarriedBy(PlayerNetwork player);
    void Throw(PlayerNetwork player, Vector2 direction);
}
