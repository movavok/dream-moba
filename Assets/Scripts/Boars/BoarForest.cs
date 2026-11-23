using UnityEngine;

// Put this on each forest's trigger polygon. The polygon is a boundary, not a wall.
[RequireComponent(typeof(PolygonCollider2D))]
public sealed class BoarForest : MonoBehaviour
{
    [SerializeField] private PolygonCollider2D area;
    [SerializeField] private LayerMask blockingLayers = 1 << 0;
    [SerializeField] private LayerMask spawnOccupancyLayers = (1 << 0) | (1 << 6) | (1 << 7);
    [SerializeField, Min(0.05f)] private float bodyRadius = 0.8f;
    private readonly Collider2D[] overlaps = new Collider2D[12];
    private readonly RaycastHit2D[] pathHits = new RaycastHit2D[12];

    public float BodyRadius => bodyRadius;
    public LayerMask BlockingLayers => blockingLayers;

    private void Awake()
    {
        if (area == null) area = GetComponent<PolygonCollider2D>();
    }

    public bool Contains(Vector2 point)
    {
        if (area == null) return false;
        // Check the entire body, including near concave polygon edges.
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            if (!area.OverlapPoint(point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * bodyRadius))
                return false;
        }
        return area.OverlapPoint(point);
    }

    public bool IsFree(Vector2 point)
    {
        return Contains(point) && !Occupied(point, spawnOccupancyLayers);
    }

    private bool Occupied(Vector2 point, LayerMask layers)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(layers);
        filter.useTriggers = false;
        return Physics2D.OverlapCircle(point, bodyRadius, filter, overlaps) > 0;
    }

    public bool ClearPath(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.001f) return Contains(to);
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(blockingLayers);
        filter.useTriggers = false;
        if (!Contains(to) || Physics2D.CircleCast(from, bodyRadius, delta / distance,
            filter, pathHits, distance) > 0)
            return false;
        return ContainsSegment(from, to);
    }

    public bool ContainsSegment(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (!Contains(to)) return false;
        // A concave polygon can contain both endpoints but not the segment.
        int steps = Mathf.CeilToInt(distance / Mathf.Max(0.12f, bodyRadius));
        for (int i = 1; i < steps; i++)
            if (!Contains(Vector2.Lerp(from, to, i / (float)steps))) return false;
        return true;
    }

    public bool TryRandomPoint(out Vector2 point, Vector2 from, float minDistance = 0f)
    {
        point = from;
        if (area == null) return false;
        Bounds bounds = area.bounds;
        for (int i = 0; i < 36; i++)
        {
            Vector2 candidate = new Vector2(Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y));
            if (Vector2.Distance(candidate, from) < minDistance || !IsFree(candidate)) continue;
            point = candidate;
            return true;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (area == null) area = GetComponent<PolygonCollider2D>();
        if (area == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
    }
}
