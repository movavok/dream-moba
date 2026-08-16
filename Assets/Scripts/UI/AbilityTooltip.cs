using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class AbilityTooltip : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Position")]
    [SerializeField] private float screenPadding = 10f;

    private RectTransform rectTransform;
    private RectTransform canvasRect;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas != null)
            canvasRect = canvas.GetComponent<RectTransform>();

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            graphic.raycastTarget = false;
        }

        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!gameObject.activeSelf)
            return;

        UpdatePosition();
    }

    public void Show(string title, string description)
    {
        titleText.text = title;
        descriptionText.text = description;

        gameObject.SetActive(true);

        Canvas.ForceUpdateCanvases();

        UpdatePosition();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void UpdatePosition()
    {
        if (rectTransform == null || canvasRect == null)
            return;

        if (Mouse.current == null)
            return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            mousePosition,
            null,
            out Vector2 localMousePosition
        );

        Vector2 tooltipSize = rectTransform.rect.size;
        Vector2 canvasSize = canvasRect.rect.size;

        const float cursorOffset = 8f;

        float halfWidth = tooltipSize.x / 2f;
        float halfHeight = tooltipSize.y / 2f;

        Vector2 position = new Vector2(
            localMousePosition.x,
            localMousePosition.y + halfHeight + cursorOffset
        );

        if (position.y + halfHeight > canvasSize.y / 2f - screenPadding)
        {
            position.y =
                localMousePosition.y -
                halfHeight -
                cursorOffset;
        }

        if (position.x - halfWidth < -canvasSize.x / 2f + screenPadding)
        {
            position.x =
                -canvasSize.x / 2f +
                halfWidth +
                screenPadding;
        }

        if (position.x + halfWidth > canvasSize.x / 2f - screenPadding)
        {
            position.x =
                canvasSize.x / 2f -
                halfWidth -
                screenPadding;
        }

        if (position.y - halfHeight < -canvasSize.y / 2f + screenPadding)
        {
            position.y =
                -canvasSize.y / 2f +
                halfHeight +
                screenPadding;
        }

        rectTransform.localPosition = position;
    }
}