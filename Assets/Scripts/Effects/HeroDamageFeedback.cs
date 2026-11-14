using System;
using UnityEngine;

public class HeroDamageFeedback : MonoBehaviour
{
    [Serializable]
    public struct DamageEffect
    {
        public DamageSource source;
        public ParticleSystem effect;
        [Min(0f)] public float destroyDelay;
    }

    [SerializeField] private Health health;
    [SerializeField] private DamageEffect[] effects;

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
        if (effects == null)
            return;

        foreach (DamageEffect entry in effects)
        {
            if (entry.effect == null ||
                entry.source != damageEvent.Source)
            {
                continue;
            }

            Play(entry);
        }
    }

    private void Play(DamageEffect entry)
    {
        ParticleSystem instance = Instantiate(
            entry.effect,
            transform.position,
            Quaternion.identity
        );

        instance.Play();

        float lifetime = entry.destroyDelay > 0f
            ? entry.destroyDelay
            : GetLifetime(instance);

        Destroy(instance.gameObject, lifetime);
    }

    private static float GetLifetime(
        ParticleSystem particles)
    {
        ParticleSystem.MainModule main = particles.main;

        return main.duration +
            main.startLifetime.constantMax;
    }
}
