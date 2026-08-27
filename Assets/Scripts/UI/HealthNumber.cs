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
        Color color)
    {
        text.text =
            amount > 0
                ? $"+{amount}"
                : amount.ToString();

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