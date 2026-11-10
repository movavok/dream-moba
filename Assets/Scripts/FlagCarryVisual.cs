using UnityEngine;

public class FlagCarryVisual : MonoBehaviour
{
    [SerializeField] private Vector3 localPosition = new(0.35f, 0.8f, 0f);
    [SerializeField] private float scale = 0.75f;
    [SerializeField] private GameObject visualObject;
    [SerializeField] private SpriteRenderer branchRenderer;
    [SerializeField] private SpriteRenderer clothRenderer;
    [SerializeField] private FlagBlinkSettings blinkSettings = new();

    private bool isCarrying;
    private float blinkPhase;

    public bool IsCarrying => isCarrying;
    public FlagBlinkSettings BlinkSettings => blinkSettings;

    private void Awake()
    {
        ResolveRenderers();
        visualObject?.SetActive(false);
    }

    public void Show(
        Sprite branch,
        Sprite cloth,
        Color clothColor)
    {
        EnsureVisual();

        ResolveRenderers();
        branchRenderer.sprite = branch;
        branchRenderer.color = Color.white;
        clothRenderer.sprite = cloth;
        clothRenderer.color = new Color(
            clothColor.r,
            clothColor.g,
            clothColor.b,
            1f
        );
        isCarrying = true;
        blinkPhase = 0f;
        visualObject.SetActive(true);
    }

    public void Hide()
    {
        if (visualObject != null)
            visualObject.SetActive(false);

        isCarrying = false;
    }

    public void SetClothColor(Color clothColor)
    {
        if (clothRenderer == null)
            return;

        Color color = clothRenderer.color;
        color.r = clothColor.r;
        color.g = clothColor.g;
        color.b = clothColor.b;
        clothRenderer.color = color;
    }

    private void Update()
    {
        if (!isCarrying || visualObject == null)
            return;

        FlagDeliveryZone zone =
            FindAnyObjectByType<FlagDeliveryZone>();

        FlagBlinkSettings activeSettings =
            zone != null ? zone.BlinkSettings : blinkSettings;

        float distance = zone != null
            ? Vector2.Distance(transform.position, zone.transform.position)
            : 0f;

        float currentBlinkSpeed = zone != null
            ? activeSettings.CalculateBlinkSpeed(distance)
            : activeSettings.BlinkSpeed;

        blinkPhase = activeSettings.UpdatePhase(
            blinkPhase,
            currentBlinkSpeed,
            Time.deltaTime
        );
        float alpha = activeSettings.CalculateAlpha(blinkPhase);

        SetAlpha(branchRenderer, alpha);
        SetAlpha(clothRenderer, alpha);
    }

    private void SetAlpha(SpriteRenderer renderer, float alpha)
    {
        if (renderer == null)
            return;

        Color color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }

    private void EnsureVisual()
    {
        if (visualObject != null &&
            branchRenderer != null &&
            clothRenderer != null)
            return;

        if (visualObject == null)
        {
            visualObject = new GameObject("CarriedFlag");
            visualObject.transform.SetParent(transform, false);
            visualObject.transform.localPosition = localPosition;
            visualObject.transform.localScale = Vector3.one * scale;
        }

        branchRenderer ??= CreateRenderer("Branch", 0);
        clothRenderer ??= CreateRenderer("Cloth", 1);
    }

    private void ResolveRenderers()
    {
        if (visualObject == null)
            return;

        Transform branch =
            visualObject.transform.Find("Branch");
        Transform cloth =
            visualObject.transform.Find("Cloth");

        if (branch != null)
            branchRenderer = branch.GetComponent<SpriteRenderer>();

        if (cloth != null)
            clothRenderer = cloth.GetComponent<SpriteRenderer>();
    }

    private SpriteRenderer CreateRenderer(
        string objectName,
        int sortingOrder)
    {
        GameObject rendererObject = new(objectName);
        rendererObject.transform.SetParent(
            visualObject.transform,
            false
        );

        SpriteRenderer renderer =
            rendererObject.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }
}
