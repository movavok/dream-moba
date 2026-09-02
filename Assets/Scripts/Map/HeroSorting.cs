using UnityEngine;

public class HeroSorting : MonoBehaviour
{
    [SerializeField] private SpriteRenderer heroRenderer;
    [SerializeField] private SpriteRenderer shadowRenderer;

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
    }
}