using UnityEngine;

public class InteractionOutline : MonoBehaviour
{
    [SerializeField] private float highlightAlpha = 0.2f;
    [SerializeField] private float bounceHeight = 0.04f;
    [SerializeField] private float bounceDuration = 0.25f;

    private SpriteRenderer[] sources;
    private Material[] sourceMaterials;
    private Material[] highlightMaterials;
    private Vector3[] sourcePositions;
    private bool highlighted;
    private float bounceTime;

    public void Configure(params SpriteRenderer[] sourceRenderers)
    {
        sources = sourceRenderers;

        if (sourceMaterials != null)
            return;

        sourceMaterials = new Material[sources.Length];
        highlightMaterials = new Material[sources.Length];
        sourcePositions = new Vector3[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null)
                continue;

            sourceMaterials[i] = sources[i].sharedMaterial;
            Shader shader = Shader.Find("DreamMoba/InteractionHighlight");
            if (shader == null)
            {
                throw new MissingReferenceException(
                    "DreamMoba/InteractionHighlight shader was not found."
                );
            }

            highlightMaterials[i] = new Material(
                shader
            );
            highlightMaterials[i].SetFloat(
                "_HighlightStrength",
                highlightAlpha
            );
            sourcePositions[i] = sources[i].transform.localPosition;
        }
    }

    public void SetHighlighted(bool value)
    {
        highlighted = value;
        bounceTime = value ? 0f : bounceDuration;

        if (sourceMaterials == null)
            return;

        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null)
                continue;

            sources[i].sharedMaterial =
                value ? highlightMaterials[i] : sourceMaterials[i];
            sources[i].transform.localPosition = sourcePositions[i];
        }
    }

    public void SetColor(Color color)
    {
    }

    private void Update()
    {
        if (!highlighted || sourceMaterials == null)
            return;

        bounceTime += Time.deltaTime;
        float bounceProgress =
            Mathf.Clamp01(bounceTime / Mathf.Max(0.01f, bounceDuration));
        float bounce = Mathf.Sin(bounceProgress * Mathf.PI) * bounceHeight;
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null)
                continue;

            sources[i].transform.localPosition =
                sourcePositions[i] + Vector3.up * bounce;
        }
    }
}
