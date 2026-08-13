using UnityEngine;
using Unity.Netcode;

public class Health : NetworkBehaviour
{
    private NetworkVariable<int> currentHealth = new NetworkVariable<int>();
    private PlayerNetwork playerNetwork;

    public override void OnNetworkSpawn()
    {
        playerNetwork = GetComponent<PlayerNetwork>();

        if (IsServer && playerNetwork != null && playerNetwork.HeroData != null)
        {
            currentHealth.Value = playerNetwork.HeroData.stats.maxHealth;
        }

        Debug.Log("Spawned with HP: " + currentHealth.Value);
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
