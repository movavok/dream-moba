using UnityEngine;

public class DamageHitParticles : MonoBehaviour
{
    [SerializeField] private ParticleSystem particles;

    [Header("Amount")]
    [SerializeField] private int minParticles = 3;
    [SerializeField] private int maxParticles = 12;

    [Header("Force")]
    [SerializeField] private float minSpeed = 1.5f;
    [SerializeField] private float maxSpeed = 3.5f;

    [SerializeField] private float randomAngle = 35f;

    [Header("Damage")]
    [SerializeField] private float damageForMaxEffect = 100f;

    private void Awake()
    {
        if (particles == null)
            particles = GetComponent<ParticleSystem>();
    }

    public void Play(
    Vector2 hitPosition,
    Vector2 playerPosition,
    float damage)
    {
        if (particles == null)
            return;

        transform.position = hitPosition;

        float power =
            Mathf.Clamp01(damage / damageForMaxEffect);

        int amount =
            Mathf.RoundToInt(
                Mathf.Lerp(minParticles, maxParticles, power)
            );

        float speed =
            Mathf.Lerp(minSpeed, maxSpeed, power);

        Vector2 direction =
            hitPosition - playerPosition;

        if (direction.sqrMagnitude > 0.001f)
        {
            float angle =
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            transform.rotation =
                Quaternion.Euler(0f, 0f, angle);
        }

        var main = particles.main;
        main.startSpeed = speed;

        var emission = particles.emission;

        emission.SetBurst(
            0,
            new ParticleSystem.Burst(
                0f,
                (short)amount
            )
        );

        particles.Play();
    }
}