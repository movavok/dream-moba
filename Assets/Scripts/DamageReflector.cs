using Unity.Netcode;
using UnityEngine;

public class DamageReflector : MonoBehaviour
{
    [SerializeField, Range(0f, 2f)] private float reflectFraction = 1f;
    [SerializeField] private Health health;

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
        if (health == null || !health.IsServer)
            return;

        if (damageEvent.Source == DamageSource.Reflect)
            return;

        if (damageEvent.Amount <= 0 || reflectFraction <= 0f)
            return;

        Health attackerHealth =
            FindPlayerHealth(damageEvent.AttackerClientId);

        if (attackerHealth == null || attackerHealth == health)
            return;

        int reflected = Mathf.Max(
            1,
            Mathf.RoundToInt(
                damageEvent.Amount * reflectFraction
            )
        );

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
