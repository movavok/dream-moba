using System.Collections.Generic;
using UnityEngine;

public class ProjectileSorting : MonoBehaviour
{
    [SerializeField] private SpriteRenderer projectileRenderer;
    [SerializeField] private SpriteRenderer behindOutline;
    [SerializeField] private SpriteRenderer[] visualRenderers;

    private static readonly int TeamColorID =
        Shader.PropertyToID("_TeamColor");
    private static readonly int PatternDirectionID =
        Shader.PropertyToID("_PatternDirection");
    private static readonly int PatternEnabledID =
        Shader.PropertyToID("_PatternEnabled");

    private const string PROJECTILE_LAYER = "Projectile";
    private const string PROJECTILE_BEHIND_LAYER = "ProjectileBehind";
    private const string PROJECTILE_BUILDING_LAYER = "ProjectileBuilding";
    private const string BEHIND_OUTLINE_LAYER = "Effect";
    private const int BEHIND_OUTLINE_SORTING_ORDER = 5;

    private bool behindBuilding;
    private bool behindPlayer;

    private void Awake()
    {
        if (projectileRenderer == null)
            projectileRenderer = GetComponentInChildren<SpriteRenderer>();

        CollectVisualRenderers();
        ApplyLayers();
    }

    private void OnEnable()
    {
        ApplyLayers();
    }

    public void SetTeamColor(Color color)
    {
        if (behindOutline == null)
            return;

        var mpb = new MaterialPropertyBlock();
        behindOutline.GetPropertyBlock(mpb);
        mpb.SetColor(TeamColorID, color);
        mpb.SetFloat(PatternDirectionID, 1f);
        mpb.SetFloat(PatternEnabledID, 0f);
        behindOutline.SetPropertyBlock(mpb);
    }

    public void SetBehind(bool behind)
    {
        behindBuilding = behind;
        ApplyLayers();
    }

    public void SetBehindPlayer(bool behind)
    {
        behindPlayer = behind;
        ApplyLayers();
    }

    private void ApplyLayers()
    {
        CollectVisualRenderers();

        string baseLayer =
            behindPlayer ? PROJECTILE_BEHIND_LAYER : PROJECTILE_LAYER;

        string layer =
            behindBuilding ? PROJECTILE_BUILDING_LAYER : baseLayer;

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            if (visualRenderers[i] != null)
                visualRenderers[i].sortingLayerName = layer;
        }

        if (behindOutline != null)
        {
            behindOutline.sortingLayerName = BEHIND_OUTLINE_LAYER;
            behindOutline.sortingOrder = BEHIND_OUTLINE_SORTING_ORDER;
            behindOutline.enabled = behindBuilding;
        }
    }

    private void CollectVisualRenderers()
    {
        if (visualRenderers != null && visualRenderers.Length > 0)
            return;

        SpriteRenderer[] found =
            GetComponentsInChildren<SpriteRenderer>(true);

        var list = new List<SpriteRenderer>(found.Length);

        for (int i = 0; i < found.Length; i++)
        {
            SpriteRenderer renderer = found[i];

            if (renderer == null || renderer == behindOutline)
                continue;

            list.Add(renderer);
        }

        visualRenderers = list.ToArray();
    }

    private void LateUpdate()
    {
        if (projectileRenderer == null || behindOutline == null)
            return;

        behindOutline.sprite = projectileRenderer.sprite;
    }
}
