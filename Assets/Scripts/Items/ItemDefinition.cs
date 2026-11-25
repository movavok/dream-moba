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
    public Color nameColor = Color.white;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Stacking")]
    [Min(1)]
    public int maxStack = 10;

    [Header("Charges (0 = ordinary stack)")]
    [Min(0)] public int maxCharges;
    public bool blocksBasicAttacks;
    public bool ignitesBasicAttacks;

    [Header("Death")]
    public bool dropOnDeath;

    [Header("Containers")]
    public bool allowInContainers = true;

    [Header("Throw")]
    public bool throwable;
    public GameObject worldPrefab;
    [Min(0f)]
    public float normalThrowMultiplier = 1f;
    [Min(0f)]
    public float strongThrowMultiplier = 2f;

    [Header("Cooking")]
    public bool canBeCooked;
    public ItemDefinition cookedResult;
    [Min(0.1f)] public float cookingDuration = 8f;
    public Sprite cookingRawSprite;
    public Sprite cookingResultSprite;

    [Header("Use")]
    public ItemUseAction useAction;
    [Min(0f)] public float useCooldown;

    [Header("Passive (while in inventory)")]
    public StatModifier[] passiveModifiers;

    [Header("Selected bonus (while slot selected)")]
    public StatModifier[] selectedModifiers;

    public bool IsStackable => maxStack > 1;

    public string ColoredDisplayName => string.IsNullOrEmpty(displayName)
        ? displayName
        : $"<color=#{ColorUtility.ToHtmlStringRGBA(nameColor)}>{displayName}</color>";

    public string GetTooltipName(InventorySlot slot) => maxCharges > 0 && !slot.IsEmpty
        ? $"{ColoredDisplayName} ({slot.charges})" : ColoredDisplayName;

    public string AudioGroup =>
        AudioGroups.Item(
            string.IsNullOrEmpty(id) ? name : id);
}
