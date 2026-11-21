using System.Collections.Generic;
using UnityEngine;

public class GoldFloatingText : MonoBehaviour
{
    [SerializeField] private HealthNumber numberPrefab;

    [Header("Colors")]
    [SerializeField] private Color gainColor = new Color(1f, 0.84f, 0f, 1f);
    [SerializeField] private Color spendColor = new Color(1f, 0.45f, 0.45f, 1f);

    [Header("Layout")]
    [SerializeField] private Vector2 baseOffset = Vector2.zero;
    [SerializeField] private float spacing = 24f;
    [SerializeField] private int maxNumbers = 4;

    [Header("Animation")]
    [SerializeField] private float appearDuration = 0.25f;
    [SerializeField] private float moveDuration = 0.2f;
    [SerializeField] private float appearOffset = 15f;

    private readonly List<HealthNumber> numbers = new();

    public void Show(int amount, string source)
    {
        Color color = amount >= 0 ? gainColor : spendColor;
        CreateNumber(amount, color, source);
    }

    private void CreateNumber(
        int amount,
        Color color,
        string source)
    {
        if (numberPrefab == null)
            return;

        CleanupDestroyedNumbers();

        for (int i = 0; i < numbers.Count; i++)
        {
            HealthNumber number = numbers[i];

            if (number == null)
                continue;

            Vector2 targetPosition =
                baseOffset +
                Vector2.up * (spacing * (i + 1));

            StartCoroutine(
                MoveNumber(
                    number,
                    targetPosition,
                    1f - (i + 1) * 0.12f
                )
            );
        }

        HealthNumber newNumber =
            Instantiate(
                numberPrefab,
                transform
            );

        newNumber.Setup(
            amount,
            color,
            source
        );

        RectTransform rect =
            newNumber.RectTransform;

        rect.anchoredPosition =
            baseOffset + Vector2.down * appearOffset;

        newNumber.SetAlpha(0f);
        newNumber.SetScale(1f);

        numbers.Insert(0, newNumber);

        StartCoroutine(
            AppearNumber(newNumber)
        );

        StartCoroutine(
            FadeNumber(newNumber, 1.5f)
        );

        while (numbers.Count > maxNumbers)
        {
            HealthNumber oldNumber =
                numbers[numbers.Count - 1];

            numbers.RemoveAt(numbers.Count - 1);

            if (oldNumber != null)
                Destroy(oldNumber.gameObject);
        }
    }

    private System.Collections.IEnumerator AppearNumber(
        HealthNumber number)
    {
        if (number == null || number.RectTransform == null)
            yield break;

        RectTransform rect =
            number.RectTransform;

        Vector2 startPosition =
            baseOffset + Vector2.down * appearOffset;

        Vector2 endPosition =
            baseOffset;

        float time = 0f;

        while (time < appearDuration)
        {
            if (number == null || rect == null)
                yield break;

            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time / appearDuration
                );

            t = Mathf.SmoothStep(0f, 1f, t);

            rect.anchoredPosition =
                Vector2.Lerp(
                    startPosition,
                    endPosition,
                    t
                );

            number.SetAlpha(t);

            yield return null;
        }

        if (number == null || rect == null)
            yield break;

        rect.anchoredPosition = endPosition;
        number.SetAlpha(1f);
    }

    private System.Collections.IEnumerator MoveNumber(
        HealthNumber number,
        Vector2 targetPosition,
        float targetScale)
    {
        if (number == null || number.RectTransform == null)
            yield break;

        RectTransform rect =
            number.RectTransform;

        Vector2 startPosition =
            rect.anchoredPosition;

        float startScale =
            number.transform.localScale.x;

        float time = 0f;

        while (time < moveDuration)
        {
            if (number == null || rect == null)
                yield break;

            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time / moveDuration
                );

            t = Mathf.SmoothStep(0f, 1f, t);

            rect.anchoredPosition =
                Vector2.Lerp(
                    startPosition,
                    targetPosition,
                    t
                );

            float scale =
                Mathf.Lerp(
                    startScale,
                    targetScale,
                    t
                );

            number.SetScale(scale);

            yield return null;
        }

        if (number == null || rect == null)
            yield break;

        rect.anchoredPosition = targetPosition;
        number.SetScale(targetScale);
    }

    private System.Collections.IEnumerator FadeNumber(
        HealthNumber number,
        float duration)
    {
        yield return new WaitForSeconds(1f);

        if (number == null)
            yield break;

        float time = 0f;

        while (time < duration)
        {
            if (number == null)
                yield break;

            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time / duration
                );

            number.SetAlpha(1f - t);

            yield return null;
        }

        if (number == null)
            yield break;

        numbers.Remove(number);

        Destroy(number.gameObject);
    }

    private void CleanupDestroyedNumbers()
    {
        for (int i = numbers.Count - 1; i >= 0; i--)
        {
            if (numbers[i] == null)
                numbers.RemoveAt(i);
        }
    }
}