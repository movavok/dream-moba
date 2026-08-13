using System;

[Serializable]
public class StreakDefinition
{
    public float streakDamageBonus = 0.01f;
    public float streakTimeoutMultiplier = 2f;

    public int maxStacks = 999;
}