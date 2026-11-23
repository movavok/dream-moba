using Unity.Netcode;
using UnityEngine;

// Server polls the authored trigger so actors spawned inside the fire ignite too.
public sealed class CampfireDangerZone : NetworkBehaviour
{
    [SerializeField] private CircleCollider2D dangerZone;
    private float nextScan;

    private void Update()
    {
        if (!IsServer || dangerZone == null || !dangerZone.enabled ||
            Time.time < nextScan)
            return;

        nextScan = Time.time + 0.1f;
        Vector2 center = dangerZone.transform.TransformPoint(dangerZone.offset);
        Vector3 scale = dangerZone.transform.lossyScale;
        float radius = dangerZone.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        foreach (Collider2D hit in hits)
        {
            if (hit == dangerZone) continue;
            Health health = hit.GetComponentInParent<Health>();
            if (health != null)
                health.Ignite();
        }
    }
}
