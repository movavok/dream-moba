using UnityEngine;

public class GuitaristAura : MonoBehaviour, IAbilityVisual
{
    private HeroVisual heroVisual;
    private SpriteRenderer sourceRenderer;
    private SpriteRenderer auraRenderer;

    private float power;

    public void Initialize(Transform owner, float visualPower)
    {
        power = visualPower;

        heroVisual = owner.GetComponentInChildren<HeroVisual>();

        if (heroVisual == null)
        {
            Debug.LogError("GuitaristAura: HeroVisual not found!");
            return;
        }

        sourceRenderer = heroVisual.SpriteRenderer;

        if (sourceRenderer == null)
        {
            Debug.LogError("GuitaristAura: Source SpriteRenderer not found!");
            return;
        }

        auraRenderer = GetComponent<SpriteRenderer>();

        if (auraRenderer == null)
        {
            Debug.LogError(
                "GuitaristAura: SpriteRenderer not found on aura prefab!"
            );
            return;
        }

        UpdateVisual();
    }

    private void LateUpdate()
    {
        if (sourceRenderer == null || auraRenderer == null)
            return;

        UpdateVisual();
    }

    private void UpdateVisual()
    {
        auraRenderer.sprite = sourceRenderer.sprite;

        auraRenderer.flipX = sourceRenderer.flipX;
        auraRenderer.flipY = sourceRenderer.flipY;

        auraRenderer.sortingLayerID =
            sourceRenderer.sortingLayerID;

        auraRenderer.sortingOrder =
            sourceRenderer.sortingOrder - 1;
    }
}
