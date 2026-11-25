using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public sealed class UIReveal : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float openDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float closeDuration = 0.12f;
    [SerializeField] private float startScale = 0.9f;
    [SerializeField] private float overshootScale = 1.03f;
    private CanvasGroup group;
    private Coroutine routine;
    private Vector3 baseScale;
    private bool shownOnce;
    private void Awake() { group = GetComponent<CanvasGroup>(); baseScale = transform.localScale; }
    public void Show()
    {
        if (routine != null) StopCoroutine(routine);
        if (!shownOnce)
        {
            group.alpha = 0f;
            transform.localScale = baseScale * startScale;
            shownOnce = true;
        }
        group.interactable = true;
        group.blocksRaycasts = true;
        routine = StartCoroutine(Animate(true));
    }
    public void Hide()
    {
        if (routine != null) StopCoroutine(routine);
        group.interactable = false;
        group.blocksRaycasts = false;
        routine = StartCoroutine(Animate(false));
    }
    private IEnumerator Animate(bool opening)
    {
        float duration = opening ? openDuration : closeDuration;
        float fromAlpha = group.alpha;
        float fromScale = Mathf.Abs(baseScale.x) > 0.0001f
            ? transform.localScale.x / baseScale.x : 1f;
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / duration);
            float smooth = p * p * (3f - 2f * p);
            group.alpha = Mathf.Lerp(fromAlpha, opening ? 1f : 0f, smooth);
            float target = opening ? (p < 0.7f
                ? Mathf.Lerp(startScale, overshootScale, p / 0.7f)
                : Mathf.Lerp(overshootScale, 1f, (p - 0.7f) / 0.3f)) : startScale;
            transform.localScale = baseScale * Mathf.Lerp(fromScale, target, smooth);
            yield return null;
        }
        group.alpha = opening ? 1f : 0f;
        transform.localScale = baseScale * (opening ? 1f : startScale);
        routine = null;
        if (!opening) Destroy(gameObject);
    }
}
