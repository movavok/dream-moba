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
    private bool logicInitialized;

    private ulong attackerClientId;
    private NetworkObjectReference attackerReference;
    private Team attackerTeam;
    private bool teamColorApplied;
    private short teamId;

    private float damageMultiplier = 1f;
    private int attackerStreak;


    public void SetAttacker(PlayerNetwork player)
    {
        if (player == null)
            return;

        attackerReference = player.NetworkObject;
        attackerClientId = player.OwnerClientId;

        if (IsServer)
            networkAttackerReference.Value = attackerReference;
    }

    public void SetAttackerStats(PlayerStreak streak)
    {
        if (streak == null)
            return;

        damageMultiplier = streak.DamageMultiplier;
        attackerStreak = streak.Streak;
    }
    
    [SerializeField] private TrailRenderer trail;

    private NetworkVariable<bool> networkTrailEnabled =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private NetworkVariable<int> networkVisualColorIndex =
        new NetworkVariable<int>(
            -1,
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
        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>();

        foreach (SpriteRenderer renderer in renderers)
        {
            renderer.sortingLayerName =
                behind ? "ProjectileBehind" : "Projectile";
        }

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

    public void SetTrailColor(Color color)
    {
        if (trail == null)
            return;

        Color darkColor = color * 0.6f;
        darkColor.a = color.a;

        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(darkColor, 0f),
                new GradientColorKey(darkColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(darkColor.a, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        trail.colorGradient = gradient;
    }

    public float Range { get; private set; }

    private void ResolveAttackData()
    {
        if (dataResolved || ownerStreak == null)
            return;

        PlayerNetwork playerNetwork = ownerStreak.GetComponent<PlayerNetwork>();

        if (playerNetwork == null || playerNetwork.HeroData == null)
            return;

        AttackDefinition attack = playerNetwork.HeroData.attack;
        Range = attack.range;

        damage = attack.damage;
        speed = attack.projectileSpeed;

        if (speed > 0f)
        {
            lifeTime = Range / (speed * speedMultiplier);
        }

        dataResolved = true;
    }

    private void InitializeProjectileLogics()
    {
        if (!IsServer || !dataResolved || logicInitialized)
            return;

        IProjectileLogic[] logics =
            GetComponents<IProjectileLogic>();

        foreach (IProjectileLogic logic in logics)
        {
            logic.Initialize(this);
        }

        logicInitialized = true;
    }

    private void Update()
    {
        if (!teamColorApplied && networkTeamId.Value != 0)
            ApplyTeamColor();

        if (!IsServer)
            return;

        ResolveAttackData();

        InitializeProjectileLogics();

        transform.position += (Vector3)(direction * speed * speedMultiplier * Time.deltaTime);

        if (!lifeTimeScheduled && lifeTime > 0f)
        {
            lifeTimeScheduled = true;
            Invoke(nameof(DestroyProjectile), lifeTime);
        }
    }

    private void OnVisualColorChanged(
        int oldValue,
        int newValue)
    {
        ApplyVisualColor(newValue);
    }

    private void ApplyVisualColor(int colorIndex)
    {
        if (colorIndex < 0)
            return;

        IProjectileVisual visual =
            GetComponent<IProjectileVisual>();

        if (visual == null)
            return;

        visual.Initialize(colorIndex);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        networkTrailEnabled.OnValueChanged += OnTrailChanged;
        UpdateTrail(networkTrailEnabled.Value);

        networkBehindPlayer.OnValueChanged += OnBehindPlayerChanged;
        ApplySortingLayer(networkBehindPlayer.Value);

        networkVisualColorIndex.OnValueChanged += OnVisualColorChanged;
        ApplyVisualColor(networkVisualColorIndex.Value);

        networkTeamId.OnValueChanged += OnTeamIdChanged;
        networkAttackerReference.OnValueChanged += OnAttackerReferenceChanged;

        if (IsServer && attackerReference.TryGet(out _))
            networkAttackerReference.Value = attackerReference;

        ApplyTeamColor();

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

    private NetworkVariable<short> networkTeamId =
        new NetworkVariable<short>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private NetworkVariable<NetworkObjectReference> networkAttackerReference =
        new NetworkVariable<NetworkObjectReference>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public void SetTeam(short team)
    {
        teamId = team;

        if (IsServer)
            networkTeamId.Value = team;
    }

    private void ApplyTeamColor()
    {
        var sorting = GetComponent<ProjectileSorting>();
        if (sorting == null)
            return;

        if (!networkAttackerReference.Value.TryGet(
                out NetworkObject attackerObject,
                NetworkManager.Singleton))
            return;

        Team team = attackerObject.GetComponent<Team>();
        if (team == null)
            return;

        if (attackerTeam != team)
        {
            if (attackerTeam != null)
                attackerTeam.TeamId.OnValueChanged -= OnAttackerTeamIdChanged;

            attackerTeam = team;
            attackerTeam.TeamId.OnValueChanged += OnAttackerTeamIdChanged;
        }

        sorting.SetTeamColor(team.TeamColor);
        teamColorApplied = true;
    }

    private void OnTeamIdChanged(short oldValue, short newValue)
    {
        teamColorApplied = false;
        ApplyTeamColor();
    }

    private void OnAttackerReferenceChanged(
        NetworkObjectReference oldValue,
        NetworkObjectReference newValue)
    {
        teamColorApplied = false;
        ApplyTeamColor();
    }

    private void OnAttackerTeamIdChanged(short oldValue, short newValue)
    {
        teamColorApplied = false;
        ApplyTeamColor();
    }

    public override void OnNetworkDespawn()
    {
        if (attackerTeam != null)
            attackerTeam.TeamId.OnValueChanged -= OnAttackerTeamIdChanged;

        networkTeamId.OnValueChanged -= OnTeamIdChanged;
        networkAttackerReference.OnValueChanged -= OnAttackerReferenceChanged;

        base.OnNetworkDespawn();
    }

    private PlayerStreak ownerStreak;

    public void SetOwnerStreak(PlayerStreak streak)
    {
        ownerStreak = streak;
    }

    public void InitializeVisual()
    {
        if (!IsServer)
            return;

        if (ownerStreak == null)
            return;

        IProjectileVisual visual =
            GetComponent<IProjectileVisual>();

        if (visual == null)
            return;

        int colorCount = 0;

        if (visual is GuitaristProjectileVisual guitaristVisual)
        {
            colorCount = guitaristVisual.ColorCount;
        }

        if (colorCount <= 0)
            return;

        int colorIndex =
            ownerStreak.Streak % colorCount;

        networkVisualColorIndex.Value = colorIndex;

        ApplyVisualColor(colorIndex);
    }

    [ClientRpc]
    private void PlayHitSoundClientRpc(
        Vector2 position,
        int streak,
        ulong attackerClientId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
            attackerClientId,
            out NetworkClient client))
        {
            return;
        }

        PlayerNetwork attacker =
            client.PlayerObject.GetComponent<PlayerNetwork>();

        if (attacker == null)
            return;

        HeroAudio heroAudio =
            attacker.GetComponentInChildren<HeroAudio>();

        if (heroAudio == null)
            return;

        heroAudio.PlayAttackHit(
            position,
            streak
        );
    }

    private bool hasHit;
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer || hasHit)
            return;

        if (other.CompareTag("Blocker"))
        {
            hasHit = true;
            DestroyProjectile();
            return;
        }

        Health health = other.GetComponentInParent<Health>();
        Team targetTeam = other.GetComponentInParent<Team>();

        if (health == null || targetTeam == null)
            return;

        if (!targetTeam.IsEnemyFor(teamId))
            return;

        if (health.IsAlive())
        {
            hasHit = true;

            int finalDamage =
                Mathf.RoundToInt(
                    damage * damageMultiplier
                );

            Vector2 hitPosition =
                other.ClosestPoint(transform.position);

            bool targetWasProtected =
                health.IsSpawnProtected;

            health.TakeDamage(
                finalDamage,
                attackerClientId
            );

            health.ShowDamageHit(
                hitPosition,
                finalDamage
            );

            PlayHitSoundClientRpc(
                hitPosition,
                attackerStreak,
                attackerClientId
            );

            if (!targetWasProtected)
            {
                ownerStreak?.AddStreak();
            }

            DestroyProjectile();
        }
    }
}
