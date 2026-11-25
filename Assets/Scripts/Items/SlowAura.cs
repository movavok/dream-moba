using UnityEngine;

// The aura requests a configured negative status; Health owns immunity, cleansing,
// modifier lifetime and visual removal. Re-entering after immunity is a new hit.
[RequireComponent(typeof(WorldItem))]
public sealed class SlowAura : MonoBehaviour
{
    [SerializeField, Min(0f)] private float radius = 3f;
    [SerializeField] private LayerMask heroLayers = 1 << 7;
    [SerializeField] private StatusDefinition slowStatus;
    [SerializeField, Min(0.05f)] private float scanInterval = 0.1f;
    private WorldItem worldItem;
    private float nextScan;

    private void Awake() => worldItem = GetComponent<WorldItem>();

    private void Update()
    {
        if (worldItem == null || !worldItem.IsServer || !worldItem.IsLanded ||
            slowStatus == null || Time.time < nextScan) return;
        nextScan = Time.time + scanInterval;
        foreach (Collider2D collider in Physics2D.OverlapCircleAll(transform.position, radius, heroLayers))
        {
            Health health = collider.GetComponentInParent<Health>();
            if (health != null && health.GetComponent<PlayerNetwork>() != null)
                health.ApplyStatus(slowStatus);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0f, 0.8f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
