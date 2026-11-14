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

    [SerializeField] private HealthFloatingText healthFloatingText;
    [SerializeField] private int standaloneMaxHealth;
    [SerializeField] private bool respawnOnDeath = true;
    [SerializeField] private bool standardRegenEnabled = true;

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
        damageHitEffect = GetComponentInChildren<DamageHitEffect>();
        damageHitFeedback = GetComponentInChildren<DamageHitFeedback>();

        if (healthFloatingText == null)
            healthFloatingText = GetComponentInChildren<HealthFloatingText>();
    }

    private void Update()
    {
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

    [ClientRpc]
    private void ShowDamageHitClientRpc(
        Vector2 hitPosition,
        float damage)
    {
        if (damageHitEffect == null)
            return;

        float radius = Mathf.Lerp(
            0.15f,
            0.5f,
            Mathf.Clamp01(damage / 100f)
        );

        damageHitEffect.SetHit(
            hitPosition,
            radius,
            damage
        );

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayDamageTaken(
                hitPosition
            );
        }

        if (damageHitFeedback != null)
        {
            damageHitFeedback.Shake(damage);
        }

        if (playerNetwork != null &&
            IsOwner &&
            PlayerCamera.Instance != null)
        {
            PlayerCamera.Instance.Shake(damage);
        }
    }

    public int CurrentHealth => currentHealth.Value;
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

    public override void OnNetworkSpawn()
    {
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

        int maximum = Mathf.Max(1, Mathf.RoundToInt(itemStats.Get(StatType.MaxHealth)));
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
        if (itemStats != null)
            itemStats.StatsChanged -= UpdateInventoryMaxHealth;
        inventoryMaxHealth.OnValueChanged -= OnInventoryMaxHealthChanged;
        currentHealth.OnValueChanged -= OnHealthChanged;
        spawnProtection.OnValueChanged -= OnSpawnProtectionChanged;

        base.OnNetworkDespawn();
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
            Damaged?.Invoke(
                new DamageEvent(
                    appliedDamage,
                    attackerClientId,
                    source
                )
            );

            ShowDamageFeedbackClientRpc(
                appliedDamage,
                attackerClientId,
                (byte)source
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
        DamageFeedback?.Invoke(
            new DamageEvent(
                amount,
                attackerClientId,
                (DamageSource)source
            )
        );
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
