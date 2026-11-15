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

        if (player == null || player.Body == null)
            return;

        // Body points to LegsPoint (a child of the solid Body collider), not
        // to the collider's own transform. Ignore the taller combat hitbox.
        Collider2D bodyCollider = player.Body.GetComponentInParent<Collider2D>();
        if (other != bodyCollider)
            return;

        player.EnterBuilding(
            targetPoint.position,
            transitionSoundIndex
        );
    }
}
