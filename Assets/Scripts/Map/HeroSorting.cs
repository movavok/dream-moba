using UnityEngine;

public class HeroSorting : MonoBehaviour
{
    [SerializeField] private SpriteRenderer heroRenderer;
    [SerializeField] private SpriteRenderer shadowRenderer;
    [SerializeField] private SpriteRenderer behindOutline;

    private Team team;
    private DeathVisual deathVisual;
    private bool hasAppliedTeamColor;
    private Color appliedTeamColor;

    private static readonly int TeamColorID =
        Shader.PropertyToID("_TeamColor");
    private static readonly int PatternDirectionID =
        Shader.PropertyToID("_PatternDirection");
    private static readonly int PatternEnabledID =
        Shader.PropertyToID("_PatternEnabled");

    private const string HERO_LAYER = "Hero";
    private const string HERO_BEHIND_LAYER = "HeroBehind";

    private const string SHADOW_LAYER = "Shadow";
    private const string SHADOW_BEHIND_LAYER = "ShadowBehind";

    private void Awake()
    {
        if (heroRenderer == null)
            heroRenderer = GetComponent<SpriteRenderer>();

        if (shadowRenderer == null)
            shadowRenderer = GetComponentInChildren<SpriteRenderer>();

        team = GetComponentInParent<Team>();
        deathVisual = GetComponentInParent<DeathVisual>();
    }

    private void OnEnable()
    {
        if (team == null)
            team = GetComponentInParent<Team>();

        if (deathVisual == null)
            deathVisual = GetComponentInParent<DeathVisual>();

        if (team == null && deathVisual == null)
            return;

        if (team != null)
            team.TeamId.OnValueChanged += OnTeamIdChanged;

        ApplyTeamColor();
    }

    private void OnDisable()
    {
        if (team != null)
            team.TeamId.OnValueChanged -= OnTeamIdChanged;
    }

    private void OnTeamIdChanged(short oldValue, short newValue)
    {
        hasAppliedTeamColor = false;
        ApplyTeamColor();
    }

    private void Update()
    {
        ApplyTeamColor();
    }

    private void ApplyTeamColor()
    {
        if (team == null &&
            (deathVisual == null || deathVisual.TeamId == 0))
            return;

        Color teamColor =
            team != null
                ? team.TeamColor
                : deathVisual.TeamColor;

        if (!hasAppliedTeamColor || appliedTeamColor != teamColor)
            SetTeamColor(teamColor);
    }

    public void SetTeamColor(Color color)
    {
        if (behindOutline == null)
            return;

        var mpb = new MaterialPropertyBlock();
        behindOutline.GetPropertyBlock(mpb);
        mpb.SetColor(TeamColorID, color);
        mpb.SetFloat(PatternDirectionID, 1f);
        mpb.SetFloat(PatternEnabledID, 1f);
        behindOutline.SetPropertyBlock(mpb);
        appliedTeamColor = color;
        hasAppliedTeamColor = true;
    }

    public void SetBehind(bool behind)
    {
        if (heroRenderer != null)
        {
            heroRenderer.sortingLayerName =
                behind ? HERO_BEHIND_LAYER : HERO_LAYER;
        }

        if (shadowRenderer != null)
        {
            shadowRenderer.sortingLayerName =
                behind ? SHADOW_BEHIND_LAYER : SHADOW_LAYER;
        }

        if (behindOutline != null)
        {
            behindOutline.enabled = behind;
        }
    }
}