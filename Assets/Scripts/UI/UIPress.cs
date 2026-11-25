using System.Collections;
using UnityEngine;

public sealed class UIPress : MonoBehaviour
{
    [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.92f;
    [SerializeField, Min(0.01f)] private float duration = 0.12f;
    private Coroutine routine;
    private Vector3 original;
    private void Awake() => original = transform.localScale;
    public void Play()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Animate());
    }
    private IEnumerator Animate()
    {
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / duration);
            transform.localScale = original * Mathf.Lerp(pressedScale, 1f, p);
            yield return null;
        }
        transform.localScale = original;
        routine = null;
    }
    private void OnDisable() => transform.localScale = original;
}
