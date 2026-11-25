using UnityEngine;

// The existing WorldItem owns flight and network despawn; this only detects a grounded step.
[RequireComponent(typeof(WorldItem))]
public sealed class BananaPeelTrap : MonoBehaviour
{
    [SerializeField, Min(0f)] private float stunDuration = 1f;
    [SerializeField] private Collider2D trapCollider;
    [SerializeField] private LayerMask bodyLayers = 1 << 6;
    [SerializeField] private StatusDefinition stunStatus;

    private readonly Collider2D[] hits = new Collider2D[24];
    private WorldItem item;
    private bool consumed;

    private void Awake() => item = GetComponent<WorldItem>();

    private void FixedUpdate()
    {
        if (consumed || item == null || !item.IsServer || !item.IsSpawned ||
            item.IsFlying || !item.WasConsumedByThrow || trapCollider == null || stunStatus == null)
            return;

        ContactFilter2D filter = new();
        filter.SetLayerMask(bodyLayers);
        filter.useTriggers = true;
        int count = trapCollider.Overlap(filter, hits);
        for (int i = 0; i < count; i++)
        {
            Collider2D body = hits[i];
            if (body == null || body.isTrigger || body == trapCollider ||
                (bodyLayers.value & (1 << body.gameObject.layer)) == 0)
                continue;
            Health victim = body.GetComponentInParent<Health>();
            if (victim == null || !victim.IsAlive()) continue;
            if (!victim.ApplyStatus(stunStatus, durationOverride: stunDuration)) continue;
            consumed = true;
            item.NetworkObject.Despawn(true);
            return;
        }
    }
}
