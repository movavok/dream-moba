using UnityEngine;
using UnityEngine.U2D.Animation;

public class HeroVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SpriteRenderer behindOutline;
    [SerializeField] private SpriteLibrary spriteLibrary;

    public SpriteRenderer SpriteRenderer => spriteRenderer;

    private HeroDefinition heroData;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteLibrary == null)
            spriteLibrary = GetComponent<SpriteLibrary>();
    }

    public void Initialize(HeroDefinition hero)
    {
        heroData = hero;

        if (heroData == null)
        {
            Debug.LogError("HeroVisual: HeroData is null!");
            return;
        }

        if (heroData.visual == null)
        {
            Debug.LogError("HeroVisual: VisualDefinition is null!");
            return;
        }

        if (spriteLibrary == null)
        {
            Debug.LogError("HeroVisual: SpriteLibrary not found!");
            return;
        }

        if (heroData.visual.spriteLibrary == null)
        {
            Debug.LogError(
                $"HeroVisual: {heroData.heroName} has no Sprite Library!"
            );
            return;
        }

        spriteLibrary.spriteLibraryAsset =
            heroData.visual.spriteLibrary;
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null || behindOutline == null)
            return;

        behindOutline.sprite = spriteRenderer.sprite;
    }
}