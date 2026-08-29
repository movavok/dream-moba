using System.Collections.Generic;
using UnityEngine;

public class HealthFloatingText : MonoBehaviour
{
    [SerializeField] private HealthNumber numberPrefab;

    [Header("Colors")]
    [SerializeField] private Color damageColor = Color.red;
    [SerializeField] private Color healColor = Color.green;

    [Header("Layout")]
    [SerializeField] private float spacing = 24f;
    [SerializeField] private int maxNumbers = 4;

    [Header("Animation")]
    [SerializeField] private float appearDuration = 0.25f;
    [SerializeField] private float moveDuration = 0.2f;
    [SerializeField] private float appearOffset = 15f;

    private readonly List<HealthNumber> numbers = new();

    public void ShowDamage(int damage)
    {
        CreateNumber(
            -damage,
            damageColor
        );
    }

    public void ShowHeal(int heal)
    {
        CreateNumber(
            heal,
            healColor
        );
    }

    private void CreateNumber(
        int amount,
        Color color)
    {
        // Сначала удаляем из списка уже уничтоженные объекты
        CleanupDestroyedNumbers();

        // Сдвигаем существующие цифры вверх
        for (int i = 0; i < numbers.Count; i++)
        {
            HealthNumber number = numbers[i];

            if (number == null)
                continue;

            Vector2 targetPosition =
                Vector2.up *
                (spacing * (i + 1));

            StartCoroutine(
                MoveNumber(
                    number,
                    targetPosition,
                    1f - (i + 1) * 0.12f
                )
            );
        }

        // Создаём новую цифру
        HealthNumber newNumber =
            Instantiate(
                numberPrefab,
                transform
            );

        newNumber.Setup(
            amount,
            color
        );

        RectTransform rect =
            newNumber.RectTransform;

        rect.anchoredPosition =
            Vector2.down * appearOffset;

        newNumber.SetAlpha(0f);
        newNumber.SetScale(1f);

        numbers.Insert(0, newNumber);

        StartCoroutine(
            AppearNumber(newNumber)
        );

        StartCoroutine(
            FadeNumber(newNumber, 1.5f)
        );

        // Ограничиваем количество
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
            Vector2.down * appearOffset;

        Vector2 endPosition =
            Vector2.zero;

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