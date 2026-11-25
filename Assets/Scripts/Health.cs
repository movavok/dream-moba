using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Cinemachine;

public class Health : NetworkBehaviour
{
    public event System.Action Died;
    public event System.Action<DamageEvent> Damaged;
    public event System.Action<DamageEvent> DamageFeedback;
    private NetworkVariable<int> currentHealth = new NetworkVariable<int>();

    private NetworkVariable<int> maxHealthOverride =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<int> inventoryMaxHealth = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private StatController itemStats;
    private int permanentMaxHealthBonus;

    [SerializeField] private HealthFloatingText healthFloatingText;
    [SerializeField] private int standaloneMaxHealth;
    [SerializeField] private bool respawnOnDeath = true;
    [SerializeField] private bool standardRegenEnabled = true;
    [SerializeField] private ParticleSystem burnParticlesPrefab;

    private readonly NetworkVariable<double> burningUntil = new(0);
    [SerializeField] private StatusDefinition[] statusDefinitions;
    [SerializeField] private Transform statusHeadPoint;
    public Transform StatusHeadPoint => statusHeadPoint;
    [SerializeField] private Vector3 statusVisualOffset;
    public Vector3 StatusVisualOffset => statusVisualOffset;
    private readonly NetworkList<StatusEntry> statuses = new();
    private readonly NetworkVariable<double> debuffImmuneUntil = new(0);
    private ScriptableObject immunityVisualSource;
    private HeroEffectVisuals effectVisuals;
    [SerializeField] private ItemPopupVisual blockPopupPrefab;
    private ItemPopupVisual blockPopup;
    public event System.Action StatusChanged;
    public bool IsDebuffImmune => IsServer && IsSpawned &&
        NetworkManager.ServerTime.Time < debuffImmuneUntil.Value;
    private ParticleSystem burnParticles;
    private ParticleSystemRenderer burnRenderer;
    private SpriteRenderer burnAnchor;
    private readonly Collider2D[] burnHits = new Collider2D[16];
    private Collider2D[] ownHitboxes;
    private readonly HashSet<Health> ignitedTargets = new();
    private double nextBurnTick;
    private float nextSpreadScan;

    [Header("Audio")]
    [Tooltip("If set, replaces the global damage-taken sounds for this object. One is picked at random per hit.")]
    [SerializeField] private AudioClip[] damageClips;
    [Tooltip("Volume group for this object's sounds, e.g. Hero/Guitarist, MinionBoss/Scarecrow, Creep/Melee. Leave empty on players to use the hero group; otherwise Global is used.")]
    [SerializeField] private string audioGroup;

    private const float REGEN_DELAY = 3f;
    private const float REGEN_INTERVAL = 1f;
    private const int REGEN_AMOUNT = 10;

    private float lastDamageTime;
    private float regenTimer;
    private PlayerNetwork playerNetwork;

    public event System.Action<int, int> HealthChanged;

    private DamageHitEffect damageHitEffect;
    private DamageHitFeedback damageHitFeedback;

    private NetworkVariable<bool> spawnProtection =
    new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsSpawnProtected => spawnProtection.Value;

    public event System.Action<bool> SpawnProtectionChanged;
    
    private void Awake()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        var hitboxes = new List<Collider2D>();
        foreach (Collider2D collider in colliders)
            if (collider.gameObject.layer == 7 && collider.isTrigger)
                hitboxes.Add(collider);
        ownHitboxes = hitboxes.ToArray();

        damageHitEffect = GetComponentInChildren<DamageHitEffect>();
        damageHitFeedback = GetComponentInChildren<DamageHitFeedback>();

