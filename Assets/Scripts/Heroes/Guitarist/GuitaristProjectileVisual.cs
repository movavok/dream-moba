using UnityEngine;

public class GuitaristProjectileVisual : MonoBehaviour, IProjectileVisual
{
    [SerializeField] private SpriteRenderer glowRenderer;
    [SerializeField] private Color[] streakColors;

    private ProjectileNetwork projectileNetwork;

    public int ColorCount =>
        streakColors != null
            ? streakColors.Length
            : 0;

    private void Awake()
    {
        projectileNetwork =
            GetComponent<ProjectileNetwork>();
    }

    public void Initialize(int colorIndex)
    {
        if (glowRenderer == null)
        {
            Debug.LogError(
                "GuitaristProjectileVisual: Glow Renderer is not assigned!"
            );

            return;
        }

        if (streakColors == null || streakColors.Length == 0)
        {
            Debug.LogError(
                "GuitaristProjectileVisual: No streak colors assigned!"
            );

            return;
        }

        colorIndex =
            colorIndex % streakColors.Length;

        Color color =
            streakColors[colorIndex];

        glowRenderer.color = color;

        if (projectileNetwork != null)
        {
            projectileNetwork.SetTrailColor(color);
        }
    }
}