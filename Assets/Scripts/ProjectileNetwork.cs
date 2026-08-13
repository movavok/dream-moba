using Unity.Netcode;
using UnityEngine;

public class ProjectileNetwork : NetworkBehaviour
{
    private float speed;
    private int damage;
    private float lifeTime;

    private Vector2 direction;
    private bool dataResolved;
    private bool lifeTimeScheduled;

    public void SetDirection(Vector2 newDirection)
    {
        direction = newDirection.normalized;
    }

    private float speedMultiplier = 1f;

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    private void ResolveAttackData()
    {
        if (dataResolved || ownerStreak == null)
            return;

        PlayerNetwork playerNetwork = ownerStreak.GetComponent<PlayerNetwork>();

        if (playerNetwork == null || playerNetwork.HeroData == null)
            return;

        AttackDefinition attack = playerNetwork.HeroData.attack;

        damage = attack.damage;
        speed = attack.projectileSpeed;

        if (speed > 0f)
        {
            lifeTime = attack.range / speed;
        }

        dataResolved = true;
    }

    private void Update()
    {
        if (!IsServer)
            return;

        ResolveAttackData();

        transform.position += (Vector3)(direction * speed * speedMultiplier * Time.deltaTime);

        if (!lifeTimeScheduled && lifeTime > 0f)
        {
            lifeTimeScheduled = true;
            Invoke(nameof(DestroyProjectile), lifeTime);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            ResolveAttackData();

            if (!lifeTimeScheduled && lifeTime > 0f)
            {
                lifeTimeScheduled = true;
                Invoke(nameof(DestroyProjectile), lifeTime);
            }
        }
    }

    private void DestroyProjectile()
    {
        if (!IsServer || !NetworkObject.IsSpawned)
            return;

        NetworkObject.Despawn();
    }

    private short teamId;
    public void SetTeam(short team)
    {
        teamId = team;
    }

    private PlayerStreak ownerStreak;

    public void SetOwnerStreak(PlayerStreak streak)
    {
        ownerStreak = streak;
    }

    private bool hasHit;
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer || hasHit)
            return;

        Health health = other.GetComponentInParent<Health>();
        Team targetTeam = other.GetComponentInParent<Team>();

        if (health == null || targetTeam == null)
            return;

        // Check if target is teammate or neutral (teamId < 1)
        if (targetTeam.TeamId.Value == teamId || targetTeam.TeamId.Value < 1)
            return;

        if (health.IsAlive())
        {
            hasHit = true;

            float finalDamage = damage * ownerStreak.DamageMultiplier;
            health.TakeDamage(Mathf.RoundToInt(finalDamage));
            
            ownerStreak?.AddStreak();

            DestroyProjectile();
        }
    }
}
