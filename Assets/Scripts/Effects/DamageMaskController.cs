using UnityEngine;

public class DamageMaskController : MonoBehaviour
{
    [SerializeField] private HeroVisual heroVisual;
    [SerializeField] private SpriteMask spriteMask;

    private Sprite lastSprite;

    private void Awake()
    {
        if (spriteMask == null)
            spriteMask = GetComponent<SpriteMask>();

        if (heroVisual == null)
            heroVisual = GetComponentInParent<HeroVisual>();
    }

    private void LateUpdate()
    {
        if (heroVisual == null || spriteMask == null)
            return;

        Sprite currentSprite = heroVisual.SpriteRenderer.sprite;

        if (currentSprite == null || currentSprite == lastSprite)
            return;

        spriteMask.sprite = currentSprite;
        lastSprite = currentSprite;
    }
}