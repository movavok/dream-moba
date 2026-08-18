using UnityEngine;

public class DamageHitEffect : MonoBehaviour
{
    [SerializeField] private GameObject effectPrefab;
    [SerializeField] private DamageHitParticles particlesPrefab;

    public void SetHit(
        Vector2 worldPosition,
        float radius,
        float damage)
    {
        if (effectPrefab == null)
        {
            Debug.LogError(
                "DamageHitEffect: Effect Prefab is not assigned!"
            );

            return;
        }

        GameObject effect =
            Instantiate(
                effectPrefab,
                transform
            );

        effect.transform.position = worldPosition;

        DamageHitInstance instance =
            effect.GetComponent<DamageHitInstance>();

        if (instance == null)
        {
            Debug.LogError(
                "DamageHitEffect: Effect prefab has no DamageHitInstance!"
            );

            Destroy(effect);
            return;
        }

        instance.Initialize(radius);

        if (particlesPrefab != null)
        {
            DamageHitParticles particles =
                Instantiate(
                    particlesPrefab,
                    worldPosition,
                    Quaternion.identity
                );

            particles.Play(
                worldPosition,
                transform.position,
                damage
            );
        }
    }

}
