using UnityEngine;

public class ProjectileSorting : MonoBehaviour
{
    [SerializeField] private SpriteRenderer projectileRenderer;
    [SerializeField] private SpriteRenderer behindOutline;

    private static readonly int TeamColorID =
        Shader.PropertyToID("_TeamColor");
    private static readonly int PatternDirectionID =
        Shader.PropertyToID("_PatternDirection");
    private static readonly int PatternEnabledID =
        Shader.PropertyToID("_PatternEnabled");

    private const string PROJECTILE_LAYER = "Projectile";
    private const string PROJECTILE_BUILDING_LAYER = "ProjectileBuilding";

    private void Awake()
    {
        if (projectileRenderer == null)
            projectileRenderer = GetComponentInChildren<SpriteRenderer>();
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
        if (projectileRenderer != null)
        {
            projectileRenderer.sortingLayerName =
                behind ? PROJECTILE_BUILDING_LAYER : PROJECTILE_LAYER;
        }

        if (behindOutline != null)
        {
            behindOutline.enabled = behind;
        }
    }

    private void LateUpdate()
    {
        if (projectileRenderer == null || behindOutline == null)
            return;

        behindOutline.sprite = projectileRenderer.sprite;
    }
}