using UnityEngine;

public class ProjectileSorting : MonoBehaviour
{
    [SerializeField] private SpriteRenderer projectileRenderer;
    [SerializeField] private SpriteRenderer behindOutline;

    private const string PROJECTILE_LAYER = "Projectile";
    private const string PROJECTILE_BUILDING_LAYER = "ProjectileBuilding";

    private void Awake()
    {
        if (projectileRenderer == null)
            projectileRenderer = GetComponentInChildren<SpriteRenderer>();
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