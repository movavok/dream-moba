using UnityEngine;
using UnityEngine.UI;

// A local screen overlay: independent of URP volumes, Pixel Perfect and lens
// changes. The normalized radial mask stretches to an ellipse on wide screens.
[DisallowMultipleComponent]
public class DamageScreenVignette : MonoBehaviour
{
    private RawImage image;
    private Texture2D mask;
    private GameObject overlay;

    private void Awake()
    {
        const int size = 128;
        mask = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Damage Vignette Mask",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 position = new Vector2(
                    (x + 0.5f) / size * 2f - 1f,
                    (y + 0.5f) / size * 2f - 1f);
                float alpha = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(0.6f, 1.15f, position.magnitude));
                pixels[y * size + x] = new Color32(255, 255, 255,
                    (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        mask.SetPixels32(pixels);
        mask.Apply(false, true);

        overlay = new GameObject("Damage Screen Vignette", typeof(RectTransform), typeof(Canvas));
        // Screen Space Overlay canvases stay at the hierarchy root. The owner
        // keeps this reference and destroys the canvas when the camera goes away.
        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100; // Under the HUD and the doorway/death fades.

        GameObject graphic = new GameObject("Red Edge Gradient", typeof(RectTransform), typeof(RawImage));
        graphic.transform.SetParent(overlay.transform, false);
        image = graphic.GetComponent<RawImage>();
        image.texture = mask;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        image.enabled = false;
    }

    public void SetIntensity(float intensity, Color color)
    {
        if (image == null)
            return;
        image.enabled = intensity > 0f;
        image.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(intensity));
    }

    private void OnEnable()
    {
        if (overlay != null)
            overlay.SetActive(true);
    }

    private void OnDisable()
    {
        if (overlay != null)
            overlay.SetActive(false);
    }

    private void OnDestroy()
    {
        if (overlay != null)
            Destroy(overlay);
        if (mask != null)
            Destroy(mask);
    }
}
