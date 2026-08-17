using UnityEngine;

public class GuitaristGhostTrail : MonoBehaviour, IAbilityVisual
{
    [Header("Lifetime")]
    [SerializeField] private float ghostLifetime = 0.3f;

    [Header("Colors")]
    [SerializeField] private Color[] ghostColors;

    private HeroVisual heroVisual;
    private SpriteRenderer sourceRenderer;

    private float spawnTimer;

    private float power;

    public void Initialize(Transform owner, float power)
    {
        heroVisual = owner.GetComponentInChildren<HeroVisual>();

        if (heroVisual != null)
            sourceRenderer = heroVisual.SpriteRenderer;

        this.power = power;
    }

    private float GetSpawnInterval()
    {
        int stacks = Mathf.RoundToInt(
            (power - 1f) / 0.05f
        );

        return Mathf.Lerp(
            0.12f,
            0.025f,
            Mathf.Clamp01((stacks - 1) / 7f)
        );
    }

    private void Update()
    {
        if (sourceRenderer == null)
            return;

        spawnTimer -= Time.deltaTime;

        if (spawnTimer <= 0f)
        {
            spawnTimer = GetSpawnInterval();
            CreateGhost();
        }
    }

    private void CreateGhost()
    {
        GameObject ghost = new GameObject("GuitaristGhost");

        ghost.transform.position =
            sourceRenderer.transform.position;

        ghost.transform.rotation =
            sourceRenderer.transform.rotation;

        ghost.transform.localScale =
            sourceRenderer.transform.lossyScale;

        SpriteRenderer ghostRenderer =
            ghost.AddComponent<SpriteRenderer>();

        ghostRenderer.sprite =
            sourceRenderer.sprite;

        ghostRenderer.flipX =
            sourceRenderer.flipX;

        ghostRenderer.flipY =
            sourceRenderer.flipY;

        ghostRenderer.sortingLayerID =
            sourceRenderer.sortingLayerID;

        ghostRenderer.sortingOrder =
            sourceRenderer.sortingOrder - 1;

        Color color;

        if (ghostColors != null && ghostColors.Length > 0)
        {
            color = ghostColors[
                Random.Range(0, ghostColors.Length)
            ];
        }
        else
        {
            color = Color.white;
        }

        color.a = 0.5f;
        ghostRenderer.color = color;

        Destroy(ghost, ghostLifetime);
    }
}