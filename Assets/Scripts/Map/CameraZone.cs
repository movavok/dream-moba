using System.Collections.Generic;
using UnityEngine;

public class CameraZone : MonoBehaviour
{
    [SerializeField] private Collider2D cameraBounds;
    private static readonly List<CameraZone> Zones = new();

    private void OnEnable()
    {
        if (!Zones.Contains(this))
            Zones.Add(this);
    }

    private void OnDisable()
    {
        Zones.Remove(this);
    }

    public static Collider2D BoundsAt(Vector3 position)
    {
        foreach (CameraZone zone in Zones)
            if (zone != null && zone.cameraBounds != null &&
                zone.cameraBounds.enabled &&
                zone.cameraBounds.OverlapPoint(position))
                return zone.cameraBounds;

        return null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerNetwork player =
            other.GetComponentInParent<PlayerNetwork>();

        if (player == null || !player.IsOwner)
            return;

        PlayerCamera.Instance?.SetCameraBounds(cameraBounds);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerNetwork player =
            other.GetComponentInParent<PlayerNetwork>();

        if (player == null || !player.IsOwner)
            return;

        Debug.Log("CAMERA ZONE EXIT → MAP BOUNDS");

        PlayerCamera.Instance?.SetMapBounds();
    }
}
