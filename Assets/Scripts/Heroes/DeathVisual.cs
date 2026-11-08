using Unity.Netcode;
using UnityEngine;
using UnityEngine.U2D.Animation;
using Unity.Collections;

public class DeathVisual : NetworkBehaviour
{
    [SerializeField] private SpriteLibrary spriteLibrary;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SpriteRenderer behindOutline;

    private NetworkVariable<short> teamId =
        new NetworkVariable<short>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public short TeamId => teamId.Value;

    public Color TeamColor
    {
        get
        {
            TeamManager manager = FindAnyObjectByType<TeamManager>();

            if (manager == null)
                return Color.white;

            TeamDefinition.TeamData data =
                manager.GetTeamData(teamId.Value);

            return data != null
                ? data.color
                : Color.white;
        }
    }

    private NetworkVariable<FixedString64Bytes> heroId =
        new NetworkVariable<FixedString64Bytes>(
            "",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private void Awake()
    {
        if (spriteLibrary == null)
            spriteLibrary = GetComponentInChildren<SpriteLibrary>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (behindOutline == null)
            behindOutline = GetComponentInChildren<SpriteRenderer>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        heroId.OnValueChanged += OnHeroIdChanged;

        ApplyHeroVisual();
    }

    private void OnHeroIdChanged(
        FixedString64Bytes oldId,
        FixedString64Bytes newId)
    {
        ApplyHeroVisual();
    }

    public void Initialize(HeroDefinition hero, short deadPlayerTeamId)
    {
        if (!IsServer)
            return;

        if (hero == null)
        {
            Debug.LogError("DeathVisual: Hero is null!");
            return;
        }

        heroId.Value = hero.id;
        teamId.Value = deadPlayerTeamId;
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null || behindOutline == null)
            return;

        behindOutline.sprite = spriteRenderer.sprite;
    }

    private void ApplyHeroVisual()
    {
        if (heroId.Value.IsEmpty)
            return;

        if (HeroDatabase.Instance == null)
        {
            Debug.LogError(
                "DeathVisual: HeroDatabase not found!"
            );
            return;
        }

        HeroDefinition hero =
            HeroDatabase.Instance.GetHero(
                heroId.Value.ToString()
            );

        if (hero == null)
            return;

        if (hero.visual == null)
        {
            Debug.LogError(
                $"DeathVisual: {hero.heroName} has no VisualDefinition!"
            );
            return;
        }

        if (hero.visual.spriteLibrary == null)
        {
            Debug.LogError(
                $"DeathVisual: {hero.heroName} has no Sprite Library!"
            );
            return;
        }

        if (spriteLibrary == null)
        {
            Debug.LogError(
                "DeathVisual: SpriteLibrary not found!"
            );
            return;
        }

        spriteLibrary.spriteLibraryAsset =
            hero.visual.spriteLibrary;
    }
}