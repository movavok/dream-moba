using UnityEngine;

[CreateAssetMenu(
    fileName = "NewHero",
    menuName = "Game/Hero Definition"
)]
public class HeroDefinition : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string heroName;
    [TextArea]
    public string description;

    [Header("Streaks")]
    public StreakDefinition streaks;

    [Header("Stats")]
    public HeroStats stats;

    [Header("Basic Attack")]
    public AttackDefinition attack;

    [Header("Passive")]
    public PassiveDefinition passive;

    [Header("Abilities")]
    public AbilityDefinition ability1;
    public AbilityDefinition ability2;

    [Header("Visual")]
    public HeroVisualDefinition visual;
}