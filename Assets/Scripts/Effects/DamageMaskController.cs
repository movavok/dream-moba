using UnityEngine;

public class DamageMaskController : MonoBehaviour
{
    [SerializeField] private HeroVisual heroVisual;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private SpriteMask spriteMask;

    private Sprite lastSprite;

    private void Awake()
    {
        if (spriteMask == null)
            spriteMask = GetComponent<SpriteMask>();

        if (heroVisual == null)
            heroVisual = GetComponentInParent<HeroVisual>();

        if (visual == null && heroVisual != null)
            visual = heroVisual.SpriteRenderer;
    }

    private void LateUpdate()
    {
        if (visual == null || spriteMask == null)
            return;

        Sprite currentSprite = visual.sprite;

        if (currentSprite == null || currentSprite == lastSprite)
            return;

        spriteMask.sprite = currentSprite;
        lastSprite = currentSprite;
    }
}
