using TMPro;
using UnityEngine;

public class HealthNumber : MonoBehaviour
{
    [SerializeField] private TMP_Text text;

    public RectTransform RectTransform { get; private set; }

    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();

        if (text == null)
            text = GetComponentInChildren<TMP_Text>();
    }

    public void Setup(
        int amount,
        Color color,
        string source = null)
    {
        string sign =
            amount > 0
                ? "+"
                : "";

        text.text =
            string.IsNullOrEmpty(source)
                ? $"{sign}{amount}"
                : $"{sign}{amount} ({source})";

        text.color = color;
    }

    public void SetAlpha(float alpha)
    {
        Color color = text.color;
        color.a = alpha;
        text.color = color;
    }

    public void SetScale(float scale)
    {
        transform.localScale =
            Vector3.one * scale;
    }
}