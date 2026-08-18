using System.Collections;
using UnityEngine;

public class DamageHitInstance : MonoBehaviour
{
    [SerializeField] private SpriteRenderer effectRenderer;

    [SerializeField] private float duration = 0.2f;

    private Color baseColor;

    private void Awake()
    {
        if (effectRenderer == null)
            effectRenderer = GetComponent<SpriteRenderer>();

        baseColor = effectRenderer.color;
    }

    public void Initialize(float radius)
    {
        SetRadius(radius);

        StartCoroutine(FadeAndDestroy());

        Destroy(gameObject, duration + 0.05f);
    }

    private void SetRadius(float radius)
    {
        if (effectRenderer.sprite == null)
            return;

        float spriteWidth =
            effectRenderer.sprite.bounds.size.x;

        if (spriteWidth <= 0f)
            return;

        float diameter = radius * 2f;

        float scale =
            diameter / spriteWidth;

        transform.localScale =
            Vector3.one * scale;
    }

    private IEnumerator FadeAndDestroy()
    {
        float timer = 0f;

        Color color = baseColor;
        color.a = 1f;
        effectRenderer.color = color;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float progress =
                Mathf.Clamp01(timer / duration);

            float alpha =
                1f - Mathf.SmoothStep(0f, 1f, progress);

            color.a = baseColor.a * alpha;
            effectRenderer.color = color;

            yield return null;
        }

        Destroy(gameObject);
    }
}
