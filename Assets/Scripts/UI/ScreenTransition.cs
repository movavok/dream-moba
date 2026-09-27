using System.Collections;
using UnityEngine;

public class ScreenTransition : MonoBehaviour
{
    public static ScreenTransition Instance { get; private set; }

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float duration = 0.15f;

    private void Awake()
    {
        Instance = this;
        canvasGroup.alpha = 0f;
    }

    public IEnumerator FadeIn()
    {
        yield return Fade(0f, 1f);
    }

    public IEnumerator FadeOut()
    {
        yield return Fade(1f, 0f);
    }

    private IEnumerator Fade(float from, float to)
    {
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            canvasGroup.alpha =
                Mathf.Lerp(from, to, time / duration);

            yield return null;
        }

        canvasGroup.alpha = to;
    }
}