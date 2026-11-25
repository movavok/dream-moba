using UnityEngine;

[CreateAssetMenu(menuName = "Game/Status Effect")]
public sealed class StatusDefinition : ScriptableObject
{
    [Min(1)] public int id = 1;
    public bool negative = true;
    public bool cleansable = true;
    public bool bypassImmunity;
    public bool blocksDetection;
    public bool blocksActions;
    [Min(0f)] public float duration = 1f;
    [Min(1)] public int maxStacks = 1;
    public StatModifier[] modifiers;
    public Sprite worldOverlay;
    public GameObject worldEffectPrefab;
    public Sprite screenOverlay;
    public Vector3 worldOffset = new(0f, 0.6f, 0f);
    [Range(0f, 1f)] public float screenOpacity = 1f;
    [Min(0.01f)] public float screenFadeIn = 0.1f;
    [Min(0.01f)] public float screenFadeOut = 0.18f;
}
