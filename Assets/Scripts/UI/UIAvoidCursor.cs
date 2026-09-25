using UnityEngine;
using UnityEngine.InputSystem;

public class UIAvoidCursor : MonoBehaviour
{
    [SerializeField] private float detectionRadius = 100f;
    [SerializeField] private float dodgeDistance = 30f;
    [SerializeField] private float moveSpeed = 12f;

    private RectTransform rectTransform;
    private Vector2 startPosition;
    private Vector2 targetPosition;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        startPosition = rectTransform.anchoredPosition;
        targetPosition = startPosition;
    }

    private void Update()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        Vector2 objectScreenPosition =
            RectTransformUtility.WorldToScreenPoint(
                null,
                rectTransform.position
            );

        float distance = Vector2.Distance(
            mousePosition,
            objectScreenPosition
        );

        if (distance < detectionRadius)
        {
            Vector2 direction =
                objectScreenPosition - mousePosition;

            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.up;
            else
                direction.Normalize();

            float strength =
                1f - distance / detectionRadius;

            targetPosition =
                startPosition +
                direction * dodgeDistance * strength;
        }
        else
        {
            targetPosition = startPosition;
        }

        rectTransform.anchoredPosition = Vector2.Lerp(
            rectTransform.anchoredPosition,
            targetPosition,
            moveSpeed * Time.deltaTime
        );
    }
}