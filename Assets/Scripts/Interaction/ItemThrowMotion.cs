using System.Collections;
using UnityEngine;

public class ItemThrowMotion : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private float spawnForwardOffset = 0.1f;
    [SerializeField] private float spawnHeightOffset = -0.1f;

    [Header("Throw")]
    [SerializeField] private float throwDistance = 0.85f;
    [SerializeField] private float throwDuration = 0.35f;
    [SerializeField] private float throwArcHeight = 0.25f;

    [Header("Collision")]
    [SerializeField] private LayerMask obstacleLayers = ~0;
    [SerializeField] private float collisionRadius = 0.05f;
    [SerializeField] private float collisionSkin = 0.02f;

    [Header("Unstuck")]
    [SerializeField] private float unstuckStep = 0.08f;
    [SerializeField] private int unstuckSteps = 20;
    [SerializeField] private float unstuckDuration = 0.12f;

    [Header("Bounce")]
    [SerializeField] private float bounceDuration = 0.16f;
    [SerializeField] private float bounceHeight = 0.08f;

    public IEnumerator Play(
        Vector3 origin,
        Vector2 direction,
        float distanceMultiplier,
        Transform ignoreRoot = null)
    {
        direction = direction.sqrMagnitude > 0.001f
            ? direction.normalized
            : Vector2.down;

        Vector3 start = GetSafeStart(origin, direction, ignoreRoot);

        transform.position = start;
        transform.rotation = Quaternion.identity;

        Vector3 targetEnd =
            start + (Vector3)(direction * throwDistance * distanceMultiplier);

        Vector3 end = GetClampedEnd(start, targetEnd, ignoreRoot);

        float elapsed = 0f;
        float rotationSpeed = Random.Range(-540f, 540f);
        bool hitObstacle = false;

        while (elapsed < throwDuration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsed / throwDuration);

            float arc =
                Mathf.Sin(progress * Mathf.PI) * throwArcHeight;

            Vector3 position =
                Vector3.Lerp(start, end, progress) +
                Vector3.up * arc;

            // Sweep each animated step: overlap at the destination alone can
            // miss thin or irregular tiles between two rendered frames.
            Vector3 safePosition = GetClampedEnd(
                transform.position, position, ignoreRoot);
            if ((safePosition - position).sqrMagnitude > 0.000001f ||
                IsOverlappingObstacle(position, ignoreRoot))
            {
                end = ResolveOverlapPosition(safePosition, ignoreRoot, start);
                transform.position = end;
                hitObstacle = true;
                break;
            }

            transform.position = position;

            transform.Rotate(
                0f,
                0f,
                rotationSpeed * Time.deltaTime
            );

            yield return null;
        }

        if (!hitObstacle)
            transform.position = end;

        end = ResolveOverlapPosition(transform.position, ignoreRoot, start);
        yield return MoveToPosition(end);

        yield return Bounce(end);

        Vector3 settled = ResolveOverlapPosition(transform.position, ignoreRoot, start);
        if ((settled - transform.position).sqrMagnitude > 0.000001f)
            yield return MoveToPosition(settled);
    }

    private Vector3 GetSafeStart(
        Vector3 origin,
        Vector2 direction,
        Transform ignoreRoot)
    {
        Vector3 desiredStart =
            origin +
            (Vector3)(direction * spawnForwardOffset) +
            Vector3.up * spawnHeightOffset;

        Vector2 delta = desiredStart - origin;
        float distance = delta.magnitude;

        if (distance <= 0.0001f)
            return desiredStart;

        Vector2 castDirection = delta / distance;
        float safeDistance = distance;
        bool hitObstacle = false;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            origin,
            collisionRadius,
            castDirection,
            distance,
            obstacleLayers
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;

            if (!IsBlockingCollider(hitCollider, ignoreRoot))
                continue;

            if (hits[i].distance < safeDistance)
            {
                safeDistance = hits[i].distance;
                hitObstacle = true;
            }
        }

        if (hitObstacle)
            safeDistance = Mathf.Max(0f, safeDistance - collisionSkin);

        Vector3 start = origin + (Vector3)(castDirection * safeDistance);

        if (IsOverlappingObstacle(start, ignoreRoot))
            start = origin + Vector3.up * spawnHeightOffset;

        if (IsOverlappingObstacle(start, ignoreRoot))
            start = origin;

        return start;
    }

    private Vector3 GetClampedEnd(
        Vector3 start,
        Vector3 targetEnd,
        Transform ignoreRoot)
    {
        Vector2 delta = targetEnd - start;
        float distance = delta.magnitude;

        if (distance <= 0.0001f)
            return start;

        Vector2 direction = delta / distance;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            start,
            collisionRadius,
            direction,
            distance,
            obstacleLayers
        );

        float nearestDistance = distance;
        bool hitObstacle = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;

            if (!IsBlockingCollider(hitCollider, ignoreRoot))
                continue;

            if (hits[i].distance < nearestDistance)
            {
                nearestDistance = hits[i].distance;
                hitObstacle = true;
            }
        }

        if (hitObstacle)
            nearestDistance = Mathf.Max(0f, nearestDistance - collisionSkin);

        return start + (Vector3)(direction * nearestDistance);
    }

    private bool IsOverlappingObstacle(Vector3 position, Transform ignoreRoot)
    {
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(
            position,
            collisionRadius,
            obstacleLayers
        );

        for (int i = 0; i < overlaps.Length; i++)
        {
            if (IsBlockingCollider(overlaps[i], ignoreRoot))
                return true;
        }

        return false;
    }

    private bool IsBlockingCollider(Collider2D hitCollider, Transform ignoreRoot)
    {
        if (hitCollider == null || hitCollider.isTrigger)
            return false;

        if (hitCollider.transform.IsChildOf(transform))
            return false;

        if (ignoreRoot != null &&
            hitCollider.transform.IsChildOf(ignoreRoot))
            return false;

        return true;
    }

    private Vector3 ResolveOverlapPosition(
        Vector3 position,
        Transform ignoreRoot,
        Vector3 fallback)
    {
        if (!IsOverlappingObstacle(position, ignoreRoot))
            return position;

        Vector2[] directions =
        {
            Vector2.right, Vector2.left, Vector2.up, Vector2.down,
            new Vector2(1f, 1f).normalized, new Vector2(1f, -1f).normalized,
            new Vector2(-1f, 1f).normalized, new Vector2(-1f, -1f).normalized,
            new Vector2(1f, 0.5f).normalized, new Vector2(1f, -0.5f).normalized,
            new Vector2(-1f, 0.5f).normalized, new Vector2(-1f, -0.5f).normalized,
            new Vector2(0.5f, 1f).normalized, new Vector2(0.5f, -1f).normalized,
            new Vector2(-0.5f, 1f).normalized, new Vector2(-0.5f, -1f).normalized
        };

        for (int step = 1; step <= unstuckSteps; step++)
        {
            float distance = unstuckStep * step;

            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 candidate =
                    position + (Vector3)(directions[i] * distance);

                if (!IsOverlappingObstacle(candidate, ignoreRoot))
                    return candidate;
            }
        }

        Vector2 toFallback = (Vector2)(fallback - position);
        float fallbackDistance = toFallback.magnitude;

        if (fallbackDistance > 0.0001f)
        {
            Vector2 fallbackDir = toFallback / fallbackDistance;
            int fallbackSteps = Mathf.Max(unstuckSteps, 30);

            for (int step = 1; step <= fallbackSteps; step++)
            {
                float t = step / (float)fallbackSteps;
                Vector3 candidate = Vector3.Lerp(position, fallback, t);

                if (!IsOverlappingObstacle(candidate, ignoreRoot))
                    return candidate;
            }

            if (!IsOverlappingObstacle(fallback, ignoreRoot))
                return fallback;
        }

        return position;
    }

    private IEnumerator MoveToPosition(Vector3 target)
    {
        Vector3 start = transform.position;

        if ((target - start).sqrMagnitude <= 0.000001f)
        {
            transform.position = target;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < unstuckDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(
                unstuckDuration <= 0f ? 1f : elapsed / unstuckDuration
            );

            transform.position = Vector3.Lerp(start, target, progress);
            yield return null;
        }

        transform.position = target;
    }

    private IEnumerator Bounce(Vector3 position)
    {
        float elapsed = 0f;
        Quaternion startRotation = transform.rotation;

        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsed / bounceDuration);

            float bounce =
                Mathf.Sin(progress * Mathf.PI) * bounceHeight;

            transform.position =
                position + Vector3.up * bounce;

            transform.rotation = Quaternion.Lerp(
                startRotation,
                Quaternion.identity,
                progress
            );

            yield return null;
        }

        transform.position = position;
        transform.rotation = Quaternion.identity;
    }
}
