using System.Collections.Generic;
using UnityEngine;

// Local-only visuals: the item position is already synchronized by NetworkTransform.
public class ItemSorting : MonoBehaviour
{
    private const string VisibleLayer = "ProjectileBehind";
    private const string BehindLayer = "ProjectileBuilding";

    private static readonly int TeamColorId = Shader.PropertyToID("_TeamColor");
    private static readonly int PatternEnabledId = Shader.PropertyToID("_PatternEnabled");
    private static Material behindMaterial;

    private readonly List<SpriteRenderer> sources = new();
    private readonly List<SpriteRenderer> silhouettes = new();
    private bool behind;

    private void Awake()
    {
        // Trigger callbacks need at least one Rigidbody2D. This body never moves
        // the item; the server controls its transform during throws.
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        Shader shader = Shader.Find("DreamMoba/BehindBuildingPattern");
        if (shader == null)
        {
            Debug.LogError("Missing BehindBuildingPattern shader", this);
            return;
        }

        if (behindMaterial == null)
            behindMaterial = new Material(shader) { name = "Item Behind Building" };

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer source in renderers)
        {
            sources.Add(source);
            source.sortingLayerName = VisibleLayer;

            GameObject visual = new GameObject("BehindOutline");
            visual.transform.SetParent(source.transform, false);
            SpriteRenderer outline = visual.AddComponent<SpriteRenderer>();
            outline.sharedMaterial = behindMaterial;
            outline.sortingLayerName = "Effect";
            outline.sortingOrder = 5;
            outline.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetColor(TeamColorId, Color.yellow);
            properties.SetFloat(PatternEnabledId, 0f);
            outline.SetPropertyBlock(properties);
            outline.enabled = false;
            silhouettes.Add(outline);
        }
    }

    public void SetBehind(bool value)
    {
        behind = value;
        foreach (SpriteRenderer source in sources)
            source.sortingLayerName = value ? BehindLayer : VisibleLayer;
    }

    private void LateUpdate()
    {
        for (int i = 0; i < sources.Count; i++)
        {
            silhouettes[i].sprite = sources[i].sprite;
            silhouettes[i].flipX = sources[i].flipX;
            silhouettes[i].flipY = sources[i].flipY;
            silhouettes[i].enabled = behind && sources[i].enabled;
        }
    }
}
