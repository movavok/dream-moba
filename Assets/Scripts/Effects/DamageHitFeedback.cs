using System.Collections;
using UnityEngine;

public class DamageHitFeedback : MonoBehaviour
{
    [SerializeField] private Transform target;

    [SerializeField] private float maxShakeDistance = 0.08f;
    [SerializeField] private float shakeDuration = 0.12f;

    private Vector3 startPosition;
    private Coroutine shakeCoroutine;

    private void Awake()
    {
        if (target == null)
            target = transform;

        startPosition = target.localPosition;
    }

    public void Shake(float damage)
    {
        float strength =
            Mathf.Clamp01(damage / 100f);

        float distance =
            maxShakeDistance * strength;

        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);

        shakeCoroutine =
            StartCoroutine(
                ShakeCoroutine(distance)
            );
    }

    private IEnumerator ShakeCoroutine(
        float distance)
    {
        float timer = 0f;

        while (timer < shakeDuration)
        {
            timer += Time.deltaTime;

            float progress =
                timer / shakeDuration;

            float currentStrength =
                1f - progress;

            Vector2 offset =
                Random.insideUnitCircle *
                distance *
                currentStrength;

            target.localPosition =
                startPosition +
                (Vector3)offset;

            yield return null;
        }

        target.localPosition =
            startPosition;

        shakeCoroutine = null;
    }
}