using UnityEngine;

public class HeroCameraShake : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private DamageSource[] sources;
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;

    private void Awake()
    {
        if (health == null)
            health = GetComponentInParent<Health>();
    }

    private void OnEnable()
    {
        if (health != null)
            health.DamageFeedback += OnDamageFeedback;
    }

    private void OnDisable()
    {
        if (health != null)
            health.DamageFeedback -= OnDamageFeedback;
    }

    private void OnDamageFeedback(DamageEvent damageEvent)
    {
        if (health == null || !health.IsOwner)
            return;

        if (!ShouldShake(damageEvent.Source))
            return;

        if (PlayerCamera.Instance == null)
            return;

        PlayerCamera.Instance.Shake(
            damageEvent.Amount * damageMultiplier
        );
    }

    private bool ShouldShake(DamageSource source)
    {
        if (sources == null)
            return false;

        foreach (DamageSource candidate in sources)
        {
            if (candidate == source)
                return true;
        }

        return false;
    }
}
