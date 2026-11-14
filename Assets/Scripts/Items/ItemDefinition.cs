using UnityEngine;

[CreateAssetMenu(
    fileName = "NewItem",
    menuName = "Game/Item Definition"
)]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Stacking")]
    [Min(1)]
    public int maxStack = 10;

    [Header("Death")]
    public bool dropOnDeath;

    [Header("Throw")]
    public bool throwable;
    public GameObject worldPrefab;
    [Min(0f)]
    public float normalThrowMultiplier = 1f;
    [Min(0f)]
    public float strongThrowMultiplier = 2f;

    [Header("Use")]
    public ItemUseAction useAction;
    [Min(0f)] public float useCooldown;

    [Header("Passive (while in inventory)")]
    public StatModifier[] passiveModifiers;

    [Header("Selected bonus (while slot selected)")]
    public StatModifier[] selectedModifiers;

    public bool IsStackable => maxStack > 1;
}
