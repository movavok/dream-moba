using UnityEngine;

public class CameraZone : MonoBehaviour
{
    [SerializeField] private Collider2D cameraBounds;

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerNetwork player =
            other.GetComponentInParent<PlayerNetwork>();

        if (player == null || !player.IsOwner)
            return;

        PlayerCamera.Instance.SetCameraBounds(cameraBounds);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerNetwork player =
            other.GetComponentInParent<PlayerNetwork>();

        if (player == null || !player.IsOwner)
            return;

        Debug.Log("CAMERA ZONE EXIT → MAP BOUNDS");

        PlayerCamera.Instance.SetMapBounds();
    }
}