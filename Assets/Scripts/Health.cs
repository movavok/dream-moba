using UnityEngine;
using Unity.Netcode;

public class Health : NetworkBehaviour
{
    private NetworkVariable<int> currentHealth = new NetworkVariable<int>();
    private PlayerNetwork playerNetwork;

    public event System.Action<int, int> HealthChanged;

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

        Debug.Log("After damage HP: " + currentHealth.Value);

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
