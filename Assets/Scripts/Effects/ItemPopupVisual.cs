using System.Collections;
using UnityEngine;

// One reused cosmetic sprite; configure motion and timing on its prefab.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ItemPopupVisual : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 0.85f;
    [SerializeField] private float initialScale = 0.3f;
    [SerializeField] private float peakScale = 1.6f;
    [SerializeField] private float swayDegrees = 25f;
    [SerializeField, Min(1)] private int swayCycles = 3;
    [SerializeField, Min(0f)] private float sidewaysDistance = 0.12f;
    [SerializeField, Min(0f)] private float riseDistance = 0.4f;
    [SerializeField] private Vector3 offset = new(0f, 1.2f, 0f);
    private SpriteRenderer image;
    private Coroutine animation;
    private void Awake()
    {
        image = GetComponent<SpriteRenderer>();
        image.enabled = false;
    }
    public void Play(Sprite sprite)
    {
        if (sprite == null) return;
        if (animation != null) StopCoroutine(animation);
        image.sprite = sprite;
        image.color = Color.white;
        image.enabled = true;
        animation = StartCoroutine(Animate());
    }
    private IEnumerator Animate()
    {
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / duration);
            float sway = Mathf.Sin(p * Mathf.PI * 2f * swayCycles);
            float strength = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((p - 0.7f) / 0.3f));
            transform.localPosition = offset + Vector3.up * (p * riseDistance) +
                Vector3.right * (sway * sidewaysDistance * strength);
            transform.localScale = Vector3.one * Mathf.Lerp(initialScale, peakScale,
                Mathf.Clamp01(p * 6f));
            transform.localRotation = Quaternion.Euler(0, 0, swayDegrees *
                sway * strength);
            Color color = image.color;
            color.a = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((p - 0.8f) / 0.2f));
            image.color = color;
            yield return null;
        }
        image.enabled = false;
        transform.localRotation = Quaternion.identity;
        animation = null;
    }
}
