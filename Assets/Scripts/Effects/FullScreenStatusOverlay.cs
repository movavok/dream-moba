using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// One non-interactive overlay per local status. Restarting ShowUntil refreshes
// its deadline even during fade-out, without creating another Canvas or Image.
public sealed class FullScreenStatusOverlay : MonoBehaviour
{
    private CanvasGroup group;
    private Coroutine routine;
    private float opacity;
    private float fadeIn;
    private float fadeOut;
    private double endTime;
    private bool hiding;

    public static FullScreenStatusOverlay Create(string effectName, Sprite sprite,
        float maxOpacity, float inDuration, float outDuration)
    {
        GameObject root = new($"Status Screen {effectName}", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasGroup), typeof(FullScreenStatusOverlay));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        GameObject child = new("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = (RectTransform)child.transform;
        rect.SetParent(root.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = child.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        FullScreenStatusOverlay overlay = root.GetComponent<FullScreenStatusOverlay>();
        overlay.group = group;
        overlay.opacity = Mathf.Clamp01(maxOpacity);
        overlay.fadeIn = Mathf.Max(0.01f, inDuration);
        overlay.fadeOut = Mathf.Max(0.01f, outDuration);
        return overlay;
    }

    public void ShowUntil(double deadline)
    {
        if (routine != null) StopCoroutine(routine);
        hiding = false;
        endTime = deadline;
        routine = StartCoroutine(ShowRoutine());
    }

    public void Hide()
    {
        if (hiding) return;
        hiding = true;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FadeAway());
    }

    private IEnumerator ShowRoutine()
    {
        while (NetworkManager.Singleton != null)
        {
            double remaining = endTime - NetworkManager.Singleton.ServerTime.Time;
            if (remaining <= 0d) break;
            float target = opacity * Mathf.Clamp01((float)(remaining / fadeOut));
            group.alpha = Mathf.MoveTowards(group.alpha, target,
                Time.deltaTime * opacity / fadeIn);
            yield return null;
        }
        group.alpha = 0f;
        Destroy(gameObject);
    }

    private IEnumerator FadeAway()
    {
        while (group.alpha > 0f)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 0f,
                Time.deltaTime * opacity / fadeOut);
            yield return null;
        }
        Destroy(gameObject);
    }
}