        if (healthFloatingText == null)
            healthFloatingText = GetComponentInChildren<HealthFloatingText>();
    }

    private void Update()
    {
        if (IsServer && IsSpawned)
        {
            double now = NetworkManager.ServerTime.Time;
            for (int i = statuses.Count - 1; i >= 0; i--)
                if (statuses[i].endsAt <= now) RemoveStatusAt(i);
            if (debuffImmuneUntil.Value > 0 && now >= debuffImmuneUntil.Value)
                EndDebuffImmunity();
        }
        if (IsServer && IsSpawned && burningUntil.Value > 0)
            UpdateBurning();

        if (!IsServer || !standardRegenEnabled)
            return;

        if (!IsAlive())
            return;

        if (currentHealth.Value >= MaxHealth)
            return;

        if (Time.time < lastDamageTime + REGEN_DELAY)
        {
            regenTimer = 0f;
            return;
        }

        regenTimer += Time.deltaTime;

        if (regenTimer >= REGEN_INTERVAL)
        {
            regenTimer -= REGEN_INTERVAL;

            currentHealth.Value = Mathf.Min(
                currentHealth.Value + REGEN_AMOUNT,
                MaxHealth
            );

            ShowHealthNumberClientRpc(REGEN_AMOUNT);
        }
    }

    private void LateUpdate()
    {
        if (burnRenderer == null || burnAnchor == null) return;
        if (burnRenderer.sortingLayerID != burnAnchor.sortingLayerID)
            burnRenderer.sortingLayerID = burnAnchor.sortingLayerID;
        if (burnRenderer.sortingOrder != burnAnchor.sortingOrder + 1)
            burnRenderer.sortingOrder = burnAnchor.sortingOrder + 1;
    }

    public void ShowDamageHit(
        Vector2 hitPosition,
        float damage)
    {
        if (!IsServer)
            return;

        if (spawnProtection.Value)
            damage = 0f;

        ShowDamageHitClientRpc(
            hitPosition,
            damage
        );
    }

    public bool TryBlockBasicAttack(short attackerTeam, Vector2 position)
    {
        if (!IsServer || !IsSpawned || spawnProtection.Value ||
            GetComponent<Team>() is not Team team || !team.IsEnemyFor(attackerTeam) ||
            GetComponent<Inventory>() is not Inventory inventory ||
            !inventory.TryBlockBasicAttack(out int itemIndex)) return false;
        ShowDamageHit(position, 0);
        ShowHealthNumberClientRpc(0);
        BlockedAttackClientRpc(itemIndex);
        return true;
    }

    [ClientRpc]
    private void BlockedAttackClientRpc(int itemIndex)
    {
        if (blockPopupPrefab == null) return;
        Inventory inventory = GetComponent<Inventory>();
        ItemDefinition item = inventory != null ? inventory.Database?.GetByIndex(itemIndex) : null;
        WorldItem world = item != null && item.worldPrefab != null
            ? item.worldPrefab.GetComponent<WorldItem>() : null;
        if (world == null || world.WorldSprite == null) return;
        if (blockPopup == null) blockPopup = Instantiate(blockPopupPrefab, transform, false);
        blockPopup.Play(world.WorldSprite);
    }

    [ClientRpc]
    private void ShowDamageHitClientRpc(
        Vector2 hitPosition,
        float damage)
    {
        float radius = Mathf.Lerp(
            0.15f,
            0.5f,
            Mathf.Clamp01(damage / 100f)
        );

        damageHitEffect?.SetHit(
            hitPosition,
            radius,
            damage
        );

    }

    private string ResolveAudioGroup()
    {
        if (!string.IsNullOrEmpty(audioGroup))
            return audioGroup;

        if (playerNetwork == null)
            playerNetwork = GetComponent<PlayerNetwork>();

        if (playerNetwork != null && playerNetwork.HeroData != null)
            return playerNetwork.HeroData.AudioGroup;

        return AudioGroups.Global;
    }

    public int CurrentHealth => currentHealth.Value;
    public int PermanentMaxHealthBonus => permanentMaxHealthBonus;
    public int MaxHealth
    {
        get
        {
            if (inventoryMaxHealth.Value > 0)
                return inventoryMaxHealth.Value;

            if (playerNetwork != null && playerNetwork.HeroData != null)
                return playerNetwork.HeroData.stats.maxHealth;

            return maxHealthOverride.Value > 0
                ? maxHealthOverride.Value
                : standaloneMaxHealth;
        }
    }

    public void SetMaxHealth(int value)
    {
        if (!IsServer || value <= 0)
            return;

        maxHealthOverride.Value = value;
        currentHealth.Value = value;

        HealthChanged?.Invoke(
            currentHealth.Value,
            MaxHealth
        );
    }

    // Server-owned match progress; RespawnManager transfers it to the replacement hero.
    public void RestorePermanentMaxHealthBonus(int total)
    {
        if (!IsServer || total < 0 || GetComponent<PlayerNetwork>() == null)
            return;

        permanentMaxHealthBonus = total;
        UpdateInventoryMaxHealth();
        if (IsSpawned && IsAlive())
            currentHealth.Value = MaxHealth;
    }

    public void AddPermanentMaxHealth(int amount)
    {
        if (!IsServer || amount <= 0 || GetComponent<PlayerNetwork>() == null)
            return;

        permanentMaxHealthBonus += amount;
        UpdateInventoryMaxHealth();
    }

    public override void OnNetworkSpawn()
    {
        if (IsClient && GetComponent<StatusVisuals>() == null)
            gameObject.AddComponent<StatusVisuals>();
        effectVisuals = GetComponent<HeroEffectVisuals>();
        statuses.OnListChanged += OnStatusListChanged;
        burningUntil.OnValueChanged += OnBurningChanged;
        OnBurningChanged(0, burningUntil.Value);
        playerNetwork = GetComponent<PlayerNetwork>();

        itemStats = GetComponent<StatController>();
        if (IsServer && itemStats != null)
        {
            itemStats.StatsChanged += UpdateInventoryMaxHealth;
            UpdateInventoryMaxHealth();
        }
        inventoryMaxHealth.OnValueChanged += OnInventoryMaxHealthChanged;

        if (IsServer)
        {
            spawnProtection.Value = true;

            if (playerNetwork != null && playerNetwork.HeroData != null)
            {
                currentHealth.Value = MaxHealth;
            }
            else if (MaxHealth > 0)
            {
                currentHealth.Value = MaxHealth;
                spawnProtection.Value = false;
            }
        }

        spawnProtection.OnValueChanged += OnSpawnProtectionChanged;
        currentHealth.OnValueChanged += OnHealthChanged;

        Debug.Log("Spawned with HP: " + currentHealth.Value);

        HealthChanged?.Invoke(
            currentHealth.Value,
            MaxHealth
        );

        SpawnProtectionChanged?.Invoke(
            spawnProtection.Value
        );
        StatusChanged?.Invoke(); // Reconcile statuses already replicated for late join.
    }

    private void OnSpawnProtectionChanged(
        bool oldValue,
        bool newValue)
    {
        SpawnProtectionChanged?.Invoke(newValue);
    }

    private void OnHealthChanged(int oldHealth, int newHealth)
    {
        HealthChanged?.Invoke(
            newHealth,
            MaxHealth
        );
    }

    private void UpdateInventoryMaxHealth()
    {
        if (!IsServer || playerNetwork == null ||
            playerNetwork.HeroData == null || itemStats == null)
            return;

        int maximum = Mathf.Max(1, Mathf.RoundToInt(itemStats.Get(StatType.MaxHealth)) +
            permanentMaxHealthBonus);
        inventoryMaxHealth.Value = maximum;
        if (currentHealth.Value > maximum)
            currentHealth.Value = maximum;
    }

    private void OnInventoryMaxHealthChanged(int previous, int current)
    {
        HealthChanged?.Invoke(currentHealth.Value, MaxHealth);
    }

    public override void OnNetworkDespawn()
    {
        GetComponent<StatusVisuals>()?.ClearVisuals();
        statuses.OnListChanged -= OnStatusListChanged;
        if (IsServer)
        {
            for (int i = 0; i < statuses.Count; i++)
            {
                StatusDefinition definition = FindStatus(statuses[i].id);
                if (definition != null) itemStats?.RemoveSource(definition);
            }
            EndDebuffImmunity();
        }
        burningUntil.OnValueChanged -= OnBurningChanged;
        StopBurningVisual();
        ignitedTargets.Clear();
        if (itemStats != null)
            itemStats.StatsChanged -= UpdateInventoryMaxHealth;
        inventoryMaxHealth.OnValueChanged -= OnInventoryMaxHealthChanged;
        currentHealth.OnValueChanged -= OnHealthChanged;
        spawnProtection.OnValueChanged -= OnSpawnProtectionChanged;

        base.OnNetworkDespawn();
    }

    // Fire is server-owned; particle simulation on a client is only cosmetic.
    public void Ignite(bool refreshExisting = true)
    {
        if (!IsServer || !IsSpawned || !IsAlive() || spawnProtection.Value || IsDebuffImmune)
            return;

        double now = NetworkManager.ServerTime.Time;
        if (!refreshExisting && burningUntil.Value > now)
            return;

        if (burningUntil.Value >= now + 4d)
            return;

        if (burningUntil.Value <= now)
            nextBurnTick = now + 1d;
        burningUntil.Value = now + 5d;
    }

    public void CleanseAndProtect(ScriptableObject visualSource, float seconds)
    {
        if (!IsServer || !IsSpawned || !IsAlive()) return;
        burningUntil.Value = 0;
        ignitedTargets.Clear();
        for (int i = statuses.Count - 1; i >= 0; i--)
        {
            StatusDefinition definition = FindStatus(statuses[i].id);
            if (definition != null && definition.negative && definition.cleansable)
                RemoveStatusAt(i);
        }
        // Refresh a single immunity and its existing visual, never add durations.
        debuffImmuneUntil.Value = NetworkManager.ServerTime.Time + Mathf.Max(0f, seconds);
        immunityVisualSource = visualSource;
        effectVisuals?.SetActive(visualSource, true);
    }

    private void EndDebuffImmunity()
    {
        if (debuffImmuneUntil.Value <= 0) return;
        debuffImmuneUntil.Value = 0;
        effectVisuals?.SetActive(immunityVisualSource, false);
        immunityVisualSource = null;
    }

    private void OnStatusListChanged(NetworkListEvent<StatusEntry> change) => StatusChanged?.Invoke();
    public int StatusCount => statuses.Count;
    public double StatusEndsAt(int index) => index >= 0 && index < statuses.Count
        ? statuses[index].endsAt : 0;
    public bool HasSightBlockingStatus
    {
        get
        {
            for (int i = 0; i < statuses.Count; i++)
            {
                StatusDefinition definition = StatusAt(i);
                if (definition != null && definition.blocksDetection) return true;
            }
            return false;
        }
    }
    public bool IsStunned
    {
        get
        {
            for (int i = 0; i < statuses.Count; i++)
            {
                StatusDefinition definition = StatusAt(i);
                if (definition != null && definition.blocksActions) return true;
            }
            return false;
        }
    }
    public StatusDefinition StatusAt(int index) => index >= 0 && index < statuses.Count &&
        statuses[index].endsAt > (NetworkManager != null ? NetworkManager.ServerTime.Time : 0)
        ? FindStatus(statuses[index].id) : null;

    private StatusDefinition FindStatus(int id)
    {
        if (statusDefinitions == null) return null;
        foreach (StatusDefinition definition in statusDefinitions)
            if (definition != null && definition.id == id) return definition;
        return null;
    }

    public bool ApplyStatus(StatusDefinition definition, ulong sourceClientId = ulong.MaxValue,
        float durationOverride = -1f)
    {
        if (!IsServer || !IsSpawned || !IsAlive() || definition == null ||
            FindStatus(definition.id) != definition ||
            (definition.negative && !definition.bypassImmunity && spawnProtection.Value) ||
            (definition.negative && !definition.bypassImmunity && IsDebuffImmune)) return false;
        double end = NetworkManager.ServerTime.Time +
            (durationOverride >= 0f ? durationOverride : definition.duration);
        for (int i = 0; i < statuses.Count; i++)
        {
            StatusEntry entry = statuses[i];
            if (entry.id != definition.id) continue;
            entry.endsAt = end;
            entry.source = sourceClientId;
            int oldStacks = entry.stacks;
            entry.stacks = Mathf.Min(definition.maxStacks, entry.stacks + 1);
            statuses[i] = entry;
            if (entry.stacks != oldStacks)
                ApplyStatusModifiers(definition, entry.stacks);
            return true;
        }
        statuses.Add(new StatusEntry(definition.id, end, sourceClientId, 1));
        ApplyStatusModifiers(definition, 1);
        return true;
    }

    private void ApplyStatusModifiers(StatusDefinition definition, int stacks)
    {
        if (itemStats == null || definition.modifiers == null) return;
        StatModifier[] scaled = new StatModifier[definition.modifiers.Length];
        for (int i = 0; i < scaled.Length; i++)
        {
            scaled[i] = definition.modifiers[i];
            scaled[i].value *= stacks;
        }
        itemStats.AddSource(definition, scaled);
    }

    private void RemoveStatusAt(int index)
    {
        StatusDefinition definition = FindStatus(statuses[index].id);
        if (definition != null) itemStats?.RemoveSource(definition);
        statuses.RemoveAt(index);
    }

    public void RemoveStatus(StatusDefinition definition)
    {
        if (!IsServer || definition == null) return;
        for (int i = statuses.Count - 1; i >= 0; i--)
            if (statuses[i].id == definition.id) RemoveStatusAt(i);
    }

    public struct StatusEntry : INetworkSerializable, System.IEquatable<StatusEntry>
    {
        public int id;
        public double endsAt;
        public ulong source;
        public int stacks;
        public StatusEntry(int id, double endsAt, ulong source, int stacks)
        { this.id = id; this.endsAt = endsAt; this.source = source; this.stacks = stacks; }
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        { serializer.SerializeValue(ref id); serializer.SerializeValue(ref endsAt);
          serializer.SerializeValue(ref source); serializer.SerializeValue(ref stacks); }
        public bool Equals(StatusEntry other) => id == other.id && endsAt == other.endsAt &&
            source == other.source && stacks == other.stacks;
    }

    private void OnBurningChanged(double previous, double current)
    {
        if (current <= 0 || !IsAlive())
        {
            StopBurningVisual();
            return;
        }
        if (burnParticles != null || burnParticlesPrefab == null ||
            (IsServer && !IsClient))
            return;

        burnAnchor = GetComponent<HeroSorting>()?.HeroRenderer;
        Transform anchor = burnAnchor != null ? burnAnchor.transform : transform;
        burnParticles = Instantiate(burnParticlesPrefab, anchor);
        burnParticles.transform.localPosition = Vector3.zero;
        burnParticles.transform.localRotation = Quaternion.identity;
        burnRenderer = burnParticles.GetComponent<ParticleSystemRenderer>();
        burnParticles.Play();
    }

    private void StopBurningVisual()
    {
        if (burnParticles != null)
            Destroy(burnParticles.gameObject);
        burnParticles = null;
        burnRenderer = null;
        burnAnchor = null;
    }

    private void UpdateBurning()
    {
        double now = NetworkManager.ServerTime.Time;
        if (!IsAlive())
        {
            burningUntil.Value = 0;
            ignitedTargets.Clear();
            return;
        }

        if (now >= nextBurnTick && nextBurnTick <= burningUntil.Value)
        {
            nextBurnTick = now + 1d;
            TakeDamage(10, ulong.MaxValue);
            if (!IsAlive()) return;
        }

        if (now >= burningUntil.Value)
        {
            burningUntil.Value = 0;
            ignitedTargets.Clear();
            return;
        }

        if (Time.time < nextSpreadScan)
            return;

        nextSpreadScan = Time.time + 0.1f;
        var filter = new ContactFilter2D();
        filter.SetLayerMask(1 << 7); // PlayerHitbox, not the body collider.
        filter.useTriggers = true;
        foreach (Collider2D hitbox in ownHitboxes)
        {
            if (hitbox == null || !hitbox.enabled) continue;
            int hitCount = hitbox.Overlap(filter, burnHits);
            for (int j = 0; j < hitCount; j++)
            {
                Collider2D hit = burnHits[j];
                if (hit == null || !hit.isTrigger) continue;
                Health other = hit.GetComponentInParent<Health>();
                if (other != null && other != this && other.IsAlive() &&
                    ignitedTargets.Add(other))
                    other.Ignite(refreshExisting: false);
            }
        }
    }

    public void DisableSpawnProtection()
    {
        if (!IsServer)
            return;

        if (!spawnProtection.Value)
            return;

        spawnProtection.Value = false;
    }

    public void ResetRegenDelay()
    {
        if (!IsServer)
            return;

        lastDamageTime = Time.time;
        regenTimer = 0f;
    }

    public void TakeDamage(
        int damage,
        ulong attackerClientId)
    {
        TakeDamage(
            damage,
            attackerClientId,
            DamageSource.Generic
        );
    }

    public void TakeDamage(
        int damage,
        ulong attackerClientId,
        DamageSource source)
    {
        if (!IsServer || !NetworkObject.IsSpawned)
            return;

        if (!IsAlive())
            return;

        if (spawnProtection.Value)
        {
            ShowHealthNumberClientRpc(0);
            return;
        }

        int healthBefore = currentHealth.Value;

        currentHealth.Value =
            Mathf.Max(
                0,
                healthBefore - damage
            );

        int appliedDamage = healthBefore - currentHealth.Value;

        lastDamageTime = Time.time;
        regenTimer = 0f;

        ShowHealthNumberClientRpc(-damage);

        if (appliedDamage > 0 &&
            attackerClientId != ulong.MaxValue &&
            attackerClientId != OwnerClientId &&
            MatchStatsManager.Instance != null)
        {
            MatchStatsManager.Instance.AddDamage(
                attackerClientId,
                appliedDamage
            );
        }

        if (appliedDamage > 0)
        {
            // Send feedback while the object is still spawned. Damaged handlers
            // can despawn a miniboss immediately on a lethal hit.
            ShowDamageFeedbackClientRpc(
                appliedDamage,
                attackerClientId,
                (byte)source
            );

            Damaged?.Invoke(
                new DamageEvent(
                    appliedDamage,
                    attackerClientId,
                    source
                )
            );

        }

        if (!IsAlive())
        {
            if (MatchStatsManager.Instance != null)
            {
                Debug.Log(
                    $"[STATS TEST] Manager found! " +
                    $"Victim={OwnerClientId}, " +
                    $"Attacker={attackerClientId}"
                );

                if (playerNetwork != null)
                {
                    MatchStatsManager.Instance.AddDeath(
                        OwnerClientId
                    );

                    if (attackerClientId != OwnerClientId)
                    {
                        MatchStatsManager.Instance.AddKill(
                            attackerClientId
                        );
                    }

                    FlagCarryVisual flagCarry =
                        GetComponent<FlagCarryVisual>();

                    if (flagCarry != null &&
                        flagCarry.IsCarrying &&
                        attackerClientId != OwnerClientId)
                    {
                        MatchStatsManager.Instance.AddCounter(
                            attackerClientId
                        );
                    }
                }
            }
            else
            {
                Debug.LogError(
                    "[STATS TEST] MatchStatsManager.Instance == NULL!"
                );
            }

            Die();
        }
    }

    [ClientRpc]
    private void ShowDamageFeedbackClientRpc(
        int amount,
        ulong attackerClientId,
        byte source)
    {
        // Damage audio belongs to actual health loss, not to the optional hit
        // visual. This also covers reflected and other non-projectile damage.
        if (AudioManager.Instance != null)
        {
            if (damageClips != null && damageClips.Length > 0)
            {
                AudioManager.Instance.PlayRandomClip(
                    ResolveAudioGroup(), SoundKeys.CustomDamage,
                    damageClips, transform.position);
            }
            else
            {
                AudioManager.Instance.PlayDamageTaken(transform.position);
            }
        }

        DamageFeedback?.Invoke(
            new DamageEvent(
                amount,
                attackerClientId,
                (DamageSource)source
            )
        );

        damageHitFeedback?.Shake(amount);
        if ((DamageSource)source != DamageSource.Projectile &&
            (DamageSource)source != DamageSource.Reflect)
        {
            // Projectile and reflection impacts are sent before TakeDamage so
            // their visuals also survive a lethal hit.
            float radius = Mathf.Lerp(0.15f, 0.5f, Mathf.Clamp01(amount / 100f));
            damageHitEffect?.SetHit(transform.position, radius, amount);
        }

    }

    [ClientRpc]
    private void ShowHealthNumberClientRpc(int amount)
    {
        if (healthFloatingText == null)
            return;

        if (amount < 0)
            healthFloatingText.ShowDamage(-amount);
        else
            healthFloatingText.ShowHeal(amount);
    }

    private void Die()
    {
        if (IsServer)
        {
            for (int i = statuses.Count - 1; i >= 0; i--) RemoveStatusAt(i);
            EndDebuffImmunity();
        }
        Debug.Log("PLAYER DIED");
        Died?.Invoke();
        if (respawnOnDeath && RespawnManager.Instance != null)
            RespawnManager.Instance.RespawnPlayer(this);
    }

    public void Heal(int amount, bool showNumber = false)
    {
        if (!IsServer || !NetworkObject.IsSpawned || amount <= 0)
            return;

        if (MaxHealth <= 0 || currentHealth.Value >= MaxHealth)
            return;

        int healedAmount = Mathf.Min(
            amount,
            MaxHealth - currentHealth.Value
        );

        currentHealth.Value += healedAmount;

        if (showNumber && healedAmount > 0)
            ShowHealthNumberClientRpc(healedAmount);
    }

    public bool IsAlive()
    {
        return currentHealth.Value > 0;
    }
}
