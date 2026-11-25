using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// UI-space one-shot particles: unlike a world ParticleSystemRenderer these render
// on Screen Space Overlay canvases. All graphics ignore raycasts.
public sealed class UICoinBurst : MonoBehaviour
{
    [SerializeField] private Sprite sprite;
    [SerializeField] private Color color = new(1f, 0.86f, 0.2f, 1f);
    [SerializeField, Min(1)] private int count = 9;
    [SerializeField, Min(0.01f)] private float duration = 0.42f;
    [SerializeField] private float radius = 90f;
    [SerializeField] private float size = 22f;

    public void Play()
    {
        if (sprite == null) return;
        for (int i = 0; i < count; i++)
            StartCoroutine(Particle(i));
    }
    private IEnumerator Particle(int index)
    {
        GameObject obj = new("Coin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = (RectTransform)obj.transform;
        rect.SetParent(transform, false);
        rect.sizeDelta = Vector2.one * size;
        Image image = obj.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        float angle = (index + Random.Range(-0.25f, 0.25f)) * Mathf.PI * 2f / count;
        Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / duration);
            rect.anchoredPosition = direction * (radius * p);
            image.color = new Color(color.r, color.g, color.b, color.a * (1f - p));
            yield return null;
        }
        Destroy(obj);
    }
}
