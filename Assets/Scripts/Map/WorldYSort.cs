using UnityEngine;
using UnityEngine.Rendering;

// Ordinary world visuals share Hero. Existing building controllers retain
// ownership of their special layers. NetworkTransform supplies position.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class WorldYSort : MonoBehaviour
{
    public const string WorldLayer = "Hero";
    public const string BehindLayer = "HeroBehind";

    [Tooltip("Optional stable ground contact, e.g. LegsPoint or a tree's trunk base.")]
    [SerializeField] private Transform sortPoint;
    [Tooltip("Used to find the bottom once. Defaults to the first child SpriteRenderer.")]
    [SerializeField] private SpriteRenderer anchorRenderer;
    [Tooltip("Empty = child SpriteRenderers. Assign only the actual visual, not shadows/outlines.")]
    [SerializeField] private SpriteRenderer[] visualRenderers;
    [Tooltip("Optional group for a multi-part prop. Do not include building silhouettes in it.")]
    [SerializeField] private SortingGroup sortingGroup;
    [SerializeField] private bool setWorldLayer = true;
    [SerializeField] private float groundOffset;

    private int[] relativeOrders;
    private float bottomOffset;
    private bool hasBottom;
    public float VisualHeight { get; set; }

    public static WorldYSort Configure(GameObject root, Transform point,
        SpriteRenderer anchor, params SpriteRenderer[] visuals)
    {
        WorldYSort sorter = root.GetComponent<WorldYSort>();
        if (sorter == null)
            sorter = root.AddComponent<WorldYSort>();

        sorter.sortPoint = point;
        sorter.anchorRenderer = anchor;
        sorter.visualRenderers = visuals;
        sorter.setWorldLayer = false; // HeroSorting / ItemSorting own building layers.
        sorter.relativeOrders = null;
        sorter.hasBottom = false;
        sorter.Initialize();
        sorter.ApplySorting();
        return sorter;
    }

    private void Start()
    {
        Initialize();
        ApplySorting();
    }

    private void Initialize()
    {
        if (relativeOrders != null)
            return;

        if (visualRenderers == null || visualRenderers.Length == 0)
            visualRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (anchorRenderer == null && visualRenderers.Length > 0)
            anchorRenderer = visualRenderers[0];

        relativeOrders = new int[visualRenderers.Length];
        int initialOrder = anchorRenderer != null ? anchorRenderer.sortingOrder : 0;
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            if (visualRenderers[i] != null)
                relativeOrders[i] = visualRenderers[i].sortingOrder - initialOrder;
        }
        CaptureBottom();
    }

    private void CaptureBottom()
    {
        if (hasBottom || anchorRenderer == null || anchorRenderer.sprite == null)
            return;

        // Cache relative to the root, not the animated visual. Root rotation
        // during a throw must not rotate this ground offset around its pivot.
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        bottomOffset = (anchorRenderer.bounds.min.y - transform.position.y) /
            Mathf.Max(scaleY, 0.0001f);
        hasBottom = true;
    }

    private void LateUpdate()
    {
        Initialize();
        ApplySorting();
    }

    private void ApplySorting()
    {
        CaptureBottom(); // Also handles a sprite assigned after spawning.
        float y = sortPoint != null ? sortPoint.position.y :
            transform.position.y + bottomOffset * Mathf.Abs(transform.lossyScale.y);
        int order = Mathf.Clamp(-Mathf.RoundToInt((y + groundOffset - VisualHeight) * 100f), -32000, 32000);

        if (sortingGroup != null)
        {
            if (setWorldLayer)
                sortingGroup.sortingLayerName = WorldLayer;
            sortingGroup.sortingOrder = order;
            return;
        }

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            SpriteRenderer visual = visualRenderers[i];
            if (visual == null)
                continue;
            if (setWorldLayer)
                visual.sortingLayerName = WorldLayer;
            visual.sortingOrder = Mathf.Clamp(order + relativeOrders[i], -32768, 32767);
        }
    }
}
