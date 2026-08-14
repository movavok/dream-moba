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

    [SerializeField] private TrailRenderer trail;

    private NetworkVariable<bool> networkTrailEnabled =
    new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public void SetTrail(bool enabled)
    {
        if (!IsServer)
            return;

        networkTrailEnabled.Value = enabled;
    }

    private void UpdateTrail(bool enabled)
    {
        if (trail == null)
            return;

        trail.emitting = enabled;
    }

    private void OnTrailChanged(bool oldValue, bool newValue)
    {
        UpdateTrail(newValue);
    }

    private NetworkVariable<bool> networkBehindPlayer =
    new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public void SetBehindPlayer(bool behind)
    {
        if (!IsServer)
            return;

        networkBehindPlayer.Value = behind;
    }

    private void ApplySortingLayer(bool behind)
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();

        if (renderer == null)
            return;

        renderer.sortingLayerName =
            behind ? "ProjectileBehind" : "Projectile";
    }

    private void OnBehindPlayerChanged(
        bool oldValue,
        bool newValue)
    {
        ApplySortingLayer(newValue);
    }

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
        base.OnNetworkSpawn();

        networkTrailEnabled.OnValueChanged += OnTrailChanged;
        UpdateTrail(networkTrailEnabled.Value);

        networkBehindPlayer.OnValueChanged += OnBehindPlayerChanged;
        ApplySortingLayer(networkBehindPlayer.Value);

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
