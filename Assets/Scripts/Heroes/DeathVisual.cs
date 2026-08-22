using Unity.Netcode;
using UnityEngine;
using UnityEngine.U2D.Animation;
using Unity.Collections;

public class DeathVisual : NetworkBehaviour
{
    [SerializeField] private SpriteLibrary spriteLibrary;

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

    public void Initialize(HeroDefinition hero)
    {
        if (!IsServer)
            return;

        if (hero == null)
        {
            Debug.LogError("DeathVisual: Hero is null!");
            return;
        }

        heroId.Value = hero.id;
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