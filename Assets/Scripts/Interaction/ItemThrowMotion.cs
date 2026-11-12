using System.Collections;
using UnityEngine;

public class ItemThrowMotion : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private float spawnForwardOffset = 0.1f;
    [SerializeField] private float spawnHeightOffset = -0.1f;

    [Header("Throw")]
    [SerializeField] private float throwDistance = 0.85f;
    [SerializeField] private float throwDuration = 0.35f;
    [SerializeField] private float throwArcHeight = 0.25f;

    [Header("Bounce")]
    [SerializeField] private float bounceDuration = 0.16f;
    [SerializeField] private float bounceHeight = 0.08f;

    public IEnumerator Play(
        Vector3 origin,
        Vector2 direction,
        float distanceMultiplier)
    {
        direction = direction.sqrMagnitude > 0.001f
            ? direction.normalized
            : Vector2.down;

        Vector3 start =
            origin +
            (Vector3)(direction * spawnForwardOffset) +
            Vector3.up * spawnHeightOffset;

        transform.position = start;
        transform.rotation = Quaternion.identity;

        Vector3 end =
            start + (Vector3)(direction * throwDistance * distanceMultiplier);

        float elapsed = 0f;
        float rotationSpeed = Random.Range(-540f, 540f);

        while (elapsed < throwDuration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsed / throwDuration);

            float arc =
                Mathf.Sin(progress * Mathf.PI) * throwArcHeight;

            transform.position =
                Vector3.Lerp(start, end, progress) +
                Vector3.up * arc;

            transform.Rotate(
                0f,
                0f,
                rotationSpeed * Time.deltaTime
            );

            yield return null;
        }

        transform.position = end;

        yield return Bounce(end);
    }

    private IEnumerator Bounce(Vector3 position)
    {
        float elapsed = 0f;
        Quaternion startRotation = transform.rotation;

        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsed / bounceDuration);

            float bounce =
                Mathf.Sin(progress * Mathf.PI) * bounceHeight;

            transform.position =
                position + Vector3.up * bounce;

            transform.rotation = Quaternion.Lerp(
                startRotation,
                Quaternion.identity,
                progress
            );

            yield return null;
        }

        transform.position = position;
        transform.rotation = Quaternion.identity;
    }
}
