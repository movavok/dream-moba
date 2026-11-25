using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(WorldItem))]
public sealed class RottenTomatoImpact : NetworkBehaviour, IThrownItemImpact
{
    [SerializeField, Min(0)] private int damage = 25;
    [SerializeField, Min(0.01f)] private float splashRadius = 0.65f;
    [SerializeField, Min(0.01f)] private float splashDuration = 0.32f;
    [SerializeField, Min(0.01f)] private float flightHitRadius = 0.12f;
    [SerializeField] private StatusDefinition blindness;
    [SerializeField] private GameObject animatedSplashPrefab;
    [SerializeField] private LayerMask hitboxLayers = 1 << 7;
    private readonly HashSet<Health> hit = new();
    private readonly Collider2D[] splashHits = new Collider2D[32];
    private ulong throwerId = ulong.MaxValue;
    private short throwerTeam;
    private bool broken;
    private bool missingShapeWarned;

    public void OnThrowStart(Transform thrower)
    {
        if (!IsServer) return;
        PlayerNetwork player = thrower != null ? thrower.GetComponent<PlayerNetwork>() : null;
        throwerId = player != null ? player.OwnerClientId : ulong.MaxValue;
        throwerTeam = thrower != null ? thrower.GetComponent<Team>()?.TeamId.Value ?? (short)0 : (short)0;
        hit.Clear(); broken = false;
    }

    public bool Sweep(Vector2 from, Vector2 to)
    {
        if (!IsServer || broken) return false;
        Vector2 delta = to - from;
        RaycastHit2D[] results = Physics2D.CircleCastAll(from, flightHitRadius,
            delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right,
            delta.magnitude, hitboxLayers);
        foreach (RaycastHit2D result in results)
        {
            if (!TryEnemy(result.collider, out Health victim)) continue;
            Vector2 position = result.collider.ClosestPoint(to);
            transform.position = position;
            Break(position, victim);
            return true;
        }
        return false;
    }

    private bool TryEnemy(Collider2D collider, out Health victim)
    {
        victim = collider != null && collider.isTrigger && collider.gameObject.layer == 7
            ? collider.GetComponentInParent<Health>() : null;
        Team team = victim != null ? victim.GetComponent<Team>() : null;
        return victim != null && victim.IsAlive() && team != null &&
            team.IsEnemyFor(throwerTeam);
    }

    public void Impact(Vector2 point)
    {
        Break(point, null);
    }

    private void DamageTarget(Health victim, Vector2 position)
    {
        if (!hit.Add(victim)) return;
        victim.ShowDamageHit(position, damage);
        victim.TakeDamage(damage, throwerId, DamageSource.Projectile);
        if (victim != null && victim.IsSpawned && victim.IsAlive())
            victim.ApplyStatus(blindness, throwerId);
    }

    private void Break(Vector2 point, Health direct)
    {
        if (!IsServer || broken) return;
        broken = true;
        if (direct != null) DamageTarget(direct, point);
        StartCoroutine(DamageSplashOverTime(point));
        ShowSplashClientRpc(point, splashDuration);
    }

    private IEnumerator DamageSplashOverTime(Vector2 point)
    {
        GameObject serverVisual = animatedSplashPrefab != null && IsServer
            ? Instantiate(animatedSplashPrefab, point, Quaternion.identity) : null;
        SpriteRenderer animatedSprite = serverVisual != null
            ? serverVisual.GetComponentInChildren<SpriteRenderer>() : null;
        SplashFrameColliders frameColliders = serverVisual != null
            ? serverVisual.GetComponent<SplashFrameColliders>() : null;
        if (serverVisual != null)
            foreach (SpriteRenderer renderer in serverVisual.GetComponentsInChildren<SpriteRenderer>())
                renderer.enabled = false;
        float elapsed = 0f;
        while (elapsed < splashDuration)
        {
            DamageSplash(point, splashRadius, frameColliders, animatedSprite);
            elapsed += Time.deltaTime;
            yield return null;
        }
        DamageSplash(point, splashRadius, frameColliders, animatedSprite);
        if (serverVisual != null) Destroy(serverVisual);
        if (IsSpawned) NetworkObject.Despawn();
    }

    private void DamageSplash(Vector2 point, float radius,
        SplashFrameColliders frames, SpriteRenderer sprite)
    {
        if (frames != null && frames.HasFrames)
        {
            Collider2D shape = sprite != null ? frames.Select(sprite.sprite) : null;
            if (shape == null)
            {
                if (!missingShapeWarned)
                {
                    missingShapeWarned = true;
                    Debug.LogWarning("[RottenTomatoImpact] No splash collider bound to the current animation sprite.", this);
                }
                return;
            }
            Physics2D.SyncTransforms();
            ContactFilter2D filter = new();
            filter.SetLayerMask(hitboxLayers);
            filter.useTriggers = true;
            int count = shape.Overlap(filter, splashHits);
            for (int i = 0; i < count; i++)
                if (TryEnemy(splashHits[i], out Health victim))
                    DamageTarget(victim, splashHits[i].ClosestPoint(point));
            return;
        }
        foreach (Collider2D collider in Physics2D.OverlapCircleAll(point, radius, hitboxLayers))
            if (TryEnemy(collider, out Health victim))
                DamageTarget(victim, collider.ClosestPoint(point));
    }

    [ClientRpc]
    private void ShowSplashClientRpc(Vector2 position, float duration)
    {
        foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(true))
            sprite.enabled = false;

        if (animatedSplashPrefab == null)
            return;

        GameObject animatedEffect = Instantiate(
            animatedSplashPrefab, position, Quaternion.identity);

        ExpandingSplashVisual visual = animatedEffect.GetComponent<ExpandingSplashVisual>();

        if (visual == null)
            visual = animatedEffect.AddComponent<ExpandingSplashVisual>();

        visual.Play(duration);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.12f, 0.09f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, splashRadius);
    }
}
