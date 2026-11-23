using Unity.Netcode;
using UnityEngine;

public class DamageReflector : MonoBehaviour
{
    [SerializeField, Range(0f, 2f)] private float reflectFraction = 1f;
    [SerializeField] private Health health;
    private Health nextNonPlayerAttacker;

    public void SetNextNonPlayerAttacker(Health attacker)
    {
        if (health != null && health.IsServer)
            nextNonPlayerAttacker = attacker;
    }

    public void ClearNextNonPlayerAttacker(Health attacker)
    {
        if (nextNonPlayerAttacker == attacker)
            nextNonPlayerAttacker = null;
    }

    private void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();
    }

    private void Start()
    {
        if (health != null)
            health.Damaged += OnDamaged;
    }

    private void OnDestroy()
    {
        if (health != null)
            health.Damaged -= OnDamaged;
    }

    private void OnDamaged(DamageEvent damageEvent)
    {
        Health nonPlayer = nextNonPlayerAttacker;
        nextNonPlayerAttacker = null;
        if (health == null || !health.IsServer)
            return;

        if (damageEvent.Source == DamageSource.Reflect)
            return;

        if (damageEvent.Amount <= 0 || reflectFraction <= 0f)
            return;

        Health attackerHealth = nonPlayer != null
            ? nonPlayer : FindPlayerHealth(damageEvent.AttackerClientId);

        if (attackerHealth == null || attackerHealth == health)
            return;

        int reflected = Mathf.Max(
            1,
            Mathf.RoundToInt(
                damageEvent.Amount * reflectFraction
            )
        );

        // The reflection has no projectile collision point. Use the attacker's
        // visible sprite instead of its ground-level root for the hit particles.
        SpriteRenderer attackerVisual = attackerHealth.GetComponent<HeroSorting>()?.HeroRenderer;
        if (attackerVisual == null)
            attackerVisual = attackerHealth.GetComponentInChildren<SpriteRenderer>();
        Vector2 hitPosition = attackerVisual != null
            ? attackerVisual.bounds.center : attackerHealth.transform.position;
        attackerHealth.ShowDamageHit(hitPosition, reflected);
        attackerHealth.TakeDamage(
            reflected,
            damageEvent.AttackerClientId,
            DamageSource.Reflect
        );
    }

    private static Health FindPlayerHealth(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
            return null;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
            clientId,
            out NetworkClient client))
        {
            return null;
        }

        if (client.PlayerObject == null)
            return null;

        return client.PlayerObject.GetComponent<Health>();
    }
}
