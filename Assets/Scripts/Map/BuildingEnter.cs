using UnityEngine;
using Unity.Netcode;

public class BuildingEnter : MonoBehaviour
{
    [SerializeField] private Transform targetPoint;
    [SerializeField] private int transitionSoundIndex;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        PlayerNetwork player =
            other.GetComponentInParent<PlayerNetwork>();

        if (player == null)
            return;

        player.EnterBuilding(
            targetPoint.position,
            transitionSoundIndex
        );
    }
}