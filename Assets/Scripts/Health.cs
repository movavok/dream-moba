using UnityEngine;
using Unity.Netcode;

public class Health : NetworkBehaviour
{
    private NetworkVariable<int> currentHealth = new NetworkVariable<int>();

    private const float REGEN_DELAY = 3f;
    private const float REGEN_INTERVAL = 1f;
    private const int REGEN_AMOUNT = 10;

    private float lastDamageTime;
    private float regenTimer;
    private PlayerNetwork playerNetwork;

    public event System.Action<int, int> HealthChanged;

    private DamageHitEffect damageHitEffect;
    private DamageHitFeedback damageHitFeedback;
    
    private void Awake()
    {
        damageHitEffect = GetComponentInChildren<DamageHitEffect>();
        damageHitFeedback = GetComponentInChildren<DamageHitFeedback>();
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
        }
    }

    public void ShowDamageHit(
    Vector2 hitPosition,
    float damage)
    {
        if (!IsServer)
            return;

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

        if (IsServer && playerNetwork != null && playerNetwork.HeroData != null)
        {
            currentHealth.Value = playerNetwork.HeroData.stats.maxHealth;
        }

        currentHealth.OnValueChanged += OnHealthChanged;

        Debug.Log("Spawned with HP: " + currentHealth.Value);

        HealthChanged?.Invoke(
            currentHealth.Value,
            MaxHealth
        );
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

        base.OnNetworkDespawn();
    }

    public void TakeDamage(int damage)
    {
        if (!IsServer || !NetworkObject.IsSpawned)
            return;

        if (!IsAlive())
            return;

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);

        lastDamageTime = Time.time;
        regenTimer = 0f;

        Debug.Log("[Health][" + Time.time + "]  After damage HP: " + currentHealth.Value);

        if (!IsAlive())
            Die();
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
