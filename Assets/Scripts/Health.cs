using UnityEngine;
using Unity.Netcode;

public class Health : NetworkBehaviour
{
    private NetworkVariable<int> currentHealth = new NetworkVariable<int>();

    [SerializeField] private HealthFloatingText healthFloatingText;

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
        if (!IsServer)
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
    }

    public int CurrentHealth => currentHealth.Value;
    public int MaxHealth =>
        playerNetwork != null && playerNetwork.HeroData != null
            ? playerNetwork.HeroData.stats.maxHealth
            : 0;

    public override void OnNetworkSpawn()
    {
        playerNetwork = GetComponent<PlayerNetwork>();

        if (IsServer)
        {
            spawnProtection.Value = true;

            if (playerNetwork != null && playerNetwork.HeroData != null)
            {
                currentHealth.Value =
                    playerNetwork.HeroData.stats.maxHealth;
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

    public override void OnNetworkDespawn()
    {
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

    public void TakeDamage(
        int damage,
        ulong attackerClientId)
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

        currentHealth.Value =
            Mathf.Max(
                0,
                currentHealth.Value - damage
            );

        lastDamageTime = Time.time;
        regenTimer = 0f;

        ShowHealthNumberClientRpc(-damage);

        if (!IsAlive())
        {
            if (MatchStatsManager.Instance != null)
            {
                Debug.Log(
                    $"[STATS TEST] Manager found! " +
                    $"Victim={OwnerClientId}, " +
                    $"Attacker={attackerClientId}"
                );

                MatchStatsManager.Instance.AddDeath(
                    OwnerClientId
                );

                MatchStatsManager.Instance.AddKill(
                    attackerClientId
                );
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
        RespawnManager.Instance.RespawnPlayer(this);
    }

    public bool IsAlive()
    {
        return currentHealth.Value > 0;
    }
}
